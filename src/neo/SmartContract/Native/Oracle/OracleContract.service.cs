#pragma warning disable IDE0051
#pragma warning disable IDE0060

using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Oracle;
using Neo.Persistence;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native.Tokens;
using Neo.VM;
using Neo.VM.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Array = Neo.VM.Types.Array;

namespace Neo.SmartContract.Native
{
    public partial class OracleContract : NativeContract
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
                            Name = "requestTxHash",
                            Type = ContractParameterType.Hash256
                        }
                    },
                    ReturnType = ContractParameterType.Boolean
                }
            };

            Manifest.Abi.Events = events.ToArray();
        }

        [ContractMethod(0_01000000, ContractParameterType.Boolean, CallFlags.All,
            ParameterTypes = new[] { ContractParameterType.String, ContractParameterType.ByteArray, ContractParameterType.String, ContractParameterType.String, ContractParameterType.ByteArray, ContractParameterType.String, ContractParameterType.Integer },
            ParameterNames = new[] { "url", "filterContract", "filterMethod", "filterArgs", "callBackContract", "callBackMethod", "oracleFee"  })]
        public StackItem RegisterRequest(ApplicationEngine engine, Array args)
        {
            if (args.Count != 7) throw new ArgumentException($"Provided arguments must be 7 instead of {args.Count}");
            //check args format
            if (!(args[0] is PrimitiveType urlItem) || !Uri.TryCreate(urlItem.GetString(), UriKind.Absolute, out var url) ||
                !(args[1] is StackItem filterContractItem) ||
                !(args[2] is StackItem filterMethodItem) ||
                !(args[3] is StackItem filterArgsItem) ||
                !(args[4] is StackItem CallBackContractItem) ||
                !(args[5] is StackItem CallBackMethodItem) ||
                !(args[6] is StackItem OracleFeeItem)
                ) throw new ArgumentException();
            // Create filter
            OracleFilter filter;
            if (filterMethodItem is PrimitiveType filterMethod)
            {
                filter = new OracleFilter()
                {
                    ContractHash = filterContractItem is PrimitiveType filterContract ? new UInt160(filterContract.Span) : throw new ArgumentException(),
                    FilterMethod = Encoding.UTF8.GetString(filterMethod.Span),
                    FilterArgs = filterArgsItem is PrimitiveType filterArgs ? Encoding.UTF8.GetString(filterArgs.Span) : ""
                };
            }
            else
            {
                if (!filterMethodItem.IsNull) throw new ArgumentException("If the filter it's defined, the values can't be null");
                filter = null;
            }
            // Create request
            OracleRequest request;
            switch (url.Scheme.ToLowerInvariant())
            {
                case "http":
                case "https":
                    {
                        request = new OracleHttpRequest()
                        {
                            Method = HttpMethod.GET,
                            URL = url,
                            Filter = filter,
                            CallBackContractHash = CallBackContractItem is PrimitiveType CallBackContract ? new UInt160(CallBackContract.Span) : throw new ArgumentException(),
                            CallBackMethod= Encoding.UTF8.GetString(CallBackMethodItem.GetSpan()),
                            OracleFee= (long)OracleFeeItem.GetBigInteger(),
                        };
                        break;
                    }
                default: throw new ArgumentException($"The scheme '{url.Scheme}' is not allowed");
            }
            return RegisterRequest(engine, request);
        }

        private bool RegisterRequest(ApplicationEngine engine, OracleRequest request)
        {      
            UInt160[] oracleNodes = GetOracleValidators(engine.Snapshot).Select(p => Contract.CreateSignatureContract(p).ScriptHash).ToArray();
            if (request.OracleFee < GetPerRequestFee(engine.Snapshot) * oracleNodes.Length) throw new InvalidOperationException("OracleFee is not enough");
            if (!(engine.GetScriptContainer() is Transaction)) return false;
            Transaction tx = (Transaction)engine.GetScriptContainer();
            request.RequestTxHash = tx.Hash;
            request.ValidHeight = engine.GetBlockchainHeight()+GetValidHeight(engine.Snapshot);
            StorageKey key = CreateRequestKey(tx.Hash);
            RequestState requestState = engine.Snapshot.Storages.TryGet(key)?.GetInteroperable<RequestState>();
            if (requestState != null) return false;
            engine.AddGas(request.OracleFee);
            requestState = new RequestState() { request = request, status = 0 };
            engine.Snapshot.Storages.Add(key, new StorageItem(requestState));
            engine.SendNotification(Hash, new Array(new StackItem[] { "RegisterRequest", request.RequestTxHash.ToArray() }));
            return true;
        }

        public OracleRequest GetRequest(StoreView snapshot, UInt256 RequestTxHash)
        {
            return snapshot.Storages.TryGet(CreateRequestKey(RequestTxHash))?.GetInteroperable<RequestState>().request;
        }

        private bool SubmitResponse(ApplicationEngine engine, OracleResponse response)
        {
            StoreView snapshot = engine.Snapshot;
            StorageKey key_request = CreateRequestKey(response.RequestTxHash);
            RequestState request = snapshot.Storages.TryGet(key_request).GetInteroperable<RequestState>();
            if (request is null) return false;
            if (request.status != RequestStatus.REQUEST) return false;
            if (request.request.ValidHeight < snapshot.Height) return false;
            request.status = RequestStatus.READY;
            return true;
        }

        [ContractMethod(0_01000000, ContractParameterType.Boolean, CallFlags.All, ParameterTypes = new[] { ContractParameterType.Hash256 }, ParameterNames = new[] { "RequestTxHash" })]
        public StackItem InvokeCallBackMethod(ApplicationEngine engine, Array args)
        {
            UInt160 oracleAddress = GetOracleMultiSigAddress(engine.Snapshot);
            if (!engine.CheckWitnessInternal(oracleAddress)) return false;

            if (!(engine.ScriptContainer is Transaction)) return false;
            Transaction tx = (Transaction)engine.ScriptContainer;
            TransactionAttribute attribute = tx.Attributes.Where(p => p is OracleResponseAttribute).FirstOrDefault();
            if (attribute is null) return false;
            OracleResponse response = ((OracleResponseAttribute)attribute).response;
            UInt256 RequestTxHash = response.RequestTxHash;
            StorageKey key_request = CreateRequestKey(RequestTxHash);
            RequestState request = engine.Snapshot.Storages.TryGet(key_request)?.GetInteroperable<RequestState>();
            if (request is null) return false;
            if (request.status != RequestStatus.READY) return false;

            byte[] data = response.Result;
            long GasLeftBeforeCallBack = engine.GasLeft;
            long FilterCost = response.FilterCost;
            engine.CallContractEx(NativeContract.Oracle.Hash, "refund", new Array() { RequestTxHash.ToArray() , GasLeftBeforeCallBack , FilterCost }, CallFlags.All);
            engine.CallContractEx(request.request.CallBackContractHash, request.request.CallBackMethod, new Array() { data}, CallFlags.All);
            return true;
        }

        [ContractMethod(0_01000000, ContractParameterType.Boolean, CallFlags.All, ParameterTypes = new[] { ContractParameterType.Hash256 }, ParameterNames = new[] { "RequestTxHash" })]
        public StackItem Refund(ApplicationEngine engine, Array args)
        {
            if (engine.CallingScriptHash != NativeContract.Oracle.Hash) return false;
            UInt256 RequestTxHash = args[0].GetSpan().AsSerializable<UInt256>();
            long GasLeftBeforeCallBack = (long)args[1].GetBigInteger() ;
            long FilterCost = (long)args[2].GetBigInteger();
            long GasLeftAfterCallBack = engine.GasLeft;
            long CallBackCost = GasLeftBeforeCallBack - GasLeftAfterCallBack;
            StorageKey key_request = CreateRequestKey(RequestTxHash);
            RequestState request = engine.Snapshot.Storages.TryGet(key_request)?.GetInteroperable<RequestState>();
            UInt160[] oracleNodes = GetOracleValidators(engine.Snapshot).Select(p => Contract.CreateSignatureContract(p).ScriptHash).ToArray();
            long refundGas = request.request.OracleFee - (FilterCost + GetPerRequestFee(engine.Snapshot)) * oracleNodes.Length - CallBackCost;

            Transaction tx = engine.GetTransaction(RequestTxHash);
            UInt160 account = tx.Sender;
            request = engine.Snapshot.Storages.GetAndChange(key_request).GetInteroperable<RequestState>();
            request.status = RequestStatus.SUCCESSED;
            if(refundGas>0) NativeContract.GAS.Mint(engine, account, refundGas);
            return true;
        }


        protected override bool OnPersist(ApplicationEngine engine)
        {
            if (!base.OnPersist(engine)) return false;
            foreach (Transaction tx in engine.Snapshot.PersistingBlock.Transactions)
            {
                TransactionAttribute attribute = tx.Attributes.Where(p => p is OracleResponseAttribute).FirstOrDefault();
                if (attribute is null) continue;
                if(tx.Sender != GetOracleMultiSigAddress(engine.Snapshot))return false;
                OracleResponse response = ((OracleResponseAttribute)attribute).response;
                if (SubmitResponse(engine, response)) {
                    UInt160[] oracleNodes = GetOracleValidators(engine.Snapshot).Select(p=>Contract.CreateSignatureContract(p).ScriptHash).ToArray();
                    foreach (UInt160 account in oracleNodes)
                    {
                        NativeContract.GAS.Mint(engine, account, response.FilterCost+GetPerRequestFee(engine.Snapshot));
                    }
                    StorageKey key_request = CreateRequestKey(response.RequestTxHash);
                    RequestState request = engine.Snapshot.Storages.TryGet(key_request)?.GetInteroperable<RequestState>();
                    long CallBackFee = request.request.OracleFee - (response.FilterCost + GetPerRequestFee(engine.Snapshot)) * oracleNodes.Length;
                    if(CallBackFee>0) NativeContract.GAS.Mint(engine, tx.Sender, CallBackFee);
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
