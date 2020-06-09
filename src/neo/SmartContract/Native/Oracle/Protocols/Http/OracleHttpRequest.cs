using Neo.IO;
using Neo.VM;
using Neo.VM.Types;
using System;
using System.IO;

namespace Neo.SmartContract.Native.Tokens
{
    public class OracleHttpRequest : OracleRequest
    {
        public override OracleRequestType Type => OracleRequestType.HTTP;

        public override int Size => base.Size + sizeof(byte) + URL.ToString().GetVarSize();
        public HttpMethod Method { get; set; }

        public Uri URL { get; set; }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write((byte)Method);
            writer.WriteVarString(URL.ToString());
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            Method = (HttpMethod)reader.ReadByte();
            URL = new Uri(reader.ReadVarString());
        }

        public override void FromStackItem(StackItem stackItem)
        {
            base.FromStackItem(stackItem);
            Method = (HttpMethod)((Struct)stackItem)[8].GetSpan().ToArray()[8];
            URL = new Uri(((Struct)stackItem)[9].GetString());
        }

        public override StackItem ToStackItem(ReferenceCounter referenceCounter)
        {
            Struct @struct=(Struct)base.ToStackItem(referenceCounter);
            @struct.Add(new byte[] { (byte)Method });
            @struct.Add(URL.ToString());
            return @struct;
        }
    }
}
