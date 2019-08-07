using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.SmartContract;
using System;

namespace Neo.UnitTests.SmartContract
{
    [TestClass]
    public class UT_InteropDescriptor
    {
        [TestMethod]
        public void TestGetMethod()
        {
            string method = @"System.ExecutionEngine.GetScriptContainer";
            Func<ApplicationEngine, bool> handler = testEngine;
            long price = 0_00000250;
            TriggerType allowedTriggers = TriggerType.All;
            InteropDescriptor descriptor = new InteropDescriptor(method, testEngine, price, allowedTriggers);
            descriptor.Method.Should().Be(method);
        }

        private bool testEngine(ApplicationEngine engine)
        {
            return true;
        }
    }
}
