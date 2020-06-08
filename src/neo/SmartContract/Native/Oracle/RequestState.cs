using Neo.IO;
using Neo.VM;
using Neo.VM.Types;
using System.Numerics;

namespace Neo.SmartContract.Native.Tokens
{
    public class RequestState : IInteroperable
    {
        public OracleRequest request;

        public BigInteger status;//0x00 未提交完成 0x01 提交完成 0x02 已执行callback


        public virtual void FromStackItem(StackItem stackItem)
        {
            OracleRequestType type = (OracleRequestType)((Struct)stackItem)[0].GetSpan().ToArray()[0];
            switch (type)
            {
                case OracleRequestType.HTTP:
                    request = ((Struct)stackItem)[1].GetSpan().ToArray().AsSerializable<OracleHttpRequest>();
                    break;
                default:
                    request = ((Struct)stackItem)[1].GetSpan().ToArray().AsSerializable<OracleHttpRequest>();
                    break;
            }
            status = ((Struct)stackItem)[2].GetBigInteger();
        }

        public virtual StackItem ToStackItem(ReferenceCounter referenceCounter)
        {
            Struct @struct = new Struct(referenceCounter);
            if (request is OracleHttpRequest)
            {
                @struct.Add(new byte[] { (byte)OracleRequestType.HTTP });
            }
            else
            {
                @struct.Add(new byte[] { (byte)OracleRequestType.HTTP });
            }
            @struct.Add(request.ToArray());
            @struct.Add(status);
            return @struct;
        }
    }
}
