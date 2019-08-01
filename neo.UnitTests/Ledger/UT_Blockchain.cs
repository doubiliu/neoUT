﻿using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using System.IO;

namespace Neo.UnitTests.Ledger
{
    internal class TestBlock : Block
    {
        public override bool Verify(Snapshot snapshot)
        {
            return true;
        }

        public static TestBlock Cast(Block input)
        {
            TestBlock block = new TestBlock();
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            BinaryReader reader = new BinaryReader(stream);
            input.Serialize(writer);
            stream.Seek(0, SeekOrigin.Begin);
            block.Deserialize(reader);
            return block;
        }
    }

    internal class TestHeader : Header
    {
        public override bool Verify(Snapshot snapshot)
        {
            return true;
        }

        public static TestHeader Cast(Header input)
        {
            TestHeader header = new TestHeader();
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            BinaryReader reader = new BinaryReader(stream);
            input.Serialize(writer);
            stream.Seek(0, SeekOrigin.Begin);
            header.Deserialize(reader);
            return header;
        }
    }
    [TestClass]
    public class UT_Blockchain
    {
        private NeoSystem system = null;
        private Store store = null;
        Transaction txSample = Blockchain.GenesisBlock.Transactions[0];

        [TestInitialize]
        public void Initialize()
        {
            system = TestBlockchain.InitializeMockNeoSystem();
            store = TestBlockchain.GetStore();
            Blockchain.Singleton.MemPool.TryAdd(txSample.Hash, txSample);
        }

        [TestMethod]
        public void TestConstructor()
        {
            system.ActorSystem.ActorOf(Blockchain.Props(system, store)).Should().NotBeSameAs(system.Blockchain);
        }

        [TestMethod]
        public void TestContainsBlock()
        {
            Blockchain.Singleton.ContainsBlock(UInt256.Zero).Should().BeFalse();
        }

        [TestMethod]
        public void TestContainsTransaction()
        {
            Blockchain.Singleton.ContainsTransaction(UInt256.Zero).Should().BeTrue();
            Blockchain.Singleton.ContainsTransaction(txSample.Hash).Should().BeTrue();
        }

        [TestMethod]
        public void TestGetCurrentBlockHash()
        {
            Blockchain.Singleton.CurrentBlockHash.Should().Be(UInt256.Parse("e0c33882caabeaf7c615896dab5818fdd649b3026f373ad580c34f093f0cb73d"));
        }

        [TestMethod]
        public void TestGetCurrentHeaderHash()
        {
            Blockchain.Singleton.CurrentHeaderHash.Should().Be(UInt256.Parse("e0c33882caabeaf7c615896dab5818fdd649b3026f373ad580c34f093f0cb73d"));
        }

        [TestMethod]
        public void TestGetBlock()
        {
            Blockchain.Singleton.GetBlock(UInt256.Zero).Should().BeNull();
        }

        [TestMethod]
        public void TestGetBlockHash()
        {
            Blockchain.Singleton.GetBlockHash(0).Should().Be(UInt256.Parse("e0c33882caabeaf7c615896dab5818fdd649b3026f373ad580c34f093f0cb73d"));
            Blockchain.Singleton.GetBlockHash(10).Should().BeNull();
        }

        [TestMethod]
        public void TestGetTransaction()
        {
            Blockchain.Singleton.GetTransaction(UInt256.Zero).Should().NotBeNull();
            Blockchain.Singleton.GetTransaction(txSample.Hash).Should().NotBeNull();
        }

        [TestMethod]
        public void TestFillCompletedConstructor()
        {
            new Blockchain.FillCompleted().Should().NotBeNull();
        }

        [TestMethod]
        public void TestFillMemoryPoolConstructor()
        {
            Transaction[] transactions = new Transaction[] { };
            var pool = new Blockchain.FillMemoryPool()
            {
                Transactions = transactions
            };
            pool.Should().NotBeNull();
            pool.Transactions.Should().BeEquivalentTo(transactions);
        }

        [TestMethod]
        public void TestImportConstructor()
        {
            Block[] blocks = new Block[] { };
            var import = new Blockchain.Import()
            {
                Blocks = blocks
            };
            import.Should().NotBeNull();
            import.Blocks.Should().BeEquivalentTo(blocks);
        }

        [TestMethod]
        public void TestImportCompletedConstructor()
        {
            new Blockchain.ImportCompleted().Should().NotBeNull();
        }

        [TestMethod]
        public void TestPersistCompletedConstructor()
        {
            Block block = Blockchain.GenesisBlock;
            var pool = new Blockchain.PersistCompleted()
            {
                Block = block
            };
            pool.Should().NotBeNull();
            pool.Block.Should().Be(block);
        }
    }
}
