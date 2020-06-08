using Neo.IO;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;
using Neo.VM.Types;
using System.IO;

namespace Neo.Oracle
{
    public class OracleFilter : ISerializable
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

        /// <summary>
        /// Filter response
        /// </summary>
        /// <param name="snapshot">Snapshot (CallEx will require the snapshot for get the script)</param>
        /// <param name="filter">Filter</param>
        /// <param name="input">Input</param>
        /// <param name="result">Result</param>
        /// <param name="gasCost">Gas cost</param>
        /// <returns>True if was filtered</returns>
        public static bool Filter(StoreView snapshot, OracleFilter filter, byte[] input, out byte[] result, long gasCost)
        {
            if (filter == null)
            {
                result = input;
                return true;
            }

            // Prepare the execution

            using ScriptBuilder script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Contract_CallEx, filter.ContractHash, filter.FilterMethod, new object[] { input, filter.FilterArgs }, (byte)CallFlags.None);

            // Execute

            using var engine = new ApplicationEngine(TriggerType.Application, null, snapshot, gasCost, false);
            engine.LoadScript(script.ToArray(), CallFlags.AllowCall);

            if (engine.Execute() != VMState.HALT || !engine.ResultStack.TryPop(out PrimitiveType ret))
            {
                result = null;
                gasCost = engine.GasConsumed;
                return false;
            }

            // Extract the filtered item

            result = ret.GetSpan().ToArray();
            gasCost = engine.GasConsumed;
            return true;
        }
    }
}
