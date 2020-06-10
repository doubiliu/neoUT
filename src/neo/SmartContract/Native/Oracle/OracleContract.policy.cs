#pragma warning disable IDE0051
#pragma warning disable IDE0060


using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Ledger;
using Neo.Persistence;
using Neo.SmartContract.Native.Oracle;
using Neo.VM;
using Neo.VM.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Neo.SmartContract.Native
{
    public partial class OracleContract : NativeContract
    {
        internal const byte Prefix_Validator = 24;
        internal const byte Prefix_Config = 25;
        internal const byte Prefix_PerRequestFee = 26;

        [ContractMethod(0_03000000, ContractParameterType.Boolean, CallFlags.AllowModifyStates, ParameterTypes = new[] { ContractParameterType.ByteArray, ContractParameterType.ByteArray }, ParameterNames = new[] { "consignorPubKey", "consigneePubKey" })]
        private StackItem DelegateOracleValidator(ApplicationEngine engine, VM.Types.Array args)
        {
            StoreView snapshot = engine.Snapshot;
            ECPoint consignorPubKey = args[0].GetSpan().AsSerializable<ECPoint>();
            ECPoint consigneePubKey = args[1].GetSpan().AsSerializable<ECPoint>();
            ECPoint[] cnPubKeys = NEO.GetValidators(snapshot);
            if (!cnPubKeys.Contains(consignorPubKey)) return false;
            UInt160 account = Contract.CreateSignatureRedeemScript(consignorPubKey).ToScriptHash();
            if (!engine.CheckWitnessInternal(account)) return false;
            StorageKey key = CreateStorageKey(Prefix_Validator, consignorPubKey);
            StorageItem item = snapshot.Storages.GetAndChange(key, () => new StorageItem());
            item.Value = consigneePubKey.ToArray();

            byte[] prefixKey = StorageKey.CreateSearchPrefix(Id, new[] { Prefix_Validator });
            List<ECPoint> delegatedOracleValidators = snapshot.Storages.Find(prefixKey).Select(p =>
              (
                  p.Key.Key.AsSerializable<ECPoint>(1)
              )).ToList();
            foreach (var validator in delegatedOracleValidators)
            {
                if (!cnPubKeys.Contains(validator))
                {
                    snapshot.Storages.Delete(CreateStorageKey(Prefix_Validator, validator));
                }
            }
            return true;
        }

        [ContractMethod(0_01000000, ContractParameterType.Array, CallFlags.AllowStates)]
        private StackItem GetOracleValidators(ApplicationEngine engine, VM.Types.Array args)
        {
            return new VM.Types.Array(engine.ReferenceCounter, GetOracleValidators(engine.Snapshot).Select(p => (StackItem)p.ToArray()));
        }

        public ECPoint[] GetOracleValidators(StoreView snapshot)
        {
            ECPoint[] cnPubKeys = NEO.GetValidators(snapshot);
            ECPoint[] oraclePubKeys = new ECPoint[cnPubKeys.Length];
            System.Array.Copy(cnPubKeys, oraclePubKeys, cnPubKeys.Length);
            for (int index = 0; index < oraclePubKeys.Length; index++)
            {
                var oraclePubKey = oraclePubKeys[index];
                StorageKey key = CreateStorageKey(Prefix_Validator, oraclePubKey);
                ECPoint delegatePubKey = snapshot.Storages.TryGet(key)?.Value.AsSerializable<ECPoint>();
                if (delegatePubKey != null) { oraclePubKeys[index] = delegatePubKey; }
            }
            return oraclePubKeys.Distinct().ToArray();
        }

        [ContractMethod(0_01000000, ContractParameterType.Integer, CallFlags.AllowStates)]
        private StackItem GetOracleValidatorsCount(ApplicationEngine engine, VM.Types.Array args)
        {
            return GetOracleValidatorsCount(engine.Snapshot);
        }

        /// <returns>The number of authorized Oracle validator</returns>
        public BigInteger GetOracleValidatorsCount(StoreView snapshot)
        {
            return GetOracleValidators(snapshot).Length;
        }

        public Contract GetOracleMultiSigContract(StoreView snapshot)
        {
            ECPoint[] oracleValidators = GetOracleValidators(snapshot);
            return Contract.CreateMultiSigContract(oracleValidators.Length - (oracleValidators.Length - 1) / 3, oracleValidators);
        }

        public UInt160 GetOracleMultiSigAddress(StoreView snapshot)
        {
            return GetOracleMultiSigContract(snapshot).ScriptHash;
        }

        [ContractMethod(0_03000000, ContractParameterType.Boolean, CallFlags.AllowModifyStates, ParameterTypes = new[] { ContractParameterType.String, ContractParameterType.ByteArray }, ParameterNames = new[] { "configKey", "configValue" })]
        private StackItem SetConfig(ApplicationEngine engine, VM.Types.Array args)
        {
            StoreView snapshot = engine.Snapshot;
            UInt160 account = GetOracleMultiSigAddress(snapshot);
            if (!engine.CheckWitnessInternal(account)) return false;

            switch (args[0].GetString())
            {
                case HttpConfig.Key:
                    {
                        var newCfg = new HttpConfig();
                        newCfg.FromStackItem(args[1]);

                        StorageItem storage = snapshot.Storages.GetAndChange(CreateStorageKey(Prefix_Config, Encoding.UTF8.GetBytes(HttpConfig.Key)));
                        var config = storage.GetInteroperable<HttpConfig>();
                        config.TimeOut = newCfg.TimeOut;
                        return true;
                    }
            }
            return false;
        }

        [ContractMethod(0_01000000, ContractParameterType.Array, CallFlags.AllowStates, ParameterTypes = new[] { ContractParameterType.String }, ParameterNames = new[] { "configKey" })]
        private StackItem GetConfig(ApplicationEngine engine, VM.Types.Array args)
        {
            return GetConfig(engine.Snapshot, args[0].GetString())?.ToStackItem(engine.ReferenceCounter);
        }

        public IInteroperable GetConfig(StoreView snapshot, string protocolType) {
            switch (protocolType)
            {
                case HttpConfig.Key:
                    return snapshot.Storages.TryGet(CreateStorageKey(Prefix_Config, Encoding.UTF8.GetBytes(HttpConfig.Key)))?.GetInteroperable<HttpConfig>();
                default:
                    return null;
            }
        }

        [ContractMethod(0_03000000, ContractParameterType.Boolean, CallFlags.AllowModifyStates, ParameterTypes = new[] { ContractParameterType.Integer }, ParameterNames = new[] { "fee" })]
        private StackItem SetPerRequestFee(ApplicationEngine engine, VM.Types.Array args)
        {
            StoreView snapshot = engine.Snapshot;
            UInt160 account = GetOracleMultiSigAddress(snapshot);
            if (!engine.CheckWitnessInternal(account)) return false;
            int perRequestFee = (int)args[0].GetBigInteger();
            if (perRequestFee <= 0) return false;
            StorageItem storage = snapshot.Storages.GetAndChange(CreateStorageKey(Prefix_PerRequestFee));
            storage.Value = BitConverter.GetBytes(perRequestFee);
            return true;
        }

        [ContractMethod(0_01000000, ContractParameterType.Integer, requiredCallFlags: CallFlags.AllowStates)]
        private StackItem GetPerRequestFee(ApplicationEngine engine, VM.Types.Array args)
        {
            return new Integer(GetPerRequestFee(engine.Snapshot));
        }

        public int GetPerRequestFee(StoreView snapshot)
        {
            StorageItem storage = snapshot.Storages.TryGet(CreateStorageKey(Prefix_PerRequestFee));
            if (storage is null) return 0;
            return BitConverter.ToInt32(storage.Value);
        }
    }
}
