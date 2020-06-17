using Neo.IO;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;
using Neo.VM.Types;
using System.IO;

namespace Neo.Oracle
{
    public class OracleFilter : ISerializable,IInteroperable
    {
        /// <summary>
        /// Contract Hash
        /// </summary>
        public UInt160 ContractHash;

        /// <summary>
        /// You need a specific method for your filters
        /// </summary>
        public string FilterMethod;

        /// <summary>
        /// Filter args
        /// </summary>
        public string FilterArgs;

        public int Size => UInt160.Length + FilterMethod.GetVarSize() + FilterArgs.GetVarSize();

        public void Deserialize(BinaryReader reader)
        {
            ContractHash = reader.ReadSerializable<UInt160>();
            FilterMethod = reader.ReadVarString(ushort.MaxValue);
            FilterArgs = reader.ReadVarString(ushort.MaxValue);
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(ContractHash);
            writer.WriteVarString(FilterMethod);
            writer.WriteVarString(FilterArgs);
        }

        public static bool Filter(StoreView snapshot, OracleFilter filter, byte[] input, out byte[] result, out long gasCost)
        {
            if (filter == null)
            {
                result = input;
                gasCost = 0;
                return true;
            }
            // Prepare the execution
            using ScriptBuilder script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Contract_CallEx, filter.ContractHash, filter.FilterMethod, new object[] { input, filter.FilterArgs }, (byte)CallFlags.None);

            // Execute
            using var engine = new ApplicationEngine(TriggerType.Application, null, snapshot, 0, true);
            engine.LoadScript(script.ToArray(), CallFlags.AllowCall);

            if (engine.Execute() != VMState.HALT || !engine.ResultStack.TryPop(out PrimitiveType ret))
            {
                result = null;
                gasCost = engine.GasConsumed;
                return false;
            }
            result = ret.GetSpan().ToArray();
            gasCost = engine.GasConsumed;
            return true;
        }

        public void FromStackItem(StackItem stackItem)
        {
            Struct @struct = (Struct)stackItem;
            ContractHash = @struct[0].GetSpan().AsSerializable<UInt160>();
            FilterMethod = @struct[1].GetString();
            FilterArgs = @struct[2].GetString();
            throw new System.NotImplementedException();
        }

        public StackItem ToStackItem(ReferenceCounter referenceCounter)
        {
            return new Struct(referenceCounter) { ContractHash.ToArray(), FilterMethod, FilterArgs };
        }
    }
}
