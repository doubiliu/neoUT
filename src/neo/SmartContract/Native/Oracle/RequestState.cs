using Neo.VM;
using Neo.VM.Types;

namespace Neo.SmartContract.Native.Tokens
{
    public class RequestState : IInteroperable
    {
        public OracleRequest request;

        public RequestStatusType status;//0x00 未提交完成 0x01 提交完成 0x02 已执行callback

        public virtual void FromStackItem(StackItem stackItem)
        {
            OracleRequestType type = (OracleRequestType)((Struct)stackItem)[0].GetSpan().ToArray()[0];
            switch (type)
            {
                case OracleRequestType.HTTP:
                    request = new OracleRequest();
                    request.FromStackItem(((Struct)stackItem)[1]);
                    break;
                default:
                    request = new OracleRequest();
                    request.FromStackItem(((Struct)stackItem)[1]);
                    break;
            }
            status = (RequestStatusType)((Struct)stackItem)[2].GetSpan().ToArray()[0];
        }

        public virtual StackItem ToStackItem(ReferenceCounter referenceCounter)
        {
            Struct @struct = new Struct(referenceCounter);
            if (request is OracleRequest)
            {
                @struct.Add(new byte[] { (byte)OracleRequestType.HTTP });
            }
            else
            {
                @struct.Add(new byte[] { (byte)OracleRequestType.HTTP });
            }
            @struct.Add(request.ToStackItem(referenceCounter));
            @struct.Add(new byte[] { (byte)status });
            return @struct;
        }
    }
}
