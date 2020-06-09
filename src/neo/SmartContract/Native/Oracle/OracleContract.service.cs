#pragma warning disable IDE0051
#pragma warning disable IDE0060

using Neo.Cryptography.ECC;
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
    public sealed partial class OracleContract : NativeContract
    {
        public override string Name => "Oracle";
        public override int Id => -4;

        private const byte Prefix_Request = 21;
        private const byte Prefix_Response = 22;


        public OracleContract()
        {
            Manifest.Features = ContractFeatures.HasStorage;
            var events = new List<ContractEventDescriptor>(Manifest.Abi.Events)
            {
                new ContractMethodDescriptor()
                {
                    Name = "RegisterRequest",
                    Parameters = new ContractParameterDefinition[]
                    {
                        new ContractParameterDefinition()
                        {
                            Name = "requestTx",
                            Type = ContractParameterType.Hash256
                        }
                    },
                    ReturnType = ContractParameterType.Boolean
                }
            };

            Manifest.Abi.Events = events.ToArray();
        }

        [ContractMethod(0_01000000, ContractParameterType.Boolean, CallFlags.AllowStates, ParameterTypes = new[] { ContractParameterType.ByteArray, ContractParameterType.ByteArray }, ParameterNames = new[] { "OracleRequestType", "OracleRequest" })]
        public StackItem RegisterRequest(ApplicationEngine engine, Array args)
        {
            OracleRequestType type = (OracleRequestType)args[0].GetSpan().ToArray()[0];
            OracleRequest request = null;
            switch (type)
            {
                case OracleRequestType.HTTP:
                    request = ((InteropInterface)args[1]).GetInterface<OracleHttpRequest>();
                    break;
                default:
                    return false;
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
            engine.SendNotification(Hash, new Array(new StackItem[] { "RegisterRequest", request.RequestTxHash.ToArray() }));
            return true;
        }

        public OracleRequest GetRequest(StoreView snapshot, UInt256 RequestTxHash)
        {
            return snapshot.Storages.TryGet(CreateRequestKey(RequestTxHash))?.GetInteroperable<RequestState>().request;
        }

        public OracleResponse GetResponse(StoreView snapshot, UInt256 RequestTxHash)
        {
            UInt160[] validators = GetOracleValidators(snapshot)?.ToList().Select(p => Contract.CreateSignatureRedeemScript(p).ToScriptHash()).ToArray();
            return snapshot.Storages.TryGet(CreateResponseKey(RequestTxHash))?.GetInteroperable<ResponseState>().GetConsensusResponse(2 * validators.Length / 3);
        }

        private bool SubmitResponse(StoreView snapshot, OracleResponse response, UInt160 oracleNode)
        {
            UInt160[] validators = GetOracleValidators(snapshot)?.ToList().Select(p => Contract.CreateSignatureRedeemScript(p).ToScriptHash()).ToArray();
            if (!validators.Contains(oracleNode)) return false;
            StorageKey key_request = CreateRequestKey(response.RequestTxHash);
            RequestState request = snapshot.Storages.TryGet(key_request).GetInteroperable<RequestState>();
            if (request is null) return false;
            if (request.status != 0x00) return false;
            if (request.request.ValidHeight < snapshot.Height) return false;

            StorageKey key_existing_response = CreateResponseKey(response.RequestTxHash);
            ResponseState existing_response = snapshot.Storages.GetAndChange(key_existing_response, () => new StorageItem(new ResponseState())).GetInteroperable<ResponseState>();
            if (existing_response.GetConsensusResponse(2 * validators.Length / 3) != null) return false;
            existing_response.ResponseHashAndResponseMapping.Add(response.Hash, response);
            existing_response.NodeAndResponseHashMapping.Add(oracleNode, response.Hash);

            if (existing_response.GetConsensusResponse(2 * validators.Length / 3) != null)
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
            if (request.status != 0x01)
            {
                NativeContract.GAS.Mint(engine, account, engine.GasLeft);
                return false;
            }
            StorageKey key_response = CreateResponseKey(RequestTxHash);
            ResponseState response = engine.Snapshot.Storages.TryGet(key_response)?.GetInteroperable<ResponseState>();
            if (response is null)
            {
                NativeContract.GAS.Mint(engine, account, engine.GasLeft);
                return false;
            }
            UInt160[] validators = GetOracleValidators(engine.Snapshot)?.ToList().Select(p => Contract.CreateSignatureRedeemScript(p).ToScriptHash()).ToArray();
            OracleResponse final_response = response.GetConsensusResponse(2 * validators.Length / 3);
            if (final_response is null)
            {
                NativeContract.GAS.Mint(engine, account, engine.GasLeft);
                return false;
            }
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
                TransactionAttribute attribute = tx.Attributes.Where(p => p is OracleResponseAttribute).FirstOrDefault();
                if (attribute is null) continue;
                OracleResponse response = ((OracleResponseAttribute)attribute).response;
                if (SubmitResponse(engine.Snapshot, response, tx.Sender))
                {
                    UInt160[] IncentiveAccount = GetIncentiveAccount(engine.Snapshot, response);
                    if (IncentiveAccount != null)
                    {
                        OracleRequest request = GetRequest(engine.Snapshot, response.RequestTxHash);
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
