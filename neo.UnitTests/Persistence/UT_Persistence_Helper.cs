﻿using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Persistence.LevelDB;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Neo.UnitTests.Persistence
{
    [TestClass]
    public class UT_P_Helper
    {
        private LevelDBStore store;
        private string dbPath;
        private static readonly object locker = new object();

        [TestInitialize]
        public void TestSetup()
        {
            string threadName = Thread.CurrentThread.ManagedThreadId.ToString();
            dbPath = Path.GetFullPath(nameof(UT_P_Helper) + string.Format("_Chain_{0}", new Random().Next(1, 1000000).ToString("X8")) + threadName);
            if (store == null)
            {
                store = new LevelDBStore(dbPath);
            }
        }

        [TestCleanup]
        public void DeleteDir()
        {
            store.Dispose();
            store = null;
            TestUtils.DeleteFile(dbPath);
        }

        [TestMethod]
        public void TestContainsBlock()
        {
            Snapshot snapshot = store.GetSnapshot();
            TrimmedBlock block = new TrimmedBlock
            {
                ConsensusData = new ConsensusData(),
                MerkleRoot = UInt256.Parse("0xa400ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff02"),
                PrevHash = UInt256.Parse("0xa400ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff01"),
                Timestamp = new DateTime(1968, 06, 01, 0, 0, 0, DateTimeKind.Utc).ToTimestamp(),
                Index = 10,
                NextConsensus = UInt160.Parse("0xa400ff00ff00ff00ff00ff00ff00ff00ff00ff01"),
                Witness = new Witness
                {
                    InvocationScript = new byte[0],
                    VerificationScript = new[] { (byte)OpCode.PUSHT }
                },
                Hashes = new UInt256[] { TestUtils.GetTransaction().Hash }
            };
            snapshot.Blocks.Add(block.Hash, block);
            snapshot.Commit();
            //if contains block, return true
            Assert.AreEqual(snapshot.ContainsBlock(block.Hash), true);
            //if not,return false
            bool result = snapshot.ContainsBlock(UInt256.Parse("0x0000000000000000000000000000000000000000000000000000000000000000"));
            Assert.AreEqual(result, false);
        }

        [TestMethod]
        public void TestAsSequence()
        {
            lock (locker)
            {
                var neoSystem = new NeoSystem(store);
                var blockchain = Blockchain.Singleton;
                TestGetBlockByIndex(blockchain);
                TestGetHeaderByIndex(blockchain);
                TestGetNexBlockHash(blockchain);
            }
        }

        public void TestGetBlockByIndex(Blockchain blockchain)
        {
            Snapshot snapshot = store.GetSnapshot();
            TrimmedBlock block = new TrimmedBlock();
            block.ConsensusData = new ConsensusData();
            block.MerkleRoot = UInt256.Parse("0xa400ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff02");
            block.PrevHash = UInt256.Parse("0xa400ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff01");
            block.Timestamp = new DateTime(1968, 06, 01, 0, 0, 0, DateTimeKind.Utc).ToTimestamp();
            block.Index = 11;
            block.NextConsensus = UInt160.Parse("0xa400ff00ff00ff00ff00ff00ff00ff00ff00ff01");
            block.Witness = new Witness
            {
                InvocationScript = new byte[0],
                VerificationScript = new[] { (byte)OpCode.PUSHT }
            };
            block.Hashes = new UInt256[] { TestUtils.GetTransaction().Hash };
            snapshot.Blocks.Add(block.Hash, block);
            snapshot.Commit();
            Type type = blockchain.GetType();
            FieldInfo info = type.GetField("header_index", BindingFlags.NonPublic | BindingFlags.Instance);
            var header_index = (List<UInt256>)info.GetValue(blockchain);
            header_index.Add(block.Hash);
            Block storeBlock = snapshot.GetBlock(1);
            header_index.RemoveAt(1);
            Assert.AreEqual(block.MerkleRoot, storeBlock.MerkleRoot);
            Assert.AreEqual(block.Timestamp, storeBlock.Timestamp);
            Assert.AreEqual(block.PrevHash, storeBlock.PrevHash);
            Assert.AreEqual(block.Index, storeBlock.Index);

            snapshot.GetBlock(2).Should().BeNull();
        }

        public void TestGetHeaderByIndex(Blockchain blockchain)
        {
            Snapshot snapshot = store.GetSnapshot();
            TrimmedBlock block = new TrimmedBlock
            {
                ConsensusData = new ConsensusData(),
                MerkleRoot = UInt256.Parse("0xa400ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff02"),
                PrevHash = UInt256.Parse("0xa400ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff01"),
                Timestamp = new DateTime(1968, 06, 01, 0, 0, 0, DateTimeKind.Utc).ToTimestamp(),
                Index = 10,
                NextConsensus = UInt160.Parse("0xa400ff00ff00ff00ff00ff00ff00ff00ff00ff01"),
                Witness = new Witness
                {
                    InvocationScript = new byte[0],
                    VerificationScript = new[] { (byte)OpCode.PUSHT }
                },
                Hashes = new UInt256[] { TestUtils.GetTransaction().Hash }
            };

            snapshot.Blocks.Add(block.Hash, block);
            snapshot.Commit();

            Type type = blockchain.GetType();
            FieldInfo info = type.GetField("header_index", BindingFlags.NonPublic | BindingFlags.Instance);
            var header_index = (List<UInt256>)info.GetValue(blockchain);
            header_index.Add(block.Hash);

            var storeHeader = snapshot.GetHeader(1);
            header_index.RemoveAt(1);
            Assert.AreEqual(storeHeader.Index, block.Index);
            Assert.AreEqual(storeHeader.MerkleRoot, block.MerkleRoot);
            Assert.AreEqual(storeHeader.NextConsensus, block.NextConsensus);
            Assert.AreEqual(storeHeader.PrevHash, block.PrevHash);
            Assert.AreEqual(storeHeader.Timestamp, block.Timestamp);

            snapshot.GetHeader(2).Should().BeNull();
        }

        public void TestGetNexBlockHash(Blockchain blockchain)
        {
            Snapshot snapshot = store.GetSnapshot();
            TrimmedBlock block1 = new TrimmedBlock
            {
                ConsensusData = new ConsensusData(),
                MerkleRoot = UInt256.Parse("0xa400ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff01"),
                PrevHash = UInt256.Parse("0xa400ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff01"),
                Timestamp = new DateTime(1968, 06, 01, 0, 0, 0, DateTimeKind.Utc).ToTimestamp(),
                Index = 1,
                NextConsensus = UInt160.Parse("0xa400ff00ff00ff00ff00ff00ff00ff00ff00ff01"),
                Witness = new Witness
                {
                    InvocationScript = new byte[0],
                    VerificationScript = new[] { (byte)OpCode.PUSHT }
                },
                Hashes = new UInt256[] { TestUtils.GetTransaction().Hash }
            };
            TrimmedBlock block2 = new TrimmedBlock
            {
                ConsensusData = new ConsensusData(),
                MerkleRoot = UInt256.Parse("0xa400ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff02"),
                PrevHash = UInt256.Parse("0xa400ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff02"),
                Timestamp = new DateTime(1968, 06, 02, 0, 0, 0, DateTimeKind.Utc).ToTimestamp(),
                Index = 2,
                NextConsensus = UInt160.Parse("0xa400ff00ff00ff00ff00ff00ff00ff00ff00ff02"),
                Witness = new Witness
                {
                    InvocationScript = new byte[0],
                    VerificationScript = new[] { (byte)OpCode.PUSHT }
                },
                Hashes = new UInt256[] { TestUtils.GetTransaction().Hash }
            };

            snapshot.Blocks.Add(block1.Hash, block1);
            snapshot.Blocks.Add(block2.Hash, block2);
            snapshot.Commit();

            Type type = blockchain.GetType();
            FieldInfo info = type.GetField("header_index", BindingFlags.NonPublic | BindingFlags.Instance);
            var header_index = (List<UInt256>)info.GetValue(blockchain);
            header_index.Add(block1.Hash);
            header_index.Add(block2.Hash);

            var nextBlockHash = snapshot.GetNextBlockHash(block1.Hash);
            header_index.RemoveAt(2);
            header_index.RemoveAt(1);
            nextBlockHash.Should().Be(block2.Hash);
            snapshot.GetNextBlockHash(block2.Hash).Should().BeNull();
        }

        [TestMethod]
        public void TestContainsTransaction()
        {
            Snapshot snapshot = store.GetSnapshot();
            Transaction tx = new Transaction
            {
                Script = TestUtils.GetByteArray(32, 0x42),
                Sender = UInt160.Zero,
                SystemFee = 4200000000,
                Attributes = new TransactionAttribute[0],
                Witnesses = new[]
            {
                new Witness
                {
                    InvocationScript = new byte[0],
                    VerificationScript = new byte[0]
                }
            }
            };
            TransactionState txState = new TransactionState();
            txState.Transaction = tx;
            txState.BlockIndex = 10;
            snapshot.Transactions.Add(tx.Hash, txState);
            snapshot.Commit();
            Assert.AreEqual(snapshot.ContainsTransaction(tx.Hash), true);
            bool result = snapshot.ContainsTransaction(UInt256.Parse("0x0000000000000000000000000000000000000000000000000000000000000000"));
            Assert.AreEqual(result, false);
        }

        [TestMethod]
        public void TestGetBlockByHash()
        {
            Snapshot snapshot = store.GetSnapshot();
            TrimmedBlock block = new TrimmedBlock
            {
                ConsensusData = new ConsensusData(),
                MerkleRoot = UInt256.Parse("0xa400ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff02"),
                PrevHash = UInt256.Parse("0xa400ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff01"),
                Timestamp = new DateTime(1968, 06, 01, 0, 0, 0, DateTimeKind.Utc).ToTimestamp(),
                Index = 15,
                NextConsensus = UInt160.Parse("0xa400ff00ff00ff00ff00ff00ff00ff00ff00ff01"),
                Witness = new Witness
                {
                    InvocationScript = new byte[0],
                    VerificationScript = new[] { (byte)OpCode.PUSHT }
                },
                Hashes = new UInt256[] { TestUtils.GetTransaction().Hash }
            };

            snapshot.Blocks.Add(block.Hash, block);
            snapshot.Commit();
            Block storeBlock = snapshot.GetBlock(block.Hash);
            Assert.AreEqual(storeBlock.MerkleRoot, block.MerkleRoot);
            Assert.AreEqual(storeBlock.PrevHash, block.PrevHash);
            Assert.AreEqual(storeBlock.Timestamp, block.Timestamp);
            Assert.AreEqual(storeBlock.Index, block.Index);
            Assert.AreEqual(storeBlock.NextConsensus, block.NextConsensus);
            Assert.AreEqual(storeBlock.Witness, block.Witness);

        }

        [TestMethod]
        public void TestGetHeaderByHash()
        {
            Snapshot snapshot = store.GetSnapshot();
            TrimmedBlock block = new TrimmedBlock
            {
                ConsensusData = new ConsensusData(),
                MerkleRoot = UInt256.Parse("0xa400ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff02"),
                PrevHash = UInt256.Parse("0xa400ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff00ff01"),
                Timestamp = new DateTime(1968, 06, 01, 0, 0, 0, DateTimeKind.Utc).ToTimestamp(),
                Index = 10,
                NextConsensus = UInt160.Parse("0xa400ff00ff00ff00ff00ff00ff00ff00ff00ff01"),
                Witness = new Witness
                {
                    InvocationScript = new byte[0],
                    VerificationScript = new[] { (byte)OpCode.PUSHT }
                },
                Hashes = new UInt256[] { TestUtils.GetTransaction().Hash }
            };

            snapshot.Blocks.Add(block.Hash, block);
            snapshot.Commit();
            Assert.AreEqual(snapshot.GetHeader(block.Hash), block.Header);
        }

        [TestMethod]
        public void TestGetTransaction()
        {
            Snapshot snapshot = store.GetSnapshot();
            Transaction tx = new Transaction
            {
                Script = TestUtils.GetByteArray(32, 0x42),
                Sender = UInt160.Zero,
                SystemFee = 4200000000,
                Attributes = new TransactionAttribute[0],
                Witnesses = new[]
            {
                new Witness
                {
                    InvocationScript = new byte[0],
                    VerificationScript = new byte[0]
                }
            }
            };
            TransactionState txState = new TransactionState();
            txState.Transaction = tx;
            txState.BlockIndex = 10;
            snapshot.Transactions.Add(tx.Hash, txState);
            snapshot.Commit();
            var transaction = snapshot.GetTransaction(tx.Hash);
            Assert.AreEqual(transaction.Script.ToHexString(), tx.Script.ToHexString());
            Assert.AreEqual(transaction.Sender, tx.Sender);
            Assert.AreEqual(transaction.SystemFee, tx.SystemFee);
        }
    }
}