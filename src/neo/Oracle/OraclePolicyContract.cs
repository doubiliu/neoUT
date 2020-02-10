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
using System.Numerics;
using Array = Neo.VM.Types.Array;

namespace Neo.SmartContract.Native
{
    public sealed class OraclePolicyContract : NativeContract
    {
        public override string ServiceName => "Neo.Native.Oracle.OraclePolicy";

        private const byte Prefix_Validator = 24;

        public OraclePolicyContract()
        {
            Manifest.Features = ContractFeatures.HasStorage;
        }

        [ContractMethod(5_00000000, ContractParameterType.Boolean, ParameterTypes = new[] { ContractParameterType.Hash160, ContractParameterType.Array }, ParameterNames = new[] { "account", "pubkeys" })]
        private bool RegisterOracleValidator(ApplicationEngine engine, Array args)
        {
            UInt160 account = new UInt160(args[0].GetSpan());
            if (!InteropService.Runtime.CheckWitnessInternal(engine, account)) return false;
            ECPoint[] pubkeys = ((Array)args[1]).Select(p => p.GetSpan().AsSerializable<ECPoint>()).ToArray();
            if (pubkeys.Length != 2) return false;
            StoreView snapshot = engine.Snapshot;
            StorageKey key = CreateStorageKey(Prefix_Validator, pubkeys[0]);
            if (snapshot.Storages.TryGet(key) != null) return false;
            snapshot.Storages.Add(key, new StorageItem
            {
                Value = pubkeys[1].ToArray()
            });
            return true;
        }

        [ContractMethod(5_00000000, ContractParameterType.Boolean, ParameterTypes = new[] { ContractParameterType.Hash160, ContractParameterType.Array }, ParameterNames = new[] { "account", "pubkeys" })]

        private bool ChangeOracleValidator(ApplicationEngine engine, Array args)
        {
            UInt160 account = new UInt160(args[0].GetSpan());
            if (!InteropService.Runtime.CheckWitnessInternal(engine, account)) return false;
            ECPoint[] pubkeys = ((Array)args[1]).Select(p => p.GetSpan().AsSerializable<ECPoint>()).ToArray();
            if (pubkeys.Length != 2) return false;
            StoreView snapshot = engine.Snapshot;
            StorageKey key = CreateStorageKey(Prefix_Validator, pubkeys[0]);
            StorageItem value = snapshot.Storages.GetAndChange(key);
            value.Value = pubkeys[1].ToArray();
            return true;
        }

        public ECPoint[] GetAuthorizedOracleValidators(StoreView snapshot)
        {
            ECPoint[] consensusPublicKey = PolicyContract.NEO.GetValidators(snapshot);
            IEnumerable<(ECPoint ConsensusPublicKey, ECPoint OraclePublicKey)> ps;
            return GetRegisteredOracleValidators(snapshot).Where(p=> consensusPublicKey.Contains(p.ConsensusPublicKey)).Select(p=>p.OraclePublicKey).ToArray();
        }

        public BigInteger GetAuthorizedOracleValidatorsCount(StoreView snapshot)
        {
            ECPoint[] consensusPublicKey = PolicyContract.NEO.GetValidators(snapshot);
            IEnumerable<(ECPoint ConsensusPublicKey, ECPoint OraclePublicKey)> ps;
            return GetRegisteredOracleValidators(snapshot).Where(p => consensusPublicKey.Contains(p.ConsensusPublicKey)).Select(p => p.OraclePublicKey).ToArray().Length;
        }

        internal IEnumerable<(ECPoint ConsensusPublicKey, ECPoint OraclePublicKey)> GetRegisteredOracleValidators(StoreView snapshot)
        {
            byte[] prefix_key = StorageKey.CreateSearchPrefix(Hash, new[] { Prefix_Validator });
            return snapshot.Storages.Find(prefix_key).Select(p =>
            (
                p.Key.Key.AsSerializable<ECPoint>(1),
                p.Value.Value.AsSerializable<ECPoint>(1)
            ));
        }
    }
}
