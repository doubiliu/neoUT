using Neo.Cryptography;
using Neo.IO;
using Neo.IO.Json;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Neo.Network.P2P.Payloads
{
    public class Transaction : IEquatable<Transaction>, IInventory
    {
        public const int MaxTransactionSize = 10240000;
        public const uint MaxValidUntilBlockIncrement = 2102400;
        /// <summary>
        /// Maximum number of attributes that can be contained within a transaction
        /// </summary>
        private const int MaxTransactionAttributes = 16;
        /// <summary>
        /// Maximum number of cosigners that can be contained within a transaction
        /// </summary>
        private const int MaxCosigners = 16;

        public byte Version;
        public uint Nonce;
        public UInt160 Sender;
        /// <summary>
        /// Distributed to NEO holders.
        /// </summary>
        public long SystemFee;
        /// <summary>
        /// Distributed to consensus nodes.
        /// </summary>
        public long NetworkFee;
        public uint ValidUntilBlock;
        public TransactionAttribute[] Attributes;
        public Cosigner[] Cosigners { get; set; }
        public byte[] Script;
        public Witness[] Witnesses { get; set; }
        public static System.Diagnostics.Stopwatch stopwatch1 = new System.Diagnostics.Stopwatch();
        public static System.Diagnostics.Stopwatch stopwatch2 = new System.Diagnostics.Stopwatch();
        public static System.Diagnostics.Stopwatch stopwatch3 = new System.Diagnostics.Stopwatch();
        public static System.Diagnostics.Stopwatch stopwatch4 = new System.Diagnostics.Stopwatch();
        public static System.Diagnostics.Stopwatch stopwatch5 = new System.Diagnostics.Stopwatch();
        public static System.Diagnostics.Stopwatch stopwatch6 = new System.Diagnostics.Stopwatch();
        public static System.Diagnostics.Stopwatch stopwatch7 = new System.Diagnostics.Stopwatch();
        public static System.Diagnostics.Stopwatch stopwatch8 = new System.Diagnostics.Stopwatch();
        public static int times = 0;
        public static int times2 = 0;

        /// <summary>
        /// The <c>NetworkFee</c> for the transaction divided by its <c>Size</c>.
        /// <para>Note that this property must be used with care. Getting the value of this property multiple times will return the same result. The value of this property can only be obtained after the transaction has been completely built (no longer modified).</para>
        /// </summary>
        public long FeePerByte => NetworkFee / Size;

        private UInt256 _hash = null;
        public UInt256 Hash
        {
            get
            {
                if (_hash == null)
                {
                    _hash = new UInt256(Crypto.Default.Hash256(this.GetHashData()));
                }
                return _hash;
            }
        }

        InventoryType IInventory.InventoryType => InventoryType.TX;

        public const int HeaderSize =
            sizeof(byte) +  //Version
            sizeof(uint) +  //Nonce
            20 +            //Sender
            sizeof(long) +  //Gas
            sizeof(long) +  //NetworkFee
            sizeof(uint);   //ValidUntilBlock

        public int Size => HeaderSize +
            Attributes.GetVarSize() +   //Attributes
            Cosigners.GetVarSize() +    //Cosigners
            Script.GetVarSize() +       //Script
            Witnesses.GetVarSize();     //Witnesses

        void ISerializable.Deserialize(BinaryReader reader)
        {
            DeserializeUnsigned(reader);
            Witnesses = reader.ReadSerializableArray<Witness>();
        }

        internal static Transaction DeserializeFrom(BinaryReader reader)
        {
            Transaction transaction = new Transaction();

            transaction.DeserializeUnsigned(reader);
            transaction.Witnesses = reader.ReadSerializableArray<Witness>();
            return transaction;
        }

        public void DeserializeUnsigned(BinaryReader reader)
        {
            Version = reader.ReadByte();
            if (Version > 0) throw new FormatException();
            Nonce = reader.ReadUInt32();
            Sender = reader.ReadSerializable<UInt160>();
            SystemFee = reader.ReadInt64();
            if (SystemFee < 0) throw new FormatException();
            if (SystemFee % NativeContract.GAS.Factor != 0) throw new FormatException();
            NetworkFee = reader.ReadInt64();
            if (NetworkFee < 0) throw new FormatException();
            if (SystemFee + NetworkFee < SystemFee) throw new FormatException();
            ValidUntilBlock = reader.ReadUInt32();
            Attributes = reader.ReadSerializableArray<TransactionAttribute>(MaxTransactionAttributes);
            Cosigners = reader.ReadSerializableArray<Cosigner>(MaxCosigners);
            if (Cosigners.Select(u => u.Account).Distinct().Count() != Cosigners.Length) throw new FormatException();
            Script = reader.ReadVarBytes(ushort.MaxValue);
            if (Script.Length == 0) throw new FormatException();
        }

        public bool Equals(Transaction other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Hash.Equals(other.Hash);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Transaction);
        }

        public override int GetHashCode()
        {
            return Hash.GetHashCode();
        }

        public UInt160[] GetScriptHashesForVerifying(Snapshot snapshot)
        {
            /*var hashes = new HashSet<UInt160> { Sender };
            hashes.UnionWith(Cosigners.Select(p => p.Account));
            return hashes.OrderBy(p => p).ToArray();*/
            var hashes = new HashSet<UInt160>(Cosigners.Select(p => p.Account));
            hashes.UnionWith(new HashSet<UInt160> { Sender });
            return hashes.OrderBy(p => p).ToArray();
        }

        public virtual bool Reverify(Snapshot snapshot, IEnumerable<Transaction> mempool)
        {
            times2++;
            try
            {
                stopwatch4.Start();
                if (ValidUntilBlock <= snapshot.Height || ValidUntilBlock > snapshot.Height + MaxValidUntilBlockIncrement)
                    return false;
                if (NativeContract.Policy.GetBlockedAccounts(snapshot).Intersect(GetScriptHashesForVerifying(snapshot)).Count() > 0)
                    return false;
                stopwatch4.Stop();
                stopwatch5.Start();
                BigInteger balance = NativeContract.GAS.BalanceOf(snapshot, Sender);
                BigInteger fee = SystemFee + NetworkFee;
                if (balance < fee) return false;
                stopwatch5.Stop();
                stopwatch6.Start();
                fee += mempool.Where(p => p != this && p.Sender.Equals(Sender)).Select(p => (BigInteger)(p.SystemFee + p.NetworkFee)).Sum();
                if (balance < fee) return false;
                stopwatch6.Stop();
                stopwatch7.Start();
                UInt160[] hashes = GetScriptHashesForVerifying(snapshot);
                stopwatch7.Stop();
                stopwatch8.Start();
                for (int i = 0; i < hashes.Length; i++)
                {
                    if (Witnesses[i].VerificationScript.Length > 0) continue;
                    if (snapshot.Contracts.TryGet(hashes[i]) is null) return false;
                }
                stopwatch8.Stop();
                return true;
            }
            finally
            {
                stopwatch4.Stop();
                stopwatch5.Stop();
                stopwatch6.Stop();
                stopwatch7.Stop();
                stopwatch8.Stop();
            }
        }

        void ISerializable.Serialize(BinaryWriter writer)
        {
            ((IVerifiable)this).SerializeUnsigned(writer);
            writer.Write(Witnesses);
        }

        void IVerifiable.SerializeUnsigned(BinaryWriter writer)
        {
            writer.Write(Version);
            writer.Write(Nonce);
            writer.Write(Sender);
            writer.Write(SystemFee);
            writer.Write(NetworkFee);
            writer.Write(ValidUntilBlock);
            writer.Write(Attributes);
            writer.Write(Cosigners);
            writer.WriteVarBytes(Script);
        }

        public JObject ToJson()
        {
            JObject json = new JObject();
            json["hash"] = Hash.ToString();
            json["size"] = Size;
            json["version"] = Version;
            json["nonce"] = Nonce;
            json["sender"] = Sender.ToAddress();
            json["sys_fee"] = SystemFee.ToString();
            json["net_fee"] = NetworkFee.ToString();
            json["valid_until_block"] = ValidUntilBlock;
            json["attributes"] = Attributes.Select(p => p.ToJson()).ToArray();
            json["cosigners"] = Cosigners.Select(p => p.ToJson()).ToArray();
            json["script"] = Script.ToHexString();
            json["witnesses"] = Witnesses.Select(p => p.ToJson()).ToArray();
            return json;
        }

        public static Transaction FromJson(JObject json)
        {
            Transaction tx = new Transaction();
            tx.Version = byte.Parse(json["version"].AsString());
            tx.Nonce = uint.Parse(json["nonce"].AsString());
            tx.Sender = json["sender"].AsString().ToScriptHash();
            tx.SystemFee = long.Parse(json["sys_fee"].AsString());
            tx.NetworkFee = long.Parse(json["net_fee"].AsString());
            tx.ValidUntilBlock = uint.Parse(json["valid_until_block"].AsString());
            tx.Attributes = ((JArray)json["attributes"]).Select(p => TransactionAttribute.FromJson(p)).ToArray();
            tx.Cosigners = ((JArray)json["cosigners"]).Select(p => Cosigner.FromJson(p)).ToArray();
            tx.Script = json["script"].AsString().HexToBytes();
            tx.Witnesses = ((JArray)json["witnesses"]).Select(p => Witness.FromJson(p)).ToArray();
            return tx;
        }

        bool IInventory.Verify(Snapshot snapshot)
        {
            return Verify(snapshot, Enumerable.Empty<Transaction>());
        }

        public virtual bool Verify(Snapshot snapshot, IEnumerable<Transaction> mempool)
        {
            try
            {
                times++;
                stopwatch1.Start();
                if (!Reverify(snapshot, mempool)) return false;
                stopwatch1.Stop();
                stopwatch2.Start();
                int size = Size;
                if (size > MaxTransactionSize) return false;
                long net_fee = NetworkFee - size * NativeContract.Policy.GetFeePerByte(snapshot);
                stopwatch2.Stop();
                stopwatch3.Start();
                if (net_fee < 0) return false;
                return this.VerifyWitnesses(snapshot, net_fee);
            }
            finally
            {
                stopwatch1.Stop();
                stopwatch2.Stop();
                stopwatch3.Stop();
            }
        }
    }
}
