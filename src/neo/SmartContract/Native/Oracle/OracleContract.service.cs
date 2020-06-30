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
        private const long BaseFee = 1000;

        public OracleContract()
        {
            Manifest.Features = ContractFeatures.HasStorage;
            var events = new List<ContractEventDescriptor>(Manifest.Abi.Events)
            {
                new ContractEventDescriptor()
                {
                    Name = "Request",
                    Parameters = new ContractParameterDefinition[]
                    {
                        new ContractParameterDefinition()
                        {
                            Name = "requestTxHash",
                            Type = ContractParameterType.Hash256
                        }
                    }
                }
            };

            Manifest.Abi.Events = events.ToArray();
        }

        [ContractMethod(0_01000000, CallFlags.All)]
        public bool Request(ApplicationEngine engine, string urlstring, string filterArgs, UInt160 callBackContractHash, string callBackMethod, long oracleFee)
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
                            FilterPath = filterArgs,
                            CallBackContract = callBackContractHash,
                            CallBackMethod = callBackMethod,
                            OracleFee = oracleFee,
                            Status = RequestStatusType.Request
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
            if (request.OracleFee < GetPerRequestFee(engine.Snapshot) * oracleNodes.Length + BaseFee) throw new InvalidOperationException("OracleFee is not enough");
            if (!(engine.GetScriptContainer() is Transaction)) return false;
            Transaction tx = (Transaction)engine.GetScriptContainer();
            request.RequestTxHash = tx.Hash;
            request.ValidHeight = engine.GetBlockchainHeight() + GetRequestMaxValidHeight(engine.Snapshot);
            StorageKey key = CreateRequestKey(tx.Hash);
            OracleRequest init_request = engine.Snapshot.Storages.TryGet(key)?.GetInteroperable<OracleRequest>();
            if (init_request != null) return false;
            UInt160 oracleAddress = GetOracleMultiSigAddress(engine.Snapshot);
            engine.AddGas(request.OracleFee);
            NativeContract.GAS.Mint(engine, oracleAddress, request.OracleFee);
            engine.Snapshot.Storages.Add(key, new StorageItem(request));
            engine.SendNotification(Hash, "Request", new Array() { });
            return true;
        }

        public OracleRequest GetRequest(StoreView snapshot, UInt256 RequestTxHash)
        {
            return snapshot.Storages.TryGet(CreateRequestKey(RequestTxHash))?.GetInteroperable<OracleRequest>();
        }

        private bool Response(ApplicationEngine engine, OracleResponseAttribute response)
        {
            StoreView snapshot = engine.Snapshot;
            StorageKey key_request = CreateRequestKey(response.RequestTxHash);
            OracleRequest request = snapshot.Storages.TryGet(key_request).GetInteroperable<OracleRequest>();
            if (request is null || request.Status != RequestStatusType.Request || request.ValidHeight < snapshot.Height) return false;
            request.Status = RequestStatusType.Ready;
            return true;
        }

        [ContractMethod(0_01000000, CallFlags.All)]
        public void CallBack(ApplicationEngine engine)
        {
            UInt160 oracleAddress = GetOracleMultiSigAddress(engine.Snapshot);
            if (!engine.CheckWitnessInternal(oracleAddress) || !(engine.ScriptContainer is Transaction)) throw new InvalidOperationException();
            Transaction tx = (Transaction)engine.ScriptContainer;
            TransactionAttribute attribute = tx.Attributes.Where(p => p is OracleResponseAttribute).FirstOrDefault();
            if (attribute is null) throw new InvalidOperationException();
            OracleResponseAttribute response = (OracleResponseAttribute)attribute;
            UInt256 RequestTxHash = response.RequestTxHash;
            StorageKey key_request = CreateRequestKey(RequestTxHash);
            OracleRequest request = engine.Snapshot.Storages.GetAndChange(key_request)?.GetInteroperable<OracleRequest>();
            if (request is null || request.Status != RequestStatusType.Ready) throw new InvalidOperationException();

            if (!response.Error)
            {
                byte[] data = response.Result;
                engine.CallFromNativeContract(() =>
                {
                    request.Status = RequestStatusType.Successed;
                }, request.CallBackContract, request.CallBackMethod, data);
            }
            else
            {
                request.Status = RequestStatusType.Failed;
            }
        }

        protected override void OnPersist(ApplicationEngine engine)
        {
            base.OnPersist(engine);
            foreach (Transaction tx in engine.Snapshot.PersistingBlock.Transactions)
            {
                TransactionAttribute attribute = tx.Attributes.Where(p => p is OracleResponseAttribute).FirstOrDefault();
                if (attribute is null) continue;
                OracleResponseAttribute response = (OracleResponseAttribute)attribute;
                if (Response(engine, response))
                {
                    UInt160[] oracleNodes = GetOracleValidators(engine.Snapshot).Select(p => Contract.CreateSignatureContract(p).ScriptHash).ToArray();
                    foreach (UInt160 account in oracleNodes)
                    {
                        NativeContract.GAS.Mint(engine, account, response.FilterCost + GetPerRequestFee(engine.Snapshot));
                    }
                    StorageKey key_request = CreateRequestKey(response.RequestTxHash);
                    OracleRequest request = engine.Snapshot.Storages.TryGet(key_request)?.GetInteroperable<OracleRequest>();
                    long refundGas = request.OracleFee - (response.FilterCost + GetPerRequestFee(engine.Snapshot)) * oracleNodes.Length - tx.NetworkFee - tx.SystemFee;
                    Transaction requestTx = engine.Snapshot.Transactions.TryGet(request.RequestTxHash)?.Transaction;
                    NativeContract.GAS.Mint(engine, requestTx.Sender, refundGas);
                    NativeContract.GAS.Burn(engine, tx.Sender, refundGas);
                }
            }
        }

        private StorageKey CreateRequestKey(UInt256 requestTxHash)
        {
            return CreateStorageKey(Prefix_Request, requestTxHash.ToArray());
        }
    }
}
