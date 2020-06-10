using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Ledger;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.SmartContract.Native.Tokens;
using Neo.UnitTests.Extensions;
using Neo.VM;
using Neo.VM.Types;
using System;
using VMArray = Neo.VM.Types.Array;

namespace Neo.UnitTests.SmartContract.Native
{
    [TestClass]
    public class UT_OracleContract
    {
        private static OracleContract test;

        [TestInitialize]
        public void TestSetup()
        {
            TestBlockchain.InitializeMockNeoSystem();
            test = NativeContract.Oracle;
        }

/*
        [TestMethod]
        public void TestOnPersistWithArgs()
        {
            var snapshot = Blockchain.Singleton.GetSnapshot();
            ApplicationEngine engine1 = new ApplicationEngine(TriggerType.Application, null, snapshot, 0);
            VMArray args = new VMArray();

            VM.Types.Boolean result1 = new VM.Types.Boolean(false);
            testNativeContract.TestOnPersist(engine1, args).Should().Be(result1);

            ApplicationEngine engine2 = new ApplicationEngine(TriggerType.System, null, snapshot, 0);
            VM.Types.Boolean result2 = new VM.Types.Boolean(true);
            testNativeContract.TestOnPersist(engine2, args).Should().Be(result2);
        }*/


        [TestMethod]
        public void Check_RegisterRequest()
        {
            var snapshot = Blockchain.Singleton.GetSnapshot();
            var ret_RegisterRequest = Check_RegisterRequest(snapshot, OracleRequestType.HTTP,new OracleHttpRequest() { });
            ret_RegisterRequest.Result.Should().Be(true);
            ret_RegisterRequest.State.Should().BeTrue();
        }

        internal static (bool State, StackItem Result) Check_RegisterRequest(StoreView snapshot, OracleRequestType requestType,OracleRequest request)
        {
            var engine = new ApplicationEngine(TriggerType.Application,
                new Nep5NativeContractExtensions.ManualWitness(UInt160.Zero), snapshot, 0, true);

            engine.LoadScript(NativeContract.Oracle.Script);

            var script = new ScriptBuilder();
            script.EmitPush(request.ToStackItem(new ReferenceCounter()));
            script.EmitPush((byte)requestType);
            script.EmitPush(2);
            script.Emit(OpCode.PACK);
            script.EmitPush("registerRequest");
            engine.LoadScript(script.ToArray());

            if (engine.Execute() == VMState.FAULT)
            {
                return (false, false);
            }

            var result = engine.ResultStack.Pop();
            result.Should().BeOfType(typeof(VM.Types.Boolean));

            return (true, result);
        }
    }
}
