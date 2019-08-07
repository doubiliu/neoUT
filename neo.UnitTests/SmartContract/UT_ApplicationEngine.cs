using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;
using System;

namespace Neo.UnitTests.SmartContract
{
    [TestClass]
    public class UT_ApplicationEngine
    {
        private string message = null;
        private StackItem item = null;
        private Store Store;

        [TestInitialize]
        public void TestSetup()
        {
            TestBlockchain.InitializeMockNeoSystem();
            Store = TestBlockchain.GetStore();
        }

        [TestMethod]
        public void TestLog()
        {
            ApplicationEngine.Log += Test_Log1;
            var snapshot = Store.GetSnapshot().Clone();
            var engine = new ApplicationEngine(TriggerType.Application, null, snapshot, 0, true);
            string logMessage = "TestMessage";

            engine.SendLog(UInt160.Zero, logMessage);
            message.Should().Be(logMessage);

            ApplicationEngine.Log += Test_Log2;
            engine.SendLog(UInt160.Zero, logMessage);
            message.Should().Be(null);

            message = logMessage;
            ApplicationEngine.Log -= Test_Log1;
            engine.SendLog(UInt160.Zero, logMessage);
            message.Should().Be(null);

            ApplicationEngine.Log -= Test_Log2;
            engine.SendLog(UInt160.Zero, logMessage);
            message.Should().Be(null);
        }

        [TestMethod]
        public void TestNotify()
        {
            ApplicationEngine.Notify += Test_Notify1;
            var snapshot = Store.GetSnapshot().Clone();
            var engine = new ApplicationEngine(TriggerType.Application, null, snapshot, 0, true);
            StackItem notifyItem = "TestItem";

            engine.SendNotification(UInt160.Zero, notifyItem);
            item.Should().Be(notifyItem);

            ApplicationEngine.Notify += Test_Notify2;
            engine.SendNotification(UInt160.Zero, notifyItem);
            item.Should().Be(null);

            item = notifyItem;
            ApplicationEngine.Notify -= Test_Notify1;
            engine.SendNotification(UInt160.Zero, notifyItem);
            item.Should().Be(null);

            ApplicationEngine.Notify -= Test_Notify2;
            engine.SendNotification(UInt160.Zero, notifyItem);
            item.Should().Be(null);
        }

        [TestMethod]
        public void TestDisposable()
        {
            var snapshot = Store.GetSnapshot().Clone();
            var replica = snapshot.Clone();
            var engine = new ApplicationEngine(TriggerType.Application, null, snapshot, 0, true);
            engine.AddDisposable(replica).Should().Be(replica);
            Action action = () => engine.Dispose();
            action.ShouldNotThrow();
        }

        private void Test_Log1(object sender, LogEventArgs e)
        {
            message = e.Message;
        }

        private void Test_Log2(object sender, LogEventArgs e)
        {
            message = null;
        }

        private void Test_Notify1(object sender, NotifyEventArgs e)
        {
            item = e.State;
        }

        private void Test_Notify2(object sender, NotifyEventArgs e)
        {
            item = null;
        }
    }
}
