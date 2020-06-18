using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.SmartContract.Native.Tokens;
using Neo.VM;
using Neo.VM.Types;
using Neo.Wallets;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

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

        [TestMethod]
        public void Check_Request()
        {
            var snapshot = Blockchain.Singleton.GetSnapshot();
            var request = new OracleRequest()
            {
                URL = new Uri("https://www.baidu.com/"),
                FilterContractHash = UInt160.Zero,
                FilterMethod = "dotest",
                FilterArgs = "dotest",
                CallBackContractHash = NativeContract.NEO.Hash,
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
                ContractParameterType.Boolean,
                "request",
                request.URL.ToString(),
                request.FilterContractHash,
                request.FilterMethod,
                request.FilterArgs,
                request.CallBackContractHash,
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

            var manifestFilePath = "./ContractDemo.manifest.json";
            var manifest = ContractManifest.Parse(File.ReadAllBytes(manifestFilePath));
            var nefFilePath = "./ContractDemo.nef";
            NefFile file;
            using (var stream = new BinaryReader(File.OpenRead(nefFilePath), Encoding.UTF8, false))
            {
                file = stream.ReadSerializable<NefFile>();
            }

            ContractState contract = new ContractState
            {
                Id = snapshot.ContractId.GetAndChange().NextId++,
                Script = file.Script,
                Manifest = manifest
            };

            var request = new OracleRequest()
            {
                URL = new Uri("https://www.baidu.com/"),
                FilterContractHash = UInt160.Zero,
                FilterMethod = "dotest",
                FilterArgs = "dotest",
                CallBackContractHash = file.ScriptHash,
                CallBackMethod = "test",
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

            OracleResponse response = new OracleResponse();
            response.RequestTxHash = requestTxHash;
            response.Result = keyPair.PublicKey.ToArray();
            response.FilterCost = 0;
            Transaction responsetx = CreateResponseTransaction(snapshot, response);
            Console.WriteLine(responsetx.SystemFee);


        }

        private static Transaction CreateResponseTransaction(StoreView initsnapshot, OracleResponse response)
        {
            StoreView snapshot = initsnapshot.Clone();
            var manifestFilePath = "./ContractDemo.manifest.json";
            var manifest = ContractManifest.Parse(File.ReadAllBytes(manifestFilePath));
            var nefFilePath = "./ContractDemo.nef";
            NefFile file;
            using (var stream = new BinaryReader(File.OpenRead(nefFilePath), Encoding.UTF8, false))
            {
                file = stream.ReadSerializable<NefFile>();
            }

            ContractState contract = new ContractState
            {
                Id = snapshot.ContractId.GetAndChange().NextId++,
                Script = file.Script,
                Manifest = manifest
            };
            snapshot.Contracts.Add(file.ScriptHash, contract);

            var oracleAddress = NativeContract.Oracle.GetOracleMultiSigContract(snapshot);
            ScriptBuilder sb = new ScriptBuilder();
            sb.EmitAppCall(NativeContract.Oracle.Hash, ContractParameterType.Void, "onPersist");

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
                    new OracleResponseAttribute()
                    {
                         response = response,
                    }
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
            sb2.EmitAppCall(NativeContract.Oracle.Hash, ContractParameterType.Void, "callBack");

            var state = new TransactionState
            {
                BlockIndex = snapshot.PersistingBlock.Index,
                Transaction = tx
            };
            snapshot.Transactions.Add(tx.Hash, state);

            var engine2 = ApplicationEngine.Run(sb2.ToArray(), snapshot, tx, testMode: true);
            if (engine2.State != VMState.HALT) throw new ApplicationException();
            tx.SystemFee = engine.GasConsumed;
            // Calculate network fee
            int size = tx.Size;
            tx.NetworkFee += Wallet.CalculateNetworkFee(oracleAddress.Script, ref size);
            tx.NetworkFee += size * NativeContract.Policy.GetFeePerByte(snapshot);
            return tx;
        }

    }
}
