using Neo.IO;
using System.IO;

namespace Neo.Network.P2P.Payloads
{
    public class OracleResponseAttribute : TransactionAttribute
    {
        public UInt256 RequestTxHash { get; set; }

        public byte[] Result { get; set; }

        public long FilterCost { get; set; }

        public bool Error => Result == null;

        public override int Size => base.Size + UInt256.Length + sizeof(long) + Result.GetVarSize();

        public override TransactionAttributeType Type => TransactionAttributeType.OracleResponse;

        public override bool AllowMultiple => false;

        protected override void DeserializeWithoutType(BinaryReader reader)
        {
            RequestTxHash = new UInt256(reader.ReadBytes(UInt256.Length));
            Result = reader.ReadByte() == 0x01 ? reader.ReadVarBytes(ushort.MaxValue) : null;
            FilterCost = reader.ReadInt64();
        }

        protected override void SerializeWithoutType(BinaryWriter writer)
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
            writer.Write(FilterCost);
        }
    }
}
