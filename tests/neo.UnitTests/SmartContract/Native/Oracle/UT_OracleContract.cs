using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Oracle;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.SmartContract.Native.Tokens;
using Neo.UnitTests.Extensions;
using Neo.VM;
using Neo.VM.Types;
using Neo.Wallets;
using System;
using System.Numerics;
using System.Security.Cryptography;
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

        [TestMethod]
        public void Check_RegisterRequest()
        {
            var snapshot = Blockchain.Singleton.GetSnapshot();
            var request = new OracleHttpRequest() {
                URL = new Uri("https://www.baidu.com/"),
                Filter=new OracleFilter() {
                    ContractHash= UInt160.Zero,
                    FilterMethod= "dotest",
                    FilterArgs= "dotest",
                },
                CallBackContractHash= NativeContract.NEO.Hash,
                CallBackMethod= "unregisterCandidate",
                OracleFee= 1000L
            };
            var ret_RegisterRequest = Check_RegisterRequest(snapshot, request,out UInt256 requestTxHash);
            ret_RegisterRequest.Result.Should().Be(new VM.Types.Boolean(true));
            ret_RegisterRequest.State.Should().BeTrue();
        }

        internal static (bool State, StackItem Result) Check_RegisterRequest(StoreView snapshot,OracleHttpRequest request,out UInt256 requestTxHash)
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
            script.EmitPush(request.OracleFee);
            script.EmitPush(request.CallBackMethod);
            script.EmitPush(request.CallBackContractHash);
            script.EmitPush(request.Filter.FilterArgs);
            script.EmitPush(request.Filter.FilterMethod);
            script.EmitPush(request.Filter.ContractHash);
            script.EmitPush(request.URL.ToString());
            script.EmitPush(7);
            script.Emit(OpCode.PACK);
            script.EmitPush("registerRequest");

            Transaction tx = new Transaction
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
            var engine = new ApplicationEngine(TriggerType.Application,
                tx, snapshot, 0, true);

            engine.LoadScript(NativeContract.Oracle.Script);

            engine.LoadScript(script.ToArray());

            if (engine.Execute() == VMState.FAULT)
            {
                return (false, false);
            }

            var result = engine.ResultStack.Pop();
            result.Should().BeOfType(typeof(VM.Types.Boolean));

            return (true, result);
        }

        [TestMethod]
        public void Check_InvokeCallBackMethod()
        {
            var snapshot = Blockchain.Singleton.GetSnapshot();
            var request = new OracleHttpRequest()
            {
                URL = new Uri("https://www.baidu.com/"),
                Filter = new OracleFilter()
                {
                    ContractHash = UInt160.Zero,
                    FilterMethod = "dotest",
                    FilterArgs = "dotest",
                },
                CallBackContractHash = NativeContract.NEO.Hash,
                CallBackMethod = "unregisterCandidate",
                OracleFee = 1000L
            };
            var ret_RegisterRequest = Check_RegisterRequest(snapshot, request, out UInt256 requestTxHash);
            ret_RegisterRequest.Result.Should().Be(new VM.Types.Boolean(true));
            ret_RegisterRequest.State.Should().BeTrue();

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
            Transaction tx=CreateResponseTransaction(snapshot, response);
            Console.WriteLine(tx.SystemFee);


        }

        private static Transaction CreateResponseTransaction(StoreView initsnapshot, OracleResponse response)
        {
            StoreView snapshot = initsnapshot.Clone();
            var contract = NativeContract.Oracle.GetOracleMultiSigContract(snapshot);
            ScriptBuilder sb = new ScriptBuilder();
            sb.EmitAppCall(NativeContract.Oracle.Hash, "onPersist");

            var tx = new Transaction()
            {
                Version = 0,
                ValidUntilBlock = snapshot.Height + Transaction.MaxValidUntilBlockIncrement,
                Attributes = new TransactionAttribute[]{
                    new Cosigner()
                    {
                        Account = contract.ScriptHash,
                        AllowedContracts = new UInt160[]{ NativeContract.Oracle.Hash },
                        Scopes = WitnessScope.CustomContracts
                    },
                    new OracleResponseAttribute()
                    {
                         response = response,
                    }
                },
                Sender = contract.ScriptHash,
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
            sb2.EmitAppCall(NativeContract.Oracle.Hash, "invokeCallBackMethod");

            var state = new TransactionState
            {
                BlockIndex = snapshot.PersistingBlock.Index,
                Transaction = tx
            };
            snapshot.Transactions.Add(tx.Hash, state);

            var engine2 = ApplicationEngine.Run(sb2.ToArray(), snapshot.Clone(), tx, testMode: true);
            if (engine2.State != VMState.HALT)
            {
                throw new ApplicationException();
            }
            tx.SystemFee = engine.GasConsumed;
            // Calculate network fee
            int size = tx.Size;
            tx.NetworkFee += Wallet.CalculateNetworkFee(contract.Script, ref size);
            tx.NetworkFee += size * NativeContract.Policy.GetFeePerByte(snapshot);
            return tx;
        }

    }
}
