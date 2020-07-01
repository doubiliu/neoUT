using Neo.IO;
using Neo.VM;
using Neo.VM.Types;
using System.IO;

namespace Neo.SmartContract.Native.Tokens
{
    public class OracleRequest : IInteroperable, ISerializable
    {
        public UInt256 RequestTxHash;
        public string FilterPath;
        public UInt160 CallBackContract;
        public string CallBackMethod;
        public uint ValidHeight;
        public long OracleFee;
        public string Url;
        public RequestStatusType Status;

        public virtual int Size =>
            UInt256.Length +              // RequestTxHash
            FilterPath.GetVarSize() +     // FilterPath
            UInt160.Length +              // CallBackContract
            CallBackMethod.GetVarSize() + // CallBackMethod
            sizeof(uint) +                // ValidHeight
            sizeof(long) +                // OracleFee
            Url.GetVarSize() +            // Url
            sizeof(byte);                 // Status

        public virtual void Serialize(BinaryWriter writer)
        {
            writer.Write(RequestTxHash);
            writer.WriteVarString(FilterPath);
            writer.Write(CallBackContract);
            writer.WriteVarString(CallBackMethod);
            writer.Write(ValidHeight);
            writer.Write(OracleFee);
            writer.WriteVarString(Url);
            writer.Write((byte)Status);
        }

        public virtual void Deserialize(BinaryReader reader)
        {
            RequestTxHash = new UInt256(reader.ReadBytes(UInt160.Length));
            FilterPath = reader.ReadVarString();
            CallBackContract = new UInt160(reader.ReadBytes(UInt160.Length));
            CallBackMethod = reader.ReadVarString();
            ValidHeight = reader.ReadUInt32();
            OracleFee = reader.ReadInt64();
            Url = reader.ReadVarString();
            Status = (RequestStatusType)reader.ReadByte();
        }

        public virtual void FromStackItem(StackItem stackItem)
        {
            Struct @struct = (Struct)stackItem;
            RequestTxHash = @struct[0].GetSpan().AsSerializable<UInt256>();
            FilterPath = @struct[1].GetString();
            CallBackContract = @struct[2].GetSpan().AsSerializable<UInt160>();
            CallBackMethod = @struct[3].GetString();
            ValidHeight = (uint)@struct[4].GetInteger();
            OracleFee = (long)@struct[5].GetInteger();
            Url = ((Struct)stackItem)[6].GetString();
            Status = (RequestStatusType)@struct[7].GetSpan().ToArray()[0];
        }

        public virtual StackItem ToStackItem(ReferenceCounter referenceCounter)
        {
            return new Struct(referenceCounter)
            {
                RequestTxHash.ToArray(),
                FilterPath,
                CallBackContract.ToArray(),
                CallBackMethod,
                ValidHeight,
                OracleFee,
                Url,
                new byte[]{ (byte)Status }
            };
        }
    }
}
