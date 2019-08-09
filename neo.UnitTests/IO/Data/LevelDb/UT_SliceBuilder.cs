using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IO.Data.LevelDB;

namespace Neo.UnitTests.IO.Data.LevelDb
{
    [TestClass]
    public class UT_SliceBuilder
    {
        private SliceBuilder sliceBuilder;

        [TestInitialize]
        public void SetUp()
        {
            sliceBuilder = SliceBuilder.Begin();
        }

        [TestMethod]
        public void TestAddByte()
        {
            sliceBuilder.Add(0x01);
            Slice source = sliceBuilder;
            Slice expected = (byte)0x01;
            Assert.AreEqual(expected, source);
        }

        [TestMethod]
        public void TestAddUshort()
        {
            ushort value = 15;
            sliceBuilder.Add(value);
            Slice source = sliceBuilder;
            Slice expected = value;
            Assert.AreEqual(expected, source);
        }

        [TestMethod]
        public void TestAddUint()
        {
            uint value = 15;
            sliceBuilder.Add(value);
            Slice source = sliceBuilder;
            Slice expected = value;
            Assert.AreEqual(expected, source);
        }

        [TestMethod]
        public void TestAddlong()
        {
            long value = 15L;
            sliceBuilder.Add(value);
            Slice source = sliceBuilder;
            Slice expected = value;
            Assert.AreEqual(expected, source);
        }

        [TestMethod]
        public void TestAddEnumerableBytes()
        {
            byte[] value = new byte[] { 0x01, 0x02 };
            sliceBuilder.Add(value);
            Slice source = sliceBuilder;
            Slice expected = value;
            Assert.AreEqual(expected, source);
        }

        [TestMethod]
        public void TestAddString()
        {
            string value = "hello";
            sliceBuilder.Add(value);
            Slice source = sliceBuilder;
            Slice expected = value;
            Assert.AreEqual(expected, source);
        }

        [TestMethod]
        public void TestAddSerializable()
        {
            UInt160 value = UInt160.Parse("0xa400ff00ff00ff00ff00ff00ff00ff00ff00ff01");
            sliceBuilder.Add(value);
            Slice source = sliceBuilder;
            Slice expected = value.ToArray();
            Assert.AreEqual(expected, source);
        }

        [TestMethod]
        public void TestBeginWithPrefix()
        {
            sliceBuilder = SliceBuilder.Begin(0x01);
            sliceBuilder.Add(0x02);

            Slice source = sliceBuilder;
            Slice expected = new byte[] { 0x01, 0x02 };
            Assert.AreEqual(expected, source);
        }
    }
}
