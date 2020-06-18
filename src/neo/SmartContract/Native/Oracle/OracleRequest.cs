using Neo.IO;
using Neo.VM;
using Neo.VM.Types;
using System;
using System.IO;
using System.Numerics;

namespace Neo.SmartContract.Native.Tokens
{
    public class OracleRequest : IInteroperable,ISerializable
    {
        public virtual int Size => UInt256.Length
         + UInt160.Length   //Timestamp
         + FilterMethod.GetVarSize()  //Timestamp
         + FilterArgs.GetVarSize()    // TODO add comments
         + UInt160.Length 
         + CallBackMethod.GetVarSize() 
         + sizeof(uint) 
         + sizeof(long) 
         + sizeof(long) 
         + sizeof(long)
         +URL.ToString().GetVarSize();

        public UInt256 RequestTxHash;

        public UInt160 FilterContractHash;

        public string FilterMethod;

        public string FilterArgs;

        public UInt160 CallBackContractHash;

        public string CallBackMethod;

        public uint ValidHeight;

        public long OracleFee;

        public Uri URL;


        public virtual void Serialize(BinaryWriter writer)
        {
            writer.Write(RequestTxHash);
            writer.Write(FilterContractHash);
            writer.WriteVarString(FilterMethod);
            writer.WriteVarString(FilterArgs);
            writer.Write(CallBackContractHash);
            writer.WriteVarString(CallBackMethod);
            writer.Write(ValidHeight);
            writer.Write(OracleFee);
            writer.WriteVarString(URL.ToString());
        }

        public virtual void Deserialize(BinaryReader reader)
        {
            RequestTxHash = new UInt256(reader.ReadBytes(UInt160.Length));
            FilterContractHash = new UInt160(reader.ReadBytes(UInt160.Length));
            FilterMethod = reader.ReadVarString();
            FilterArgs = reader.ReadVarString();
            CallBackContractHash = new UInt160(reader.ReadBytes(UInt160.Length));
            CallBackMethod = reader.ReadVarString();
            ValidHeight = reader.ReadUInt32();
            OracleFee = reader.ReadInt64();
            URL = new Uri(reader.ReadVarString());
        }

        public virtual void FromStackItem(StackItem stackItem)
        {
            Struct @struct = (Struct)stackItem;
            RequestTxHash = @struct[0].GetSpan().AsSerializable<UInt256>();
            FilterContractHash = @struct[1].GetSpan().AsSerializable<UInt160>();
            FilterMethod = @struct[2].GetString();
            FilterArgs = @struct[3].GetString();
            CallBackContractHash = @struct[4].GetSpan().AsSerializable<UInt160>();
            CallBackMethod = @struct[5].GetString();
            ValidHeight = (uint)@struct[6].GetBigInteger();
            OracleFee= (long)@struct[7].GetBigInteger();
            URL = new Uri(((Struct)stackItem)[8].GetString());
        }

        public virtual StackItem ToStackItem(ReferenceCounter referenceCounter)
        {
            Struct @struct = new Struct(referenceCounter)
            { 
                RequestTxHash.ToArray(),
              FilterContractHash.ToArray(),
              FilterMethod,
              FilterArgs,
              CallBackContractHash.ToArray(),
              CallBackMethod,
              ValidHeight,
              OracleFee,
              URL.ToString()
            };
            return @struct;
        }
    }
}
