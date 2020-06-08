#pragma warning disable IDE0051
#pragma warning disable IDE0060

using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native.Tokens;
using Neo.VM;
using Neo.VM.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using Array = Neo.VM.Types.Array;

namespace Neo.SmartContract.Native
{
    public sealed class OracleContract : NativeContract
    {
        public override string Name => "Oracle";
        public override int Id => -4;

        private const byte Prefix_Request = 21;
        private const byte Prefix_Response = 22;


        public OracleContract()
        {
            Manifest.Features = ContractFeatures.HasStorage;
        }

        internal override void Initialize(ApplicationEngine engine)
        {

        }

        [ContractMethod(0_01000000, ContractParameterType.Boolean, CallFlags.AllowStates, ParameterTypes = new[] { ContractParameterType.ByteArray, ContractParameterType.ByteArray }, ParameterNames = new[] { "OracleRequestType", "OracleRequest" })]
        public StackItem RegisterRequest(ApplicationEngine engine, Array args)
        {
            OracleRequestType type = (OracleRequestType)args[0].GetSpan().ToArray()[0];
            OracleRequest request = null;
            switch (type)
            {
                case OracleRequestType.HTTP:
                    request = args[1].GetSpan().AsSerializable<OracleHttpRequest>();
                    break;
                default:
                    return null;
            }
            return RegisterRequest(engine, request);
        }

        private bool RegisterRequest(ApplicationEngine engine, OracleRequest request)
        {
            if (request.ValidHeight < 0) throw new InvalidOperationException("ValidHeight can't be negnative");
            if (request.CallBackFee < 0) throw new InvalidOperationException("CallBackFee can't be negnative");
            if (request.OracleFee < 0) throw new InvalidOperationException("OracleFee can't be negnative");
            if (request.FilterFee < 0) throw new InvalidOperationException("FilterFee can't be negnative");
            Transaction tx = (Transaction)engine.GetScriptContainer();
            request.RequestTxHash = tx.Hash;
            request.ValidHeight += engine.GetBlockchainHeight();
            StorageKey key = CreateRequestKey(tx.Hash);
            RequestState requestState = engine.Snapshot.Storages.TryGet(key)?.GetInteroperable<RequestState>();
            if (requestState != null) return false;
            if (!NativeContract.GAS.Transfer(engine, tx.Sender, NativeContract.Oracle.Hash, request.OracleFee + request.CallBackFee + request.FilterFee)) return false;
            requestState = new RequestState() { request = request, status = 0 };
            engine.Snapshot.Storages.Add(key, new StorageItem(requestState));
            return true;
        }

        [ContractMethod(0_01000000, ContractParameterType.InteropInterface, CallFlags.ReadOnly, ParameterTypes = new[] { ContractParameterType.Hash256 }, ParameterNames = new[] { "RequestTxHash" })]
        public StackItem GetRequest(ApplicationEngine engine, Array args)
        {
            UInt256 RequestTxHash = args[0].GetSpan().AsSerializable<UInt256>();
            return GetRequest(engine.Snapshot, RequestTxHash).ToArray();
        }

        public OracleRequest GetRequest(StoreView snapshot, UInt256 RequestTxHash)
        {
            return snapshot.Storages.TryGet(CreateRequestKey(RequestTxHash))?.GetInteroperable<RequestState>().request;
        }

        [ContractMethod(0_01000000, ContractParameterType.InteropInterface, CallFlags.ReadOnly, ParameterTypes = new[] { ContractParameterType.Hash256 }, ParameterNames = new[] { "RequestTxHash" })]
        public StackItem GetResponse(ApplicationEngine engine, Array args)
        {
            UInt256 RequestTxHash = args[0].GetSpan().AsSerializable<UInt256>();
            return GetResponse(engine.Snapshot, RequestTxHash).ToArray();
        }

        private OracleResponse GetResponse(StoreView snapshot, UInt256 RequestTxHash)
        {
            return snapshot.Storages.TryGet(CreateResponseKey(RequestTxHash))?.GetInteroperable<ResponseState>().GetConsensusResponse(4);
        }

        private bool SubmitResponse(StoreView snapshot, OracleResponse response, UInt160 oracleNode)
        {
            //权限检查,发送者是否是Oracle节点
            StorageKey key_request = CreateRequestKey(response.RequestTxHash);
            RequestState request = snapshot.Storages.TryGet(key_request).GetInteroperable<RequestState>();
            if (request is null) return false;
            if (request.status != 0x00) return false;
            if (request.request.ValidHeight < snapshot.Height) return false;

            StorageKey key_existing_response = CreateResponseKey(response.RequestTxHash);
            ResponseState existing_response = snapshot.Storages.GetAndChange(key_existing_response, () => new StorageItem(new ResponseState())).GetInteroperable<ResponseState>();
            if (existing_response.GetConsensusResponse(4) != null) return false;
            existing_response.ResponseHashAndResponseMapping.Add(response.Hash, response);
            existing_response.NodeAndResponseHashMapping.Add(oracleNode, response.Hash);

            if (existing_response.GetConsensusResponse(4) != null)
                request.status = 0x01;

            return true;
        }

        private UInt160[] GetIncentiveAccount(StoreView snapshot, OracleResponse response)
        {
            StorageKey key = CreateResponseKey(response.RequestTxHash);
            ResponseState responsetState = snapshot.Storages.GetAndChange(key, () => new StorageItem(new ResponseState())).GetInteroperable<ResponseState>();
            return responsetState.GetIncentiveAccount(4);
        }

        [ContractMethod(0_01000000, ContractParameterType.Boolean, CallFlags.All, ParameterTypes = new[] { ContractParameterType.Hash256 }, ParameterNames = new[] { "RequestTxHash" })]
        public StackItem InvokeCallBackMethod(ApplicationEngine engine, Array args)
        {
            UInt256 RequestTxHash = args[0].GetSpan().AsSerializable<UInt256>();
            if (!(engine.ScriptContainer is Transaction)) return false;
            Transaction tx = (Transaction)engine.ScriptContainer;
            UInt160 account = tx.Sender;

            StorageKey key_request = CreateRequestKey(RequestTxHash);
            RequestState request = engine.Snapshot.Storages.TryGet(key_request)?.GetInteroperable<RequestState>();
            if (request.status != 0x01) return false;

            StorageKey key_response = CreateResponseKey(RequestTxHash);
            ResponseState response = engine.Snapshot.Storages.TryGet(key_response)?.GetInteroperable<ResponseState>();
            if (response is null) return false;
            OracleResponse final_response = response.GetConsensusResponse(4);
            if (final_response is null) return false;
            byte[] data = final_response.Result;
            engine.CallContractEx(request.request.CallBackContractHash, request.request.CallBackMethod, new Array() { data }, CallFlags.All);
            request = engine.Snapshot.Storages.GetAndChange(key_request).GetInteroperable<RequestState>();
            request.status = 0x02;
            NativeContract.GAS.Burn(engine, NativeContract.Oracle.Hash, request.request.CallBackFee);
            NativeContract.GAS.Mint(engine, account, request.request.CallBackFee);
            return true;
        }

        protected override bool OnPersist(ApplicationEngine engine)
        {
            if (!base.OnPersist(engine)) return false;
            foreach (Transaction tx in engine.Snapshot.PersistingBlock.Transactions)
            {
                OracleResponseAttribute attribute = (OracleResponseAttribute)tx.Attributes.Where(p => p is OracleResponseAttribute).First();
                if (attribute is null) continue;
                if (SubmitResponse(engine.Snapshot, attribute.response, tx.Sender))
                {
                    UInt160[] IncentiveAccount = GetIncentiveAccount(engine.Snapshot, attribute.response);
                    if (IncentiveAccount != null)
                    {
                        OracleRequest request = GetRequest(engine.Snapshot, attribute.response.RequestTxHash);
                        NativeContract.GAS.Burn(engine, NativeContract.Oracle.Hash, request.OracleFee / IncentiveAccount.Length + request.FilterFee / IncentiveAccount.Length);
                        foreach (UInt160 account in IncentiveAccount)
                        {
                            NativeContract.GAS.Mint(engine, account, request.OracleFee / IncentiveAccount.Length + request.FilterFee / IncentiveAccount.Length);
                        }
                    }
                }
            }
            return true;
        }

        private StorageKey CreateRequestKey(UInt256 requestTxHash)
        {
            return CreateStorageKey(Prefix_Request, requestTxHash.ToArray());
        }

        private StorageKey CreateResponseKey(UInt256 requestTxHash)
        {
            return CreateStorageKey(Prefix_Response, requestTxHash.ToArray());
        }
    }
}
