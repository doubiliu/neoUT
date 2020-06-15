using Neo.IO;
using Neo.Oracle;
using Neo.VM;
using Neo.VM.Types;
using System;
using System.IO;
using System.Numerics;

namespace Neo.SmartContract.Native.Tokens
{
    public abstract class OracleRequest : IInteroperable,ISerializable
    {
        public abstract OracleRequestType Type { get; }

        public virtual int Size => UInt256.Length + UInt160.Length + CallBackMethod.GetVarSize() + sizeof(uint) + sizeof(long) + sizeof(long) + sizeof(long);

        public UInt256 RequestTxHash;

        public OracleFilter Filter;

        public UInt160 CallBackContractHash;

        public string CallBackMethod;

        public uint ValidHeight;

        public long OracleFee;


        public virtual void Serialize(BinaryWriter writer)
        {
            writer.Write(RequestTxHash);
            writer.WriteVarBytes(Filter.ToArray());
            writer.Write(CallBackContractHash);
            writer.WriteVarString(CallBackMethod);
            writer.Write(ValidHeight);
            writer.Write(OracleFee);
        }

        public virtual void Deserialize(BinaryReader reader)
        {
            RequestTxHash = new UInt256(reader.ReadBytes(UInt160.Length));
            Filter = reader.ReadVarBytes().AsSerializable<OracleFilter>();
            CallBackContractHash = new UInt160(reader.ReadBytes(UInt160.Length));
            CallBackMethod = reader.ReadVarString();
            ValidHeight = reader.ReadUInt32();
            OracleFee = reader.ReadInt64();
        }

        public virtual void FromStackItem(StackItem stackItem)
        {
            Struct @struct = (Struct)stackItem;
            RequestTxHash = @struct[0].GetSpan().AsSerializable<UInt256>();
            Filter= @struct[1].GetSpan().AsSerializable<OracleFilter>();
            CallBackContractHash = @struct[2].GetSpan().AsSerializable<UInt160>();
            CallBackMethod = @struct[3].GetString();
            ValidHeight = (uint)@struct[4].GetBigInteger();
            OracleFee= (long)@struct[5].GetBigInteger();
        }

        public virtual StackItem ToStackItem(ReferenceCounter referenceCounter)
        {
            Struct @struct = new Struct(referenceCounter)
            { RequestTxHash.ToArray(),
              Filter.ToArray(),
              CallBackContractHash.ToArray(),
              CallBackMethod,
              ValidHeight,
              OracleFee
            };
            return @struct;
        }
    }
}
