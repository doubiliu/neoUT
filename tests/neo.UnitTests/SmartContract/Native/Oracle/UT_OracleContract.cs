using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Cryptography.ECC;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.SmartContract.Native.Oracle;
using Neo.SmartContract.Native.Tokens;
using Neo.VM;
using Neo.VM.Types;
using Neo.Wallets;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using static Neo.UnitTests.Extensions.Nep5NativeContractExtensions;
using ECPoint = Neo.Cryptography.ECC.ECPoint;

namespace Neo.UnitTests.SmartContract.Native
{
    [TestClass]
    public class UT_OracleContract
    {
        [TestInitialize]
        public void TestSetup()
        {
            TestBlockchain.InitializeMockNeoSystem();
        }

        internal static StorageKey CreateStorageKey(byte prefix, byte[] key = null)
        {
            StorageKey storageKey = new StorageKey
            {
                Id = NativeContract.Oracle.Id,
                Key = new byte[sizeof(byte) + (key?.Length ?? 0)]
            };
            storageKey.Key[0] = prefix;
            key?.CopyTo(storageKey.Key.AsSpan(1));
            return storageKey;
        }

        [TestMethod]
        public void Test_GetPerRequestFee()
        {
            var snapshot = Blockchain.Singleton.GetSnapshot();
            var script = new ScriptBuilder();
            script.EmitAppCall(NativeContract.Oracle.Hash, "getPerRequestFee");
            var engine = new ApplicationEngine(TriggerType.Application, null, snapshot, 0, true);
            engine.LoadScript(script.ToArray());

            engine.Execute().Should().Be(VMState.HALT);
            var result = engine.ResultStack.Pop();
            result.Should().BeOfType(typeof(VM.Types.Integer));
            Assert.AreEqual(result, 0);
        }

        [TestMethod]
        public void Test_SetPerRequestFee()
        {
            var snapshot = Blockchain.Singleton.GetSnapshot().Clone();

            // Init
            var engine = new ApplicationEngine(TriggerType.Application, null, snapshot, 0, true);
            var from = NativeContract.Oracle.GetOracleMultiSigAddress(snapshot);
            long value = 12345;

            // Set 
            var script = new ScriptBuilder();
            script.EmitAppCall(NativeContract.Oracle.Hash, "setPerRequestFee", value);
            engine = new ApplicationEngine(TriggerType.Application, new ManualWitness(from), snapshot, 0, true);
            engine.LoadScript(script.ToArray());

            engine.Execute().Should().Be(VMState.HALT);
            var result = engine.ResultStack.Pop();
            result.Should().BeOfType(typeof(VM.Types.Boolean));
            Assert.IsTrue((result as VM.Types.Boolean).GetBoolean());

            // Set (wrong witness)
            script = new ScriptBuilder();
            script.EmitAppCall(NativeContract.Oracle.Hash, "setPerRequestFee", value);
            engine = new ApplicationEngine(TriggerType.Application, new ManualWitness(null), snapshot, 0, true);
            engine.LoadScript(script.ToArray());

            engine.Execute().Should().Be(VMState.HALT);
            result = engine.ResultStack.Pop();
            result.Should().BeOfType(typeof(VM.Types.Boolean));
            Assert.IsFalse((result as VM.Types.Boolean).GetBoolean());

            // Set wrong (negative)
            script = new ScriptBuilder();
            script.EmitAppCall(NativeContract.Oracle.Hash, "setPerRequestFee", -1);
            engine = new ApplicationEngine(TriggerType.Application, new ManualWitness(from), snapshot, 0, true);
            engine.LoadScript(script.ToArray());

            engine.Execute().Should().Be(VMState.HALT);
            result = engine.ResultStack.Pop();
            result.Should().BeOfType(typeof(VM.Types.Boolean));
            Assert.IsFalse((result as VM.Types.Boolean).GetBoolean());

            // Get
            script = new ScriptBuilder();
            script.EmitAppCall(NativeContract.Oracle.Hash, "getPerRequestFee");
            engine = new ApplicationEngine(TriggerType.Application, null, snapshot, 0, true);
            engine.LoadScript(script.ToArray());

            engine.Execute().Should().Be(VMState.HALT);
            result = engine.ResultStack.Pop();
            result.Should().BeOfType(typeof(VM.Types.Integer));
            Assert.AreEqual(result, value);
        }

        [TestMethod]
        public void Test_GetValidHeight()
        {
            var snapshot = Blockchain.Singleton.GetSnapshot();
            var script = new ScriptBuilder();
            script.EmitAppCall(NativeContract.Oracle.Hash, "getValidHeight");
            var engine = new ApplicationEngine(TriggerType.Application, null, snapshot, 0, true);
            engine.LoadScript(script.ToArray());

            engine.Execute().Should().Be(VMState.HALT);
            var result = engine.ResultStack.Pop();
            result.Should().BeOfType(typeof(VM.Types.Integer));
            Assert.AreEqual(result, 0);
        }

        [TestMethod]
        public void Test_SetValidHeight()
        {
            var snapshot = Blockchain.Singleton.GetSnapshot().Clone();

            // Init
            var engine = new ApplicationEngine(TriggerType.Application, null, snapshot, 0, true);
            var from = NativeContract.Oracle.GetOracleMultiSigAddress(snapshot);
            uint value = 123;

            // Set 
            var script = new ScriptBuilder();
            script.EmitAppCall(NativeContract.Oracle.Hash, "setValidHeight", value);
            engine = new ApplicationEngine(TriggerType.Application, new ManualWitness(from), snapshot, 0, true);
            engine.LoadScript(script.ToArray());

            engine.Execute().Should().Be(VMState.HALT);
            var result = engine.ResultStack.Pop();
            result.Should().BeOfType(typeof(VM.Types.Boolean));
            Assert.IsTrue((result as VM.Types.Boolean).GetBoolean());

            // Set (wrong witness)
            script = new ScriptBuilder();
            script.EmitAppCall(NativeContract.Oracle.Hash, "setValidHeight", value);
            engine = new ApplicationEngine(TriggerType.Application, new ManualWitness(null), snapshot, 0, true);
            engine.LoadScript(script.ToArray());

            engine.Execute().Should().Be(VMState.HALT);
            result = engine.ResultStack.Pop();
            result.Should().BeOfType(typeof(VM.Types.Boolean));
            Assert.IsFalse((result as VM.Types.Boolean).GetBoolean());

            // Get
            script = new ScriptBuilder();
            script.EmitAppCall(NativeContract.Oracle.Hash, "getValidHeight");
            engine = new ApplicationEngine(TriggerType.Application, null, snapshot, 0, true);
            engine.LoadScript(script.ToArray());

            engine.Execute().Should().Be(VMState.HALT);
            result = engine.ResultStack.Pop();
            result.Should().BeOfType(typeof(VM.Types.Integer));
            Assert.AreEqual(result, value);
        }

        [TestMethod]
        public void Test_GetHttpConfig()
        {
            var snapshot = Blockchain.Singleton.GetSnapshot();
            var script = new ScriptBuilder();
            script.EmitAppCall(NativeContract.Oracle.Hash, "getConfig", HttpConfig.Key);
            var engine = new ApplicationEngine(TriggerType.Application, null, snapshot, 0, true);
            engine.LoadScript(script.ToArray());

            engine.Execute().Should().Be(VMState.HALT);
            var result = engine.ResultStack.Pop();
            result.Should().BeOfType(typeof(VM.Types.Array));

            var cfg = new HttpConfig();
            cfg.FromStackItem(result);

            Assert.AreEqual(cfg.TimeOut, 5000);
        }

        [TestMethod]
        public void Test_SetConfig()
        {
            var snapshot = Blockchain.Singleton.GetSnapshot().Clone();

            // Init
            var engine = new ApplicationEngine(TriggerType.Application, null, snapshot, 0, true);
            var from = NativeContract.Oracle.GetOracleMultiSigAddress(snapshot);
            var key = HttpConfig.Key;
            var value = new HttpConfig() { TimeOut = 12345 };
            var data = JsonConvert.SerializeObject(value);

            // Set (wrong witness)
            var script = new ScriptBuilder();
            script.EmitAppCall(NativeContract.Oracle.Hash, "setConfig", key, data);
            engine = new ApplicationEngine(TriggerType.Application, new ManualWitness(null), snapshot, 0, true);
            engine.LoadScript(script.ToArray());

            engine.Execute().Should().Be(VMState.HALT);
            var result = engine.ResultStack.Pop();
            result.Should().BeOfType(typeof(VM.Types.Boolean));
            Assert.IsFalse((result as VM.Types.Boolean).GetBoolean());

            // Set good
            script = new ScriptBuilder();
            script.EmitAppCall(NativeContract.Oracle.Hash, "setConfig", key, data);
            engine = new ApplicationEngine(TriggerType.Application, new ManualWitness(from), snapshot, 0, true);
            engine.LoadScript(script.ToArray());

            engine.Execute().Should().Be(VMState.HALT);
            result = engine.ResultStack.Pop();
            result.Should().BeOfType(typeof(VM.Types.Boolean));
            Assert.IsTrue((result as VM.Types.Boolean).GetBoolean());

            // Get
            script = new ScriptBuilder();
            script.EmitAppCall(NativeContract.Oracle.Hash, "getConfig", key);
            engine = new ApplicationEngine(TriggerType.Application, null, snapshot, 0, true);
            engine.LoadScript(script.ToArray());

            engine.Execute().Should().Be(VMState.HALT);
            result = engine.ResultStack.Pop();
            result.Should().BeOfType(typeof(VM.Types.Array));
            var array = (VM.Types.Array)result;
            Assert.AreEqual(array[0].GetInteger(), new BigInteger(value.TimeOut));
        }

        [TestMethod]
        public void Test_GetOracleValidatorsCount()
        {
            var snapshot = Blockchain.Singleton.GetSnapshot();
            var script = new ScriptBuilder();
            script.EmitAppCall(NativeContract.Oracle.Hash, "getOracleValidatorsCount");
            var engine = new ApplicationEngine(TriggerType.Application, null, snapshot, 0, true);
            engine.LoadScript(script.ToArray());

            engine.Execute().Should().Be(VMState.HALT);
            var result = engine.ResultStack.Pop();
            result.Should().BeOfType(typeof(VM.Types.Integer));
            Assert.AreEqual(result, 7);
        }

        [TestMethod]
        public void Test_GetOracleValidators()
        {
            var snapshot = Blockchain.Singleton.GetSnapshot();

            // Fake a oracle validator has cosignee.
            ECPoint[] oraclePubKeys = NativeContract.Oracle.GetOracleValidators(snapshot);

            ECPoint pubkey0 = oraclePubKeys[0]; // Validator0 is the cosignor
            ECPoint cosignorPubKey = oraclePubKeys[1]; // Validator1 is the cosignee
            var validator0Key = NativeContract.Oracle.CreateStorageKey(24, pubkey0); // 24 = Prefix_Validator
            var validator0Value = new StorageItem()
            {
                Value = cosignorPubKey.ToArray()
            };
            snapshot.Storages.Add(validator0Key, validator0Value);

            var script = new ScriptBuilder();
            script.EmitAppCall(NativeContract.Oracle.Hash, "getOracleValidators");
            var engine = new ApplicationEngine(TriggerType.Application, null, snapshot, 0, true);
            engine.LoadScript(script.ToArray());

            engine.Execute().Should().Be(VMState.HALT);
            var result = engine.ResultStack.Pop();
            result.Should().BeOfType(typeof(VM.Types.Array));
            Assert.AreEqual(6, ((VM.Types.Array)result).Count);

            // The validator0's cosignee should be the validator1
            var validators = (VM.Types.Array)result;
            var cosignee0Bytes = ((VM.Types.ByteString)validators[0]).GetSpan().ToHexString();
            var cosignee1Bytes = ((VM.Types.ByteString)validators[1]).GetSpan().ToHexString();
            Assert.AreNotEqual(cosignee0Bytes, cosignee1Bytes);
            var validator1Bytes = cosignorPubKey.ToArray().ToHexString();
            Assert.AreNotEqual(cosignee1Bytes, validator1Bytes);

            // clear data
            snapshot.Storages.Delete(validator0Key);
        }

        [TestMethod]
        public void Check_Request()
        {
            var snapshot = Blockchain.Singleton.GetSnapshot();
            var request = new OracleRequest()
            {
                URL = new Uri("https://www.baidu.com/"),
                FilterPath = "dotest",
                CallBackContract = NativeContract.NEO.Hash,
                CallBackMethod = "unregisterCandidate",
                OracleFee = 1000L
            };
            var ret_Request = Check_Request(snapshot, request, out UInt256 requestTxHash, out Transaction tx);
            ret_Request.Result.Should().Be(new VM.Types.Boolean(true));
            ret_Request.State.Should().BeTrue();
        }

        internal static (bool State, StackItem Result) Check_Request(StoreView snapshot, OracleRequest request, out UInt256 requestTxHash, out Transaction tx)
        {
            snapshot.PersistingBlock = new Block() { Index = 1000 };
            byte[] privateKey = new byte[32];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(privateKey);
            }
            KeyPair keyPair = new KeyPair(privateKey);
            UInt160 account = Contract.CreateSignatureRedeemScript(keyPair.PublicKey).ToScriptHash();

            var script = new ScriptBuilder();
            script.EmitAppCall(NativeContract.Oracle.Hash,
                "request",
                request.URL.ToString(),
                request.FilterPath,
                request.CallBackContract,
                request.CallBackMethod,
                request.OracleFee);

            tx = new Transaction
            {
                Version = 0,
                Nonce = (uint)1000,
                Script = script.ToArray(),
                Sender = account,
                ValidUntilBlock = snapshot.Height + Transaction.MaxValidUntilBlockIncrement,
                Attributes = new TransactionAttribute[0],
                Witnesses = new Witness[] { new Witness
            {
                InvocationScript = System.Array.Empty<byte>(),
                VerificationScript = Contract.CreateSignatureRedeemScript(keyPair.PublicKey)
            }}
            };
            var data = new ContractParametersContext(tx);
            byte[] sig = data.Verifiable.Sign(keyPair);
            tx.Witnesses[0].InvocationScript = sig;
            requestTxHash = tx.Hash;
            ApplicationEngine engine = ApplicationEngine.Run(script.ToArray(), snapshot, tx, null, 0, true);
            if (engine.State == VMState.FAULT)
            {
                return (false, false);
            }

            var result = engine.ResultStack.Pop();
            result.Should().BeOfType(typeof(VM.Types.Boolean));
            return (true, result);
        }

        [TestMethod]
        public void Check_CallBack()
        {
            var snapshot = Blockchain.Singleton.GetSnapshot();
            string manifestString = "7b2267726f757073223a5b5d2c226665617475726573223a7b2273746f72616765223a747275652c2270617961626c65223a747275657d2c22616269223a7b0a202020202268617368223a22307831303633323632353734653666636432333636653632633539666663616130383531623232343836222c0a20202020226d6574686f6473223a0a202020205b0a20202020202020207b0a202020202020202020202020226e616d65223a227465737431222c0a202020202020202020202020226f6666736574223a2230222c0a20202020202020202020202022706172616d6574657273223a0a2020202020202020202020205b0a202020202020202020202020202020207b0a2020202020202020202020202020202020202020226e616d65223a2264617461222c0a20202020202020202020202020202020202020202274797065223a22427974654172726179220a202020202020202020202020202020207d0a2020202020202020202020205d2c0a2020202020202020202020202272657475726e54797065223a22426f6f6c65616e220a20202020202020207d0a202020205d2c0a20202020226576656e7473223a0a202020205b0a202020205d0a7d2c227065726d697373696f6e73223a5b7b22636f6e7472616374223a222a222c226d6574686f6473223a222a227d5d2c22747275737473223a5b5d2c22736166654d6574686f6473223a5b5d2c226578747261223a6e756c6c7d";
            var manifest = ContractManifest.Parse(manifestString.HexToBytes());
            var nefFileString = "4e4546336e656f6e00000000000000000000000000000000000000000000000000000000030000000000000000000000000000008624b25108aafc9fc5626e36d2fce67425266310d47370510757010111706840";
            NefFile file = new NefFile();
            file.Deserialize(new BinaryReader(new MemoryStream(nefFileString.HexToBytes())));
            ContractState contract = new ContractState
            {
                Id = snapshot.ContractId.GetAndChange().NextId++,
                Script = file.Script,
                Manifest = manifest
            };

            var request = new OracleRequest()
            {
                URL = new Uri("https://www.baidu.com/"),
                FilterPath = "dotest",
                CallBackContract = contract.ScriptHash,
                CallBackMethod = "test1",
                OracleFee = 1000L
            };
            var ret_Request = Check_Request(snapshot, request, out UInt256 requestTxHash, out Transaction tx);
            ret_Request.Result.Should().Be(new VM.Types.Boolean(true));
            ret_Request.State.Should().BeTrue();
            snapshot.Transactions.Add(tx.Hash, new TransactionState() { Transaction = tx, VMState = VMState.HALT, BlockIndex = snapshot.PersistingBlock.Index });


            byte[] privateKey = new byte[32];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(privateKey);
            }
            KeyPair keyPair = new KeyPair(privateKey);

            OracleResponseAttribute response = new OracleResponseAttribute();
            response.RequestTxHash = requestTxHash;
            response.Result = keyPair.PublicKey.ToArray();
            response.FilterCost = 0;
            Transaction responsetx = CreateResponseTransaction(snapshot, response);
            Console.WriteLine(responsetx.SystemFee);
        }

        private static Transaction CreateResponseTransaction(StoreView initsnapshot, OracleResponseAttribute response)
        {
            StoreView snapshot = initsnapshot.Clone();
            string manifestString = "7b2267726f757073223a5b5d2c226665617475726573223a7b2273746f72616765223a747275652c2270617961626c65223a747275657d2c22616269223a7b0a202020202268617368223a22307831303633323632353734653666636432333636653632633539666663616130383531623232343836222c0a20202020226d6574686f6473223a0a202020205b0a20202020202020207b0a202020202020202020202020226e616d65223a227465737431222c0a202020202020202020202020226f6666736574223a2230222c0a20202020202020202020202022706172616d6574657273223a0a2020202020202020202020205b0a202020202020202020202020202020207b0a2020202020202020202020202020202020202020226e616d65223a2264617461222c0a20202020202020202020202020202020202020202274797065223a22427974654172726179220a202020202020202020202020202020207d0a2020202020202020202020205d2c0a2020202020202020202020202272657475726e54797065223a22426f6f6c65616e220a20202020202020207d0a202020205d2c0a20202020226576656e7473223a0a202020205b0a202020205d0a7d2c227065726d697373696f6e73223a5b7b22636f6e7472616374223a222a222c226d6574686f6473223a222a227d5d2c22747275737473223a5b5d2c22736166654d6574686f6473223a5b5d2c226578747261223a6e756c6c7d";
            var manifest = ContractManifest.Parse(manifestString.HexToBytes());
            var nefFileString = "4e4546336e656f6e00000000000000000000000000000000000000000000000000000000030000000000000000000000000000008624b25108aafc9fc5626e36d2fce67425266310d47370510757010111706840";
            NefFile file = new NefFile();
            file.Deserialize(new BinaryReader(new MemoryStream(nefFileString.HexToBytes())));

            ContractState contract = new ContractState
            {
                Id = snapshot.ContractId.GetAndChange().NextId++,
                Script = file.Script,
                Manifest = manifest
            };
            snapshot.Contracts.Add(file.ScriptHash, contract);

            var oracleAddress = NativeContract.Oracle.GetOracleMultiSigContract(snapshot);
            ScriptBuilder sb = new ScriptBuilder();
            sb.EmitAppCall(NativeContract.Oracle.Hash, "onPersist");

            var tx = new Transaction()
            {
                Version = 0,
                ValidUntilBlock = snapshot.Height + Transaction.MaxValidUntilBlockIncrement,
                Attributes = new TransactionAttribute[]{
                    new Cosigner()
                    {
                        Account = oracleAddress.ScriptHash,
                        AllowedContracts = new UInt160[]{ NativeContract.Oracle.Hash },
                        Scopes = WitnessScope.CustomContracts
                    },
                    response
                },
                Sender = oracleAddress.ScriptHash,
                Witnesses = new Witness[0],
                Script = sb.ToArray(),
                NetworkFee = 0,
                Nonce = 0,
                SystemFee = 0
            };

            snapshot.PersistingBlock = new Block() { Index = snapshot.Height + 1, Transactions = new Transaction[] { tx } };
            //commit response
            var engine = new ApplicationEngine(TriggerType.System, null, snapshot, 0, true);
            engine.LoadScript(sb.ToArray());
            if (engine.Execute() != VMState.HALT) throw new InvalidOperationException();

            var sb2 = new ScriptBuilder();
            sb2.EmitAppCall(NativeContract.Oracle.Hash, "callBack");

            var state = new TransactionState
            {
                BlockIndex = snapshot.PersistingBlock.Index,
                Transaction = tx
            };
            snapshot.Transactions.Add(tx.Hash, state);

            var engine2 = ApplicationEngine.Run(sb2.ToArray(), snapshot, tx, testMode: true);
            if (engine2.State != VMState.HALT) throw new ApplicationException();
            tx.SystemFee = engine2.GasConsumed;
            // Calculate network fee
            int size = tx.Size;
            tx.NetworkFee += Wallet.CalculateNetworkFee(oracleAddress.Script, ref size);
            tx.NetworkFee += size * NativeContract.Policy.GetFeePerByte(snapshot);
            return tx;
        }

    }
}
