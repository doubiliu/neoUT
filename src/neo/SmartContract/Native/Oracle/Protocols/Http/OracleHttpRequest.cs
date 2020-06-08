using Neo.IO;
using System.IO;

namespace Neo.SmartContract.Native.Tokens
{
    public class OracleHttpRequest : OracleRequest
    {
        public override OracleRequestType Type => OracleRequestType.HTTP;

        public override int Size => base.Size + sizeof(byte) + URL.GetVarSize();
        public HttpMethod Method { get; set; }

        public string URL { get; set; }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write((byte)Method);
            writer.WriteVarString(URL);
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            Method = (HttpMethod)reader.ReadByte();
            URL = reader.ReadVarString();
        }
    }
}
