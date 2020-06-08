using Neo.Cryptography;
using Neo.IO;
using System.IO;

namespace Neo.SmartContract.Native.Tokens
{
    public class OracleResponse : ISerializable
    {
        public int Size => UInt256.Length + sizeof(byte) + Result.GetVarSize();

        public UInt256 RequestTxHash { get; set; }

        public byte[] Result { get; set; }

        public bool Error => Result == null;

        public virtual void Serialize(BinaryWriter writer)
        {
            writer.Write(RequestTxHash);
            if (Result != null)
            {
                writer.Write((byte)0x01);
                writer.WriteVarBytes(Result);
            }
            else
            {
                writer.Write((byte)0x00);
            }
        }

        public virtual void Deserialize(BinaryReader reader)
        {
            RequestTxHash = new UInt256(reader.ReadBytes(UInt256.Length));
            if (reader.ReadByte() == 0x01)
            {
                Result = reader.ReadVarBytes(ushort.MaxValue);
            }
            else
            {
                Result = null;
            }
        }

        public static OracleResponse CreateError(UInt256 requestHash)
        {
            return CreateResult(requestHash, null);
        }

        public static OracleResponse CreateResult(UInt256 requestTxHash, byte[] result)
        {
            return new OracleResponse()
            {
                RequestTxHash = requestTxHash,
                Result = result
            };
        }

        private UInt160 _hash;
        public UInt160 Hash
        {
            get
            {
                if (_hash == null)
                {
                    _hash = new UInt160(Crypto.Hash160(this.ToArray()));
                }

                return _hash;
            }
        }
    }
}
