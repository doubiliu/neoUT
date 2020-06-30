using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Ledger;
using Neo.Persistence;
using System;

namespace Neo.SmartContract.Native
{
    public partial class OracleContract : NativeContract
    {
        internal const byte Prefix_Validator = 37;
        internal const byte Prefix_RequestBaseFee = 13;
        internal const byte Prefix_RequestMaxValidHeight = 33;

        [ContractMethod(0_01000000, CallFlags.AllowStates)]
        public bool SetOracleValidators(ApplicationEngine engine, ECPoint[] validators)
        {
            UInt160 committeeAddress = NativeContract.NEO.GetCommitteeAddress(engine.Snapshot);
            if (!engine.CheckWitnessInternal(committeeAddress)) return false;
            StorageKey key = CreateStorageKey(Prefix_Validator);
            engine.Snapshot.Storages.GetAndChange(key, () => new StorageItem() { Value = validators.ToByteArray() });
            return true;
        }

        [ContractMethod(0_01000000, CallFlags.AllowStates)]
        public ECPoint[] GetOracleValidators(StoreView snapshot)
        {
            StorageKey key = CreateStorageKey(Prefix_Validator);
            StorageItem item = snapshot.Storages.TryGet(key);
            if (item is null) return NativeContract.NEO.GetCommittee(snapshot);
            return item.Value.AsSerializableArray<ECPoint>();
        }

        public UInt160 GetOracleMultiSigAddress(StoreView snapshot)
        {
            ECPoint[] oracleValidators = GetOracleValidators(snapshot);
            return Contract.CreateMultiSigContract(oracleValidators.Length - (oracleValidators.Length - 1) / 3, oracleValidators).ScriptHash;
        }

        [ContractMethod(0_03000000, CallFlags.AllowModifyStates)]
        public bool SetRequestBaseFee(ApplicationEngine engine, long requestBaseFee)
        {
            UInt160 account = GetOracleMultiSigAddress(engine.Snapshot);
            if (!engine.CheckWitnessInternal(account)) return false;
            if (requestBaseFee <= 0) return false;
            StorageItem storage = engine.Snapshot.Storages.GetAndChange(CreateStorageKey(Prefix_RequestBaseFee), () => new StorageItem());
            storage.Value = BitConverter.GetBytes(requestBaseFee);
            return true;
        }

        [ContractMethod(0_01000000, CallFlags.AllowStates)]
        public long GetRequestBaseFee(StoreView snapshot)
        {
            StorageItem storage = snapshot.Storages.TryGet(CreateStorageKey(Prefix_RequestBaseFee));
            if (storage is null) return 0;
            return BitConverter.ToInt64(storage.Value);
        }

        [ContractMethod(0_03000000, CallFlags.AllowModifyStates)]
        public bool SetRequestMaxValidHeight(ApplicationEngine engine, uint ValidHeight)
        {
            UInt160 account = GetOracleMultiSigAddress(engine.Snapshot);
            if (!engine.CheckWitnessInternal(account)) return false;
            StorageItem storage = engine.Snapshot.Storages.GetAndChange(CreateStorageKey(Prefix_RequestMaxValidHeight), () => new StorageItem());
            storage.Value = BitConverter.GetBytes(ValidHeight);
            return true;
        }

        [ContractMethod(0_01000000, CallFlags.AllowStates)]
        public uint GetRequestMaxValidHeight(StoreView snapshot)
        {
            StorageItem storage = snapshot.Storages.TryGet(CreateStorageKey(Prefix_RequestMaxValidHeight));
            if (storage is null) return 0;
            return BitConverter.ToUInt32(storage.Value);
        }
    }
}
