using Neo.VM;
using Neo.VM.Types;

namespace Neo.SmartContract.Native.Tokens
{
    public class RequestState : IInteroperable
    {
        public OracleRequest Request;

        public RequestStatusType Status;

        public virtual void FromStackItem(StackItem stackItem)
        {
            Request = new OracleRequest();
            Request.FromStackItem(((Struct)stackItem)[0]);
            Status = (RequestStatusType)((Struct)stackItem)[1].GetSpan().ToArray()[0];
        }

        public virtual StackItem ToStackItem(ReferenceCounter referenceCounter)
        {
            Struct @struct = new Struct(referenceCounter);
            @struct.Add(Request.ToStackItem(referenceCounter));
            @struct.Add(new byte[] { (byte)Status });
            return @struct;
        }
    }
}
