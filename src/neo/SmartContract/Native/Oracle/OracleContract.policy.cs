using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Ledger;
using Neo.Persistence;
using Neo.SmartContract.Native.Oracle;
using Neo.VM;
using Neo.VM.Types;
using System;
using System.Linq;
using System.Text;

namespace Neo.SmartContract.Native
{
    public partial class OracleContract : NativeContract
    {
        internal const byte Prefix_Validator = 24;
        internal const byte Prefix_Config = 17;
        internal const byte Prefix_PerRequestFee = 21;
        internal const byte Prefix_ValidHeight = 19;

        [ContractMethod(0_01000000, CallFlags.AllowStates)]
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

        [ContractMethod(0_01000000, CallFlags.AllowStates)]
        public int GetOracleValidatorsCount(StoreView snapshot)
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

        [ContractMethod(0_03000000, CallFlags.AllowModifyStates)]
        private bool SetConfig(ApplicationEngine engine, VM.Types.Array args)
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

        [ContractMethod(0_01000000, CallFlags.AllowStates)]
        private StackItem GetConfig(ApplicationEngine engine, VM.Types.Array args)
        {
            return GetConfig(engine.Snapshot, args[0].GetString())?.ToStackItem(engine.ReferenceCounter);
        }

        public IInteroperable GetConfig(StoreView snapshot, string protocolType)
        {
            switch (protocolType)
            {
                case HttpConfig.Key:
                    return snapshot.Storages.TryGet(CreateStorageKey(Prefix_Config, Encoding.UTF8.GetBytes(HttpConfig.Key)))?.GetInteroperable<HttpConfig>();
                default:
                    return null;
            }
        }

        [ContractMethod(0_03000000, CallFlags.AllowModifyStates)]
        public bool SetPerRequestFee(ApplicationEngine engine, int perRequestFee)
        {
            StoreView snapshot = engine.Snapshot;
            UInt160 account = GetOracleMultiSigAddress(snapshot);
            if (!engine.CheckWitnessInternal(account)) return false;
            if (perRequestFee <= 0) return false;
            StorageItem storage = snapshot.Storages.GetAndChange(CreateStorageKey(Prefix_PerRequestFee));
            storage.Value = BitConverter.GetBytes(perRequestFee);
            return true;
        }

        [ContractMethod(0_01000000, CallFlags.AllowStates)]
        public long GetPerRequestFee(StoreView snapshot)
        {
            StorageItem storage = snapshot.Storages.TryGet(CreateStorageKey(Prefix_PerRequestFee));
            if (storage is null) return 0;
            return BitConverter.ToInt64(storage.Value);
        }

        [ContractMethod(0_03000000, CallFlags.AllowModifyStates)]
        public bool SetValidHeight(ApplicationEngine engine, uint ValidHeight)
        {
            StoreView snapshot = engine.Snapshot;
            UInt160 account = GetOracleMultiSigAddress(snapshot);
            if (!engine.CheckWitnessInternal(account)) return false;
            StorageItem storage = snapshot.Storages.GetAndChange(CreateStorageKey(Prefix_ValidHeight));
            storage.Value = BitConverter.GetBytes(ValidHeight);
            return true;
        }

        [ContractMethod(0_01000000, CallFlags.AllowStates)]
        public uint GetValidHeight(StoreView snapshot)
        {
            StorageItem storage = snapshot.Storages.TryGet(CreateStorageKey(Prefix_ValidHeight));
            if (storage is null) return 0;
            return BitConverter.ToUInt32(storage.Value);
        }
    }
}
