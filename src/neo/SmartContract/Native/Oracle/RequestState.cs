using Neo.VM;
using Neo.VM.Types;

namespace Neo.SmartContract.Native.Tokens
{
    public class RequestState : IInteroperable
    {
        public OracleRequest Request;

        public RequestStatusType status;//0x00 未提交完成 0x01 提交完成 0x02 已执行callback

        public virtual void FromStackItem(StackItem stackItem)
        {
            request = new OracleRequest();
            request.FromStackItem(((Struct)stackItem)[0]);
            status = (RequestStatusType)((Struct)stackItem)[1].GetSpan().ToArray()[0];
        }

        public virtual StackItem ToStackItem(ReferenceCounter referenceCounter)
        {
            Struct @struct = new Struct(referenceCounter);
            @struct.Add(request.ToStackItem(referenceCounter));
            @struct.Add(new byte[] { (byte)status });
            return @struct;
        }
    }
}
