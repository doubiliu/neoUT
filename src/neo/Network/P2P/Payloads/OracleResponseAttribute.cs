using Neo.IO;
using Neo.IO.Caching;
using Neo.IO.Json;
using Neo.SmartContract.Native.Tokens;
using System;
using System.IO;

namespace Neo.Network.P2P.Payloads
{
    public class OracleResponseAttribute : TransactionAttribute
    {
        public OracleResponse response;

        public override int Size => base.Size + UInt256.Length;

        public override TransactionAttributeType Type => TransactionAttributeType.OracleResponse;

        public override bool AllowMultiple => false;

        protected override void DeserializeWithoutType(BinaryReader reader)
        {
            response = reader.ReadSerializable<OracleResponse>();
        }

        protected override void SerializeWithoutType(BinaryWriter writer)
        {
            writer.Write(response);
        }
    }
}
