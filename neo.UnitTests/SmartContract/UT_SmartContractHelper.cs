using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.SmartContract;
using Neo.VM;
using Neo.VM.Types;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Neo.UnitTests.SmartContract
{
    [TestClass]
    public class UT_SmartContractHelper
    {
        [TestMethod]
        public void TestIsMultiSigContract()
        {
            for (int j = 0; j < 4; j++)
            {
                if (j == 0)
                {
                    Neo.Cryptography.ECC.ECPoint[] publicKeys = new Neo.Cryptography.ECC.ECPoint[20];
                    for (int i = 0; i < 20; i++)
                    {
                        byte[] privateKey = new byte[32];
                        RandomNumberGenerator rng = RandomNumberGenerator.Create();
                        rng.GetBytes(privateKey);
                        KeyPair key = new KeyPair(privateKey);
                        publicKeys[i] = key.PublicKey;
                    }
                    byte[] script = Contract.CreateMultiSigRedeemScript(20, publicKeys);
                    Assert.AreEqual(true, Neo.SmartContract.Helper.IsMultiSigContract(script, out int m, out int n));
                }
                else if (j == 1)
                {
                    Neo.Cryptography.ECC.ECPoint[] publicKeys = new Neo.Cryptography.ECC.ECPoint[256];
                    for (int i = 0; i < 256; i++)
                    {
                        byte[] privateKey = new byte[32];
                        RandomNumberGenerator rng = RandomNumberGenerator.Create();
                        rng.GetBytes(privateKey);
                        KeyPair key = new KeyPair(privateKey);
                        publicKeys[i] = key.PublicKey;
                    }
                    byte[] script = Contract.CreateMultiSigRedeemScript(256, publicKeys);
                    Assert.AreEqual(true, Neo.SmartContract.Helper.IsMultiSigContract(script, out int m, out int n));
                }
                else if (j == 2)
                {
                    Neo.Cryptography.ECC.ECPoint[] publicKeys = new Neo.Cryptography.ECC.ECPoint[3];
                    for (int i = 0; i < 3; i++)
                    {
                        byte[] privateKey = new byte[32];
                        RandomNumberGenerator rng = RandomNumberGenerator.Create();
                        rng.GetBytes(privateKey);
                        KeyPair key = new KeyPair(privateKey);
                        publicKeys[i] = key.PublicKey;
                    }
                    byte[] script = Contract.CreateMultiSigRedeemScript(3, publicKeys);
                    Assert.AreEqual(true, Neo.SmartContract.Helper.IsMultiSigContract(script, out int m, out int n));
                }
                else
                {
                    Neo.Cryptography.ECC.ECPoint[] publicKeys = new Neo.Cryptography.ECC.ECPoint[3];
                    for (int i = 0; i < 3; i++)
                    {
                        byte[] privateKey = new byte[32];
                        RandomNumberGenerator rng = RandomNumberGenerator.Create();
                        rng.GetBytes(privateKey);
                        KeyPair key = new KeyPair(privateKey);
                        publicKeys[i] = key.PublicKey;
                    }
                    byte[] script = Contract.CreateMultiSigRedeemScript(3, publicKeys);
                    script[script.Length - 1] = 0x00;
                    Assert.AreEqual(false, Neo.SmartContract.Helper.IsMultiSigContract(script, out int m, out int n));
                }
            }
        }

        [TestMethod]
        public void TestIsSignatureContract()
        {
            byte[] privateKey = new byte[32];
            RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(privateKey);
            KeyPair key = new KeyPair(privateKey);
            byte[] script = Contract.CreateSignatureRedeemScript(key.PublicKey);
            Assert.AreEqual(true, Neo.SmartContract.Helper.IsSignatureContract(script));
            script[0] = 0x22;
            Assert.AreEqual(false, Neo.SmartContract.Helper.IsSignatureContract(script));
        }

        [TestMethod]
        public void TestIsStandardContract()
        {
            for (int j = 0; j < 2; j++)
            {
                if (j == 0)
                {
                    byte[] privateKey = new byte[32];
                    RandomNumberGenerator rng = RandomNumberGenerator.Create();
                    rng.GetBytes(privateKey);
                    KeyPair key = new KeyPair(privateKey);
                    byte[] script = Contract.CreateSignatureRedeemScript(key.PublicKey);
                    Assert.AreEqual(true, Neo.SmartContract.Helper.IsStandardContract(script));
                }
                else
                {
                    Neo.Cryptography.ECC.ECPoint[] publicKeys = new Neo.Cryptography.ECC.ECPoint[3];
                    for (int i = 0; i < 3; i++)
                    {
                        byte[] privateKey = new byte[32];
                        RandomNumberGenerator rng = RandomNumberGenerator.Create();
                        rng.GetBytes(privateKey);
                        KeyPair key = new KeyPair(privateKey);
                        publicKeys[i] = key.PublicKey;
                    }
                    byte[] script = Contract.CreateMultiSigRedeemScript(3, publicKeys);
                    Assert.AreEqual(true, Neo.SmartContract.Helper.IsStandardContract(script));
                }
            }
        }

        [TestMethod]
        public void TestSerialize()
        {
            for (int i = 0; i < 10; i++)
            {
                if (i == 0)
                {
                    StackItem stackItem = new ByteArray(new byte[5]);
                    byte[] result = Neo.SmartContract.Helper.Serialize(stackItem);
                    byte[] expectedArray = new byte[] {
                        0x00, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00
                    };
                    Assert.AreEqual(Encoding.Default.GetString(expectedArray), Encoding.Default.GetString(result));
                }
                else if (i == 1)
                {
                    StackItem stackItem = new VM.Types.Boolean(true);
                    byte[] result = Neo.SmartContract.Helper.Serialize(stackItem);
                    byte[] expectedArray = new byte[] {
                        0x01, 0x01
                    };
                    Assert.AreEqual(Encoding.Default.GetString(expectedArray), Encoding.Default.GetString(result));
                }
                else if (i == 2)
                {
                    StackItem stackItem = new VM.Types.Integer(1);
                    byte[] result = Neo.SmartContract.Helper.Serialize(stackItem);
                    byte[] expectedArray = new byte[] {
                        0x02, 0x01, 0x01
                    };
                    Assert.AreEqual(Encoding.Default.GetString(expectedArray), Encoding.Default.GetString(result));
                }
                else if (i == 3)
                {
                    StackItem stackItem = new InteropInterface<object>(new object());
                    Action action = () => Neo.SmartContract.Helper.Serialize(stackItem);
                    action.ShouldThrow<NotSupportedException>();
                }
                else if (i == 4)
                {
                    StackItem stackItem = new VM.Types.Integer(1);
                    byte[] result = Neo.SmartContract.Helper.Serialize(stackItem);
                    byte[] expectedArray = new byte[] {
                        0x02, 0x01, 0x01
                    };
                    Assert.AreEqual(Encoding.Default.GetString(expectedArray), Encoding.Default.GetString(result));
                }
                else if (i == 5)
                {
                    StackItem stackItem1 = new VM.Types.Integer(1);
                    List<StackItem> list = new List<StackItem>
                    {
                        stackItem1
                    };
                    StackItem stackItem2 = new VM.Types.Array(list);
                    byte[] result = Neo.SmartContract.Helper.Serialize(stackItem2);
                    byte[] expectedArray = new byte[] {
                        0x80,0x01,0x02,0x01,0x01
                    };
                    Assert.AreEqual(Encoding.Default.GetString(expectedArray), Encoding.Default.GetString(result));
                }
                else if (i == 6)
                {
                    StackItem stackItem1 = new VM.Types.Integer(1);
                    List<StackItem> list = new List<StackItem>
                    {
                        stackItem1
                    };
                    StackItem stackItem2 = new VM.Types.Struct(list);
                    byte[] result = Neo.SmartContract.Helper.Serialize(stackItem2);
                    byte[] expectedArray = new byte[] {
                        0x81,0x01,0x02,0x01,0x01
                    };
                    Assert.AreEqual(Encoding.Default.GetString(expectedArray), Encoding.Default.GetString(result));
                }
                else if (i == 7)
                {
                    StackItem stackItem1 = new VM.Types.Integer(1);
                    Dictionary<StackItem, StackItem> list = new Dictionary<StackItem, StackItem>
                    {
                        { new VM.Types.Integer(2), stackItem1 }
                    };
                    StackItem stackItem2 = new VM.Types.Map(list);
                    byte[] result = Neo.SmartContract.Helper.Serialize(stackItem2);
                    byte[] expectedArray = new byte[] {
                        0x82,0x01,0x02,0x01,0x02,0x02,0x01,0x01
                    };
                    Assert.AreEqual(Encoding.Default.GetString(expectedArray), Encoding.Default.GetString(result));
                }
                else if (i == 8)
                {
                    StackItem stackItem = new VM.Types.Integer(1);
                    Map stackItem1 = new VM.Types.Map();
                    stackItem1.Add(stackItem, stackItem1);
                    Action action = () => Neo.SmartContract.Helper.Serialize(stackItem1);
                    action.ShouldThrow<NotSupportedException>();
                }
                else
                {
                    VM.Types.Array stackItem = new VM.Types.Array();
                    stackItem.Add(stackItem);
                    Action action = () => Neo.SmartContract.Helper.Serialize(stackItem);
                    action.ShouldThrow<NotSupportedException>();
                }
            }
        }

        [TestMethod]
        public void TestToInteropMethodHash()
        {
            byte[] temp1 = Encoding.ASCII.GetBytes("AAAA");
            byte[] temp2 = Neo.Cryptography.Helper.Sha256(temp1);
            uint result = BitConverter.ToUInt32(temp2, 0);
            Assert.AreEqual(result, Neo.SmartContract.Helper.ToInteropMethodHash("AAAA"));
        }

        [TestMethod]
        public void TestToScriptHash()
        {
            byte[] temp1 = Encoding.ASCII.GetBytes("AAAA");
            byte[] temp2 = Neo.Cryptography.Helper.Sha256(temp1);
            uint result = BitConverter.ToUInt32(temp2, 0);
            Assert.AreEqual(result, Neo.SmartContract.Helper.ToInteropMethodHash("AAAA"));
        }
    }
}