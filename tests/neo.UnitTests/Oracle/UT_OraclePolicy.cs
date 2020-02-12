using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Cryptography.ECC;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Oracle;
using Neo.SmartContract.Native;

namespace Neo.UnitTests.Oracle
{
    [TestClass]
    public class UT_OraclePolicy
    {
        [TestInitialize]
        public void TestSetup()
        {
            TestBlockchain.InitializeMockNeoSystem();
        }

        [TestMethod]
        public void Check_GetPerRequestFee()
        {
            var snapshot = Blockchain.Singleton.GetSnapshot();
            // Fake blockchain
            OraclePolicyContract contract= NativeContract.OraclePolicy;
            Assert.AreEqual(contract.GetPerRequestFee(snapshot),1000);
        }

        [TestMethod]
        public void Check_GetTimeOutMilliSeconds()
        {
            var snapshot = Blockchain.Singleton.GetSnapshot();
            // Fake blockchain
            OraclePolicyContract contract = NativeContract.OraclePolicy;
            Assert.AreEqual(contract.GetPerRequestFee(snapshot), 1000);
        }

        [TestMethod]
        public void Check_GetOracleValidators()
        {
            var snapshot = Blockchain.Singleton.GetSnapshot();
            // Fake blockchain
            OraclePolicyContract contract = NativeContract.OraclePolicy;
            ECPoint[] obj=contract.GetOracleValidators(snapshot);
            Assert.AreEqual(obj.Length, 7);
        }
    }
}
