using Neo.IO;
using Neo.Oracle;
using System.IO;
using System.Numerics;

namespace Neo.SmartContract.Native.Tokens
{
    public abstract class OracleRequest : ISerializable
    {
        public abstract OracleRequestType Type { get; }

        public virtual int Size => UInt256.Length + UInt160.Length + CallBackMethod.GetVarSize() + sizeof(uint) + sizeof(long) + sizeof(long) + sizeof(long);

        public UInt256 RequestTxHash;

        public OracleFilter Filter;

        public UInt160 CallBackContractHash;

        public string CallBackMethod;

        public uint ValidHeight;

        public long OracleFee;

        public long CallBackFee;

        public long FilterFee;


        public virtual void Serialize(BinaryWriter writer)
        {
            writer.Write(RequestTxHash);
            writer.WriteVarBytes(Filter.ToArray());
            writer.Write(CallBackContractHash);
            writer.WriteVarString(CallBackMethod);
            writer.Write(ValidHeight);
            writer.Write(OracleFee);
            writer.Write(CallBackFee);
            writer.Write(FilterFee);
        }

        public virtual void Deserialize(BinaryReader reader)
        {
            RequestTxHash = new UInt256(reader.ReadBytes(UInt160.Length));
            Filter = reader.ReadVarBytes().AsSerializable<OracleFilter>();
            CallBackContractHash = new UInt160(reader.ReadBytes(UInt160.Length));
            CallBackMethod = reader.ReadVarString();
            ValidHeight = reader.ReadUInt32();
            OracleFee = reader.ReadInt64();
            CallBackFee = reader.ReadInt64();
            FilterFee = reader.ReadInt64();
        }
    }
}
