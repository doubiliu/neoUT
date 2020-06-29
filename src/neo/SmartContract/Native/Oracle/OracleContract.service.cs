using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using Array = Neo.VM.Types.Array;

namespace Neo.SmartContract.Native
{
    public partial class OracleContract : NativeContract
    {
        public override string Name => "Oracle";
        public override int Id => -5;
        private const byte Prefix_Request = 21;
        private const byte Prefix_Response = 17;

        public OracleContract()
        {
            Manifest.Features = ContractFeatures.HasStorage;
            var events = new List<ContractEventDescriptor>(Manifest.Abi.Events)
            {
                new ContractMethodDescriptor()
                {
                    Name = "Request",
                    Parameters = new ContractParameterDefinition[]
                    {
                        new ContractParameterDefinition()
                        {
                            Name = "requestTxHash",
                            Type = ContractParameterType.Hash256
                        }
                    },
                    ReturnType = ContractParameterType.Boolean
                }
            };

            Manifest.Abi.Events = events.ToArray();
        }

        [ContractMethod(0_01000000, CallFlags.All)]
        public bool Request(ApplicationEngine engine, string urlstring,string filterArgs, UInt160 callBackContractHash, string callBackMethod, long oracleFee)
        {
            if (!Uri.TryCreate(urlstring, UriKind.Absolute, out var url)) throw new ArgumentException();
            // Create request
            OracleRequest request;
            switch (url.Scheme.ToLowerInvariant())
            {
                case "http":
                case "https":
                    {
                        request = new OracleRequest()
                        {
                            URL = url,
                            FilterArgs = filterArgs,
                            CallBackContractHash = callBackContractHash,
                            CallBackMethod = callBackMethod,
                            OracleFee = oracleFee,
                            Status=RequestStatusType.REQUEST
                        };
                        break;
                    }
                default: throw new ArgumentException($"The scheme '{url.Scheme}' is not allowed");
            }
            return Request(engine, request);
        }

        private bool Request(ApplicationEngine engine, OracleRequest request)
        {
            UInt160[] oracleNodes = GetOracleValidators(engine.Snapshot).Select(p => Contract.CreateSignatureContract(p).ScriptHash).ToArray();
            if (request.OracleFee < GetPerRequestFee(engine.Snapshot) * oracleNodes.Length) throw new InvalidOperationException("OracleFee is not enough");
            if (!(engine.GetScriptContainer() is Transaction)) return false;
            Transaction tx = (Transaction)engine.GetScriptContainer();
            request.RequestTxHash = tx.Hash;
            request.ValidHeight = engine.GetBlockchainHeight() + GetValidHeight(engine.Snapshot);
            StorageKey key = CreateRequestKey(tx.Hash);
            OracleRequest init_request = engine.Snapshot.Storages.TryGet(key)?.GetInteroperable<OracleRequest>();
            if (init_request != null) return false;
            engine.AddGas(request.OracleFee);
            engine.Snapshot.Storages.Add(key, new StorageItem(request));
            engine.SendNotification(Hash, "Request", new Array() { });
            return true;
        }

        public OracleRequest GetRequest(StoreView snapshot, UInt256 RequestTxHash)
        {
            return snapshot.Storages.TryGet(CreateRequestKey(RequestTxHash))?.GetInteroperable<OracleRequest>();
        }

        private bool Response(ApplicationEngine engine, OracleResponse response)
        {
            StoreView snapshot = engine.Snapshot;
            StorageKey key_request = CreateRequestKey(response.RequestTxHash);
            OracleRequest request = snapshot.Storages.TryGet(key_request).GetInteroperable<OracleRequest>();
            if (request is null) return false;
            if (request.Status != RequestStatusType.REQUEST) return false;
            if (request.ValidHeight < snapshot.Height) return false;
            request.Status = RequestStatusType.READY;
            return true;
        }

        [ContractMethod(0_01000000, CallFlags.All)]
        public void CallBack(ApplicationEngine engine)
        {
            UInt160 oracleAddress = GetOracleMultiSigAddress(engine.Snapshot);
            if (!engine.CheckWitnessInternal(oracleAddress)) throw new InvalidOperationException();

            if (!(engine.ScriptContainer is Transaction)) throw new InvalidOperationException();
            Transaction tx = (Transaction)engine.ScriptContainer;
            TransactionAttribute attribute = tx.Attributes.Where(p => p is OracleResponseAttribute).FirstOrDefault();
            if (attribute is null) throw new InvalidOperationException();
            OracleResponse response = ((OracleResponseAttribute)attribute).Response;
            UInt256 RequestTxHash = response.RequestTxHash;
            StorageKey key_request = CreateRequestKey(RequestTxHash);
            OracleRequest request = engine.Snapshot.Storages.TryGet(key_request)?.GetInteroperable<OracleRequest>();
            if (request is null) throw new InvalidOperationException();
            if (request.Status != RequestStatusType.READY) throw new InvalidOperationException();

            byte[] data = response.Result;
            long GasLeftBeforeCallBack = engine.GasLeft;
            long FilterCost = response.FilterCost;

            engine.CallFromNativeContract(() =>
            {
                long GasLeftAfterCallBack = engine.GasLeft;
                long CallBackCost = GasLeftBeforeCallBack - GasLeftAfterCallBack;
                StorageKey key_request = CreateRequestKey(RequestTxHash);
                OracleRequest request = engine.Snapshot.Storages.TryGet(key_request)?.GetInteroperable<OracleRequest>();
                UInt160[] oracleNodes = GetOracleValidators(engine.Snapshot).Select(p => Contract.CreateSignatureContract(p).ScriptHash).ToArray();
                long refundGas = request.OracleFee - (FilterCost + GetPerRequestFee(engine.Snapshot)) * oracleNodes.Length - CallBackCost;

                Transaction tx = engine.Snapshot.Transactions.TryGet(RequestTxHash)?.Transaction;
                UInt160 account = tx.Sender;
                request = engine.Snapshot.Storages.GetAndChange(key_request).GetInteroperable<OracleRequest>();
                request.Status = RequestStatusType.SUCCESSED;
                if (refundGas > 0) NativeContract.GAS.Mint(engine, account, refundGas);
            }, request.CallBackContractHash, request.CallBackMethod, data);
        }

        protected override void OnPersist(ApplicationEngine engine)
        {
            base.OnPersist(engine);
            foreach (Transaction tx in engine.Snapshot.PersistingBlock.Transactions)
            {
                TransactionAttribute attribute = tx.Attributes.Where(p => p is OracleResponseAttribute).FirstOrDefault();
                if (attribute is null) continue;
                if (tx.Sender != GetOracleMultiSigAddress(engine.Snapshot)) throw new InvalidOperationException();
                OracleResponse response = ((OracleResponseAttribute)attribute).Response;
                if (Response(engine, response))
                {
                    UInt160[] oracleNodes = GetOracleValidators(engine.Snapshot).Select(p => Contract.CreateSignatureContract(p).ScriptHash).ToArray();
                    foreach (UInt160 account in oracleNodes)
                    {
                        NativeContract.GAS.Mint(engine, account, response.FilterCost + GetPerRequestFee(engine.Snapshot));
                    }
                    StorageKey key_request = CreateRequestKey(response.RequestTxHash);
                    OracleRequest request = engine.Snapshot.Storages.TryGet(key_request)?.GetInteroperable<OracleRequest>();
                    long CallBackFee = request.OracleFee - (response.FilterCost + GetPerRequestFee(engine.Snapshot)) * oracleNodes.Length;
                    if (CallBackFee > 0) NativeContract.GAS.Mint(engine, tx.Sender, CallBackFee);
                }
            }
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
