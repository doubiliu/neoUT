using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using System;
using System.IO;
using System.Linq;

namespace Neo.Network.P2P.Payloads
{
    public class OraclePayload : IInventory
    {
        private const long MaxWitnessGas = 0_02000000;

        public ECPoint OraclePub;

        public UInt256 TransactionRequestHash;

        public UInt256 TransactionResponseHash;

        private byte[] _signature;
        public byte[] Signature
        {
            get => _signature;
            set
            {
                if (value.Length != 64) throw new ArgumentException();
                _signature = value;
            }
        }

        public Witness Witness;

        public int Size =>
            Signature.GetVarSize() + // Signature
            OraclePub.Size +         // Oracle Public key
            1 +                      // The length of witnesses, currently it fixed 1.
            Witness.Size +           // Witness
            UInt256.Length +         //RequestTx Hash
            UInt256.Length;          //ResponseTx Hash

        public UInt256 Hash => new UInt256(Crypto.Hash256(this.GetHashData()));

        Witness[] IVerifiable.Witnesses
        {
            get => new[] { Witness };
            set
            {
                if (value.Length != 1) throw new ArgumentException();
                Witness = value[0];
            }
        }

        public InventoryType InventoryType => InventoryType.Oracle;

        void ISerializable.Deserialize(BinaryReader reader)
        {
            ((IVerifiable)this).DeserializeUnsigned(reader);

            var witness = reader.ReadSerializableArray<Witness>(1);
            if (witness.Length != 1) throw new FormatException();
            Witness = witness[0];
        }

        void IVerifiable.DeserializeUnsigned(BinaryReader reader)
        {
            OraclePub = reader.ReadSerializable<ECPoint>();
            TransactionRequestHash = reader.ReadSerializable<UInt256>();
            TransactionResponseHash = reader.ReadSerializable<UInt256>();
            Signature = reader.ReadBytes(64);
        }

        public virtual void Serialize(BinaryWriter writer)
        {
            ((IVerifiable)this).SerializeUnsigned(writer);
            writer.Write(new Witness[] { Witness });
        }

        void IVerifiable.SerializeUnsigned(BinaryWriter writer)
        {
            writer.Write(OraclePub);
            writer.Write(TransactionRequestHash);
            writer.Write(TransactionResponseHash);
            writer.Write(Signature);
        }

        UInt160[] IVerifiable.GetScriptHashesForVerifying(StoreView snapshot)
        {
            return new[] { Contract.CreateSignatureRedeemScript(OraclePub).ToScriptHash() };
        }

        public bool Verify(StoreView snapshot)
        {
            ECPoint[] validators = NativeContract.Oracle.GetOracleValidators(snapshot);
            if (!validators.Any(u => u.Equals(OraclePub)))
                return false;
            return this.VerifyWitnesses(snapshot, MaxWitnessGas);
        }
    }
}
