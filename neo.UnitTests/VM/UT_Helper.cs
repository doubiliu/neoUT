using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Cryptography.ECC;
using Neo.SmartContract;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Neo.UnitTests.IO
{
    [TestClass]
    public class UT_Helper
    {
        [TestMethod]
        public void TestEmit()
        {
            ScriptBuilder sb = new ScriptBuilder();
            sb.Emit(new OpCode[] { OpCode.PUSH0 });
            Assert.AreEqual(Encoding.Default.GetString(new byte[] { 0x00 }), Encoding.Default.GetString(sb.ToArray()));
        }

        [TestMethod]
        public void TestEmitAppCall1()
        {
            //format:(byte)0x00+(byte)OpCode.NEWARRAY+(string)operation+(Uint160)scriptHash+(uint)InteropService.System_Contract_Call
            ScriptBuilder sb = new ScriptBuilder();
            sb.EmitAppCall(UInt160.Zero, "AAAAA");
            byte[] tempArray = new byte[34];
            tempArray[0] = 0x00;//0
            tempArray[1] = 0xC5;//OpCode.NEWARRAY 
            tempArray[2] = 5;//operation.Length
            Array.Copy(Encoding.UTF8.GetBytes("AAAAA"), 0, tempArray, 3, 5);//operation.data
            tempArray[8] = 0x14;//scriptHash.Length
            Array.Copy(UInt160.Zero.ToArray(), 0, tempArray, 9, 20);//operation.data
            uint api = InteropService.System_Contract_Call;
            tempArray[29] = 0x68;//OpCode.SYSCALL
            Array.Copy(BitConverter.GetBytes(api), 0, tempArray, 30, 4);//api.data
            byte[] resultArray = sb.ToArray();
            Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(resultArray));
        }

        [TestMethod]
        public void TestEmitAppCall2()
        {
            //format:(ContractParameter[])ContractParameter+(byte)OpCode.PACK+(string)operation+(Uint160)scriptHash+(uint)InteropService.System_Contract_Call
            ScriptBuilder sb = new ScriptBuilder();
            sb.EmitAppCall(UInt160.Zero, "AAAAA", new ContractParameter[] { new ContractParameter(ContractParameterType.Integer) });
            byte[] tempArray = new byte[35];
            tempArray[0] = 0x00;//0
            tempArray[1] = 0x51;//ContractParameter.Length 
            tempArray[2] = 0xC1;//OpCode.PACK
            tempArray[3] = 0x05;//operation.Length
            Array.Copy(Encoding.UTF8.GetBytes("AAAAA"), 0, tempArray, 4, 5);//operation.data
            tempArray[9] = 0x14;//scriptHash.Length
            Array.Copy(UInt160.Zero.ToArray(), 0, tempArray, 10, 20);//operation.data
            uint api = InteropService.System_Contract_Call;
            tempArray[30] = 0x68;//OpCode.SYSCALL
            Array.Copy(BitConverter.GetBytes(api), 0, tempArray, 31, 4);//api.data
            byte[] resultArray = sb.ToArray();
            Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(resultArray));
        }

        [TestMethod]
        public void TestEmitAppCall3()
        {
            //format:(object[])args+(byte)OpCode.PACK+(string)operation+(Uint160)scriptHash+(uint)InteropService.System_Contract_Call
            ScriptBuilder sb = new ScriptBuilder();
            sb.EmitAppCall(UInt160.Zero, "AAAAA", true);
            byte[] tempArray = new byte[35];
            tempArray[0] = 0x51;//arg
            tempArray[1] = 0x51;//args.Length 
            tempArray[2] = 0xC1;//OpCode.PACK
            tempArray[3] = 0x05;//operation.Length
            Array.Copy(Encoding.UTF8.GetBytes("AAAAA"), 0, tempArray, 4, 5);//operation.data
            tempArray[9] = 0x14;//scriptHash.Length
            Array.Copy(UInt160.Zero.ToArray(), 0, tempArray, 10, 20);//operation.data
            uint api = InteropService.System_Contract_Call;
            tempArray[30] = 0x68;//OpCode.SYSCALL
            Array.Copy(BitConverter.GetBytes(api), 0, tempArray, 31, 4);//api.data
            byte[] resultArray = sb.ToArray();
            Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(resultArray));
        }

        [TestMethod]
        public void TestEmitPush1()
        {
            ScriptBuilder sb = new ScriptBuilder();
            sb.EmitPush(UInt160.Zero);
            byte[] tempArray = new byte[21];
            tempArray[0] = 0x14;
            Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
        }

        [TestMethod]
        public void TestEmitPush2()
        {
            for (int i = 0; i < 11; i++)
            {
                if (i == 0)//Signature
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    sb.EmitPush(new ContractParameter(ContractParameterType.Signature));
                    byte[] tempArray = new byte[65];
                    tempArray[0] = 0x40;
                    Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
                }
                else if (i == 1)//ByteArray
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    sb.EmitPush(new ContractParameter(ContractParameterType.ByteArray));
                    byte[] tempArray = new byte[1];
                    tempArray[0] = 0x00;
                    Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
                }
                else if (i == 2)//Boolean
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    sb.EmitPush(new ContractParameter(ContractParameterType.Boolean));
                    byte[] tempArray = new byte[1];
                    tempArray[0] = 0x00;
                    Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
                }
                else if (i == 3)//Integer
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    ContractParameter parameter = new ContractParameter(ContractParameterType.Integer);
                    sb.EmitPush(parameter);
                    byte[] tempArray = new byte[1];
                    tempArray[0] = 0x00;
                    Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
                }
                else if (i == 4)//Integer BigInteger
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    ContractParameter parameter = new ContractParameter(ContractParameterType.Integer)
                    {
                        Value = BigInteger.Zero
                    };
                    sb.EmitPush(parameter);
                    byte[] tempArray = new byte[1];
                    tempArray[0] = 0x00;
                    Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
                }
                else if (i == 5)//Hash160
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    sb.EmitPush(new ContractParameter(ContractParameterType.Hash160));
                    byte[] tempArray = new byte[21];
                    tempArray[0] = 0x14;
                    Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
                }
                else if (i == 6)//Hash256
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    sb.EmitPush(new ContractParameter(ContractParameterType.Hash256));
                    byte[] tempArray = new byte[33];
                    tempArray[0] = 0x20;
                    Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
                }
                else if (i == 7)//PublicKey
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    sb.EmitPush(new ContractParameter(ContractParameterType.PublicKey));
                    byte[] tempArray = new byte[34];
                    tempArray[0] = 0x21;
                    Array.Copy(ECCurve.Secp256r1.G.EncodePoint(true), 0, tempArray, 1, 33);
                    Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
                }
                else if (i == 8)//String
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    sb.EmitPush(new ContractParameter(ContractParameterType.String));
                    byte[] tempArray = new byte[1];
                    Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
                }
                else if (i == 9)//Array
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    ContractParameter parameter = new ContractParameter(ContractParameterType.Array);
                    IList<ContractParameter> values = new List<ContractParameter>();
                    values.Add(new ContractParameter(ContractParameterType.Integer));
                    values.Add(new ContractParameter(ContractParameterType.Integer));
                    parameter.Value = values;
                    sb.EmitPush(parameter);
                    byte[] tempArray = new byte[4];
                    tempArray[0] = 0x00;
                    tempArray[1] = 0x00;
                    tempArray[2] = 0x52;
                    tempArray[3] = 0xC1;
                    Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
                }
                else//default
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    Action action = () => sb.EmitPush(new ContractParameter(ContractParameterType.Map));
                    action.ShouldThrow<ArgumentException>();
                }
            }
        }

        enum TestEnum : byte
        {
            case1 = 0
        }

        [TestMethod]
        public void TestEmitPush3()
        {
            for (int i = 0; i < 15; i++)
            {
                if (i == 0)//bool
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    sb.EmitPush(true);
                    byte[] tempArray = new byte[1];
                    tempArray[0] = 0x51;
                    Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
                }
                else if (i == 1)//byte[]
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    sb.EmitPush(new byte[] { 0x01 });
                    byte[] tempArray = new byte[2];
                    tempArray[0] = 0x01;
                    tempArray[1] = 0x01;
                    Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
                }
                else if (i == 2)//string
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    sb.EmitPush("");
                    byte[] tempArray = new byte[1];
                    tempArray[0] = 0x00;
                    Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
                }
                else if (i == 3)//BigInteger
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    sb.EmitPush(BigInteger.Zero);
                    byte[] tempArray = new byte[1];
                    tempArray[0] = 0x00;
                    Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
                }
                else if (i == 4)//ISerializable
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    sb.EmitPush(UInt160.Zero);
                    byte[] tempArray = new byte[21];
                    tempArray[0] = 0x14;
                    Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
                }
                else if (i == 5)//sbyte
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    sbyte temp = 0;
                    VM.Helper.EmitPush(sb, temp);
                    byte[] tempArray = new byte[1];
                    tempArray[0] = 0x00;
                    Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
                }
                else if (i == 6)//byte
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    byte temp = 0;
                    VM.Helper.EmitPush(sb, temp);
                    byte[] tempArray = new byte[1];
                    tempArray[0] = 0x00;
                    Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
                }
                else if (i == 7)//short
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    short temp = 0;
                    VM.Helper.EmitPush(sb, temp);
                    byte[] tempArray = new byte[1];
                    tempArray[0] = 0x00;
                    Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
                }
                else if (i == 8)//ushort
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    ushort temp = 0;
                    VM.Helper.EmitPush(sb, temp);
                    byte[] tempArray = new byte[1];
                    tempArray[0] = 0x00;
                    Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
                }
                else if (i == 9)//int
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    int temp = 0;
                    VM.Helper.EmitPush(sb, temp);
                    byte[] tempArray = new byte[1];
                    tempArray[0] = 0x00;
                    Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
                }
                else if (i == 10)//uint
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    uint temp = 0;
                    VM.Helper.EmitPush(sb, temp);
                    byte[] tempArray = new byte[1];
                    tempArray[0] = 0x00;
                    Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
                }
                else if (i == 11)//long
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    long temp = 0;
                    VM.Helper.EmitPush(sb, temp);
                    byte[] tempArray = new byte[1];
                    tempArray[0] = 0x00;
                    Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
                }
                else if (i == 12)//ulong
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    ulong temp = 0;
                    VM.Helper.EmitPush(sb, temp);
                    byte[] tempArray = new byte[1];
                    tempArray[0] = 0x00;
                    Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
                }
                else if (i == 13)//Enum
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    sb.EmitPush(TestEnum.case1);
                    byte[] tempArray = new byte[1];
                    tempArray[0] = 0x00;
                    Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
                }
                else //default
                {
                    ScriptBuilder sb = new ScriptBuilder();
                    Action action = () => sb.EmitPush(new object());
                    action.ShouldThrow<ArgumentException>();
                }
            }
        }

        [TestMethod]
        public void TestEmitSysCall()
        {
            ScriptBuilder sb = new ScriptBuilder();
            sb.EmitSysCall(0, true);
            byte[] tempArray = new byte[6];
            tempArray[0] = 0x51;
            tempArray[1] = 0x68;
            tempArray[2] = 0x00;
            tempArray[3] = 0x00;
            tempArray[4] = 0x00;
            tempArray[5] = 0x00;
            Assert.AreEqual(Encoding.Default.GetString(tempArray), Encoding.Default.GetString(sb.ToArray()));
        }

        [TestMethod]
        public void TestToParameter()
        {
            for (int i = 0; i < 7; i++)
            {
                if (i == 0)//VMArray
                {
                    VM.Types.Array item = new VM.Types.Array();
                    ContractParameter parameter = VM.Helper.ToParameter(item);
                    Assert.AreEqual(ContractParameterType.Array, parameter.Type);
                    Assert.AreEqual(0, ((List<ContractParameter>)parameter.Value).Count);
                }
                else if (i == 1)//Map
                {
                    StackItem item = new VM.Types.Map();
                    ContractParameter parameter = VM.Helper.ToParameter(item);
                    Assert.AreEqual(ContractParameterType.Map, parameter.Type);
                    Assert.AreEqual(0, ((List<KeyValuePair<ContractParameter, ContractParameter>>)parameter.Value).Count);
                }
                else if (i == 2)//VMBoolean
                {
                    StackItem item = new VM.Types.Boolean(true);
                    ContractParameter parameter = VM.Helper.ToParameter(item);
                    Assert.AreEqual(ContractParameterType.Boolean, parameter.Type);
                    Assert.AreEqual(true, parameter.Value);
                }
                else if (i == 3)//ByteArray
                {
                    StackItem item = new VM.Types.ByteArray(new byte[] { 0x00 });
                    ContractParameter parameter = VM.Helper.ToParameter(item);
                    Assert.AreEqual(ContractParameterType.ByteArray, parameter.Type);
                    Assert.AreEqual(Encoding.Default.GetString(new byte[] { 0x00 }), Encoding.Default.GetString((byte[])parameter.Value));
                }
                else if (i == 4)//Integer
                {
                    StackItem item = new VM.Types.Integer(0);
                    ContractParameter parameter = VM.Helper.ToParameter(item);
                    Assert.AreEqual(ContractParameterType.Integer, parameter.Type);
                    Assert.AreEqual(BigInteger.Zero, parameter.Value);
                }
                else if (i == 5)//InteropInterface
                {
                    StackItem item = new VM.Types.InteropInterface<VM.Types.Boolean>(new VM.Types.Boolean(true));
                    ContractParameter parameter = VM.Helper.ToParameter(item);
                    Assert.AreEqual(ContractParameterType.InteropInterface, parameter.Type);
                }
                else //default
                {
                    Action action = () => VM.Helper.ToParameter(null);
                    action.ShouldThrow<ArgumentException>();
                }
            }
        }
    }
}
