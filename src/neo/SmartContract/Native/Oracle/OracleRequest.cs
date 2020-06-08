using Neo.IO;
using Neo.Oracle;
using System.IO;
using System.Numerics;

namespace Neo.SmartContract.Native.Tokens
{
    public abstract class OracleRequest : ISerializable
    {
        public abstract OracleRequestType Type { get; }

        public virtual int Size => UInt256.Length + UInt160.Length + CallBackMethod.GetVarSize() + sizeof(uint) + OracleFee.ToByteArray().GetVarSize() + CallBackFee.ToByteArray().GetVarSize();

        public UInt256 RequestTxHash;

        public OracleFilter Filter;

        public UInt160 CallBackContractHash;

        public string CallBackMethod;

        public uint ValidHeight;

        public BigInteger OracleFee;

        public BigInteger CallBackFee;

        public BigInteger FilterFee;


        public virtual void Serialize(BinaryWriter writer)
        {
            writer.Write(RequestTxHash);
            writer.WriteVarBytes(Filter.ToArray());
            writer.Write(CallBackContractHash);
            writer.WriteVarString(CallBackMethod);
            writer.Write(ValidHeight);
            writer.WriteVarBytes(OracleFee.ToByteArray());
            writer.WriteVarBytes(CallBackFee.ToByteArray());
            writer.WriteVarBytes(FilterFee.ToByteArray());
        }

        public virtual void Deserialize(BinaryReader reader)
        {
            RequestTxHash = new UInt256(reader.ReadBytes(UInt160.Length));
            Filter = reader.ReadVarBytes().AsSerializable<OracleFilter>();
            CallBackContractHash = new UInt160(reader.ReadBytes(UInt160.Length));
            CallBackMethod = reader.ReadVarString();
            ValidHeight = reader.ReadUInt32();
            OracleFee = new BigInteger(reader.ReadVarBytes());
            CallBackFee = new BigInteger(reader.ReadVarBytes());
        }
    }
}
