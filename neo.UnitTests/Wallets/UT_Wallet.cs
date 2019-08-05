﻿using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Cryptography.ECC;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.SmartContract.Native.Tokens;
using Neo.UnitTests.Cryptography;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace Neo.UnitTests.Wallets
{
    internal class MyWallet : Wallet
    {
        public override string Name => "MyWallet";

        public override Version Version => Version.Parse("0.0.1");

        Dictionary<UInt160, WalletAccount> accounts = new Dictionary<UInt160, WalletAccount>();

        public override bool Contains(UInt160 scriptHash)
        {
            return accounts.ContainsKey(scriptHash);
        }

        public void AddAccount(WalletAccount account)
        {
            accounts.Add(account.ScriptHash, account);
        }

        public override WalletAccount CreateAccount(byte[] privateKey)
        {
            KeyPair key = new KeyPair(privateKey);
            Neo.Wallets.SQLite.VerificationContract contract = new Neo.Wallets.SQLite.VerificationContract
            {
                Script = Contract.CreateSignatureRedeemScript(key.PublicKey),
                ParameterList = new[] { ContractParameterType.Signature }
            };
            MyWalletAccount account = new MyWalletAccount(contract.ScriptHash);
            account.SetKey(key);
            account.Contract = contract;
            AddAccount(account);
            return account;
        }

        public override WalletAccount CreateAccount(Contract contract, KeyPair key = null)
        {
            MyWalletAccount account = new MyWalletAccount(contract.ScriptHash)
            {
                Contract = contract
            };
            account.SetKey(key);
            AddAccount(account);
            return account;
        }

        public override WalletAccount CreateAccount(UInt160 scriptHash)
        {
            MyWalletAccount account = new MyWalletAccount(scriptHash);
            AddAccount(account);
            return account;
        }

        public override bool DeleteAccount(UInt160 scriptHash)
        {
            return accounts.Remove(scriptHash);
        }

        public override WalletAccount GetAccount(UInt160 scriptHash)
        {
            accounts.TryGetValue(scriptHash, out WalletAccount account);
            return account;
        }

        public override IEnumerable<WalletAccount> GetAccounts()
        {
            return accounts.Values;
        }

        public override bool VerifyPassword(string password)
        {
            return true;
        }
    }

    [TestClass]
    public class UT_Wallet
    {
        Store store;

        [TestInitialize]
        public void TestSetup()
        {
            store = TestBlockchain.GetStore();
        }

        [TestMethod]
        public void TestContains()
        {
            MyWallet wallet = new MyWallet();
            Action action = () => wallet.Contains(UInt160.Zero);
            action.ShouldNotThrow();
        }

        [TestMethod]
        public void TestCreateAccount1()
        {
            MyWallet wallet = new MyWallet();
            wallet.CreateAccount(new byte[32]).Should().NotBeNull();
        }

        [TestMethod]
        public void TestCreateAccount2()
        {
            MyWallet wallet = new MyWallet();
            Contract contract = Contract.Create(new ContractParameterType[] { ContractParameterType.Boolean }, new byte[] { 1 });
            WalletAccount account = wallet.CreateAccount(contract, UT_Crypto.generateCertainKey(32).PrivateKey);
            account.Should().NotBeNull();

            wallet = new MyWallet();
            account = wallet.CreateAccount(contract, (byte[])(null));
            account.Should().NotBeNull();
        }

        [TestMethod]
        public void TestCreateAccount3()
        {
            MyWallet wallet = new MyWallet();
            Contract contract = Contract.Create(new ContractParameterType[] { ContractParameterType.Boolean }, new byte[] { 1 });
            wallet.CreateAccount(contract, UT_Crypto.generateCertainKey(32)).Should().NotBeNull();
        }

        [TestMethod]
        public void TestCreateAccount4()
        {
            MyWallet wallet = new MyWallet();
            wallet.CreateAccount(UInt160.Zero).Should().NotBeNull();
        }

        [TestMethod]
        public void TestGetName()
        {
            MyWallet wallet = new MyWallet();
            wallet.Name.Should().Be("MyWallet");
        }

        [TestMethod]
        public void TestGetVersion()
        {
            MyWallet wallet = new MyWallet();
            wallet.Version.Should().Be(Version.Parse("0.0.1"));
        }

        [TestMethod]
        public void TestGetAccount1()
        {
            MyWallet wallet = new MyWallet();
            wallet.CreateAccount(UInt160.Parse("522a2b818c308c7a2c77cfdda11763fe043bfb40"));
            WalletAccount account = wallet.GetAccount(ECCurve.Secp256r1.G);
            account.ScriptHash.Should().Be(UInt160.Parse("0x522a2b818c308c7a2c77cfdda11763fe043bfb40"));
        }

        [TestMethod]
        public void TestGetAccount2()
        {
            MyWallet wallet = new MyWallet();
            Action action = () => wallet.GetAccount(UInt160.Zero);
            action.ShouldNotThrow();
        }

        [TestMethod]
        public void TestGetAccounts()
        {
            MyWallet wallet = new MyWallet();
            Action action = () => wallet.GetAccounts();
            action.ShouldNotThrow();
        }

        [TestMethod]
        public void TestGetAvailable()
        {
            MyWallet wallet = new MyWallet();
            Contract contract = Contract.Create(new ContractParameterType[] { ContractParameterType.Boolean }, new byte[] { 1 });
            WalletAccount account = wallet.CreateAccount(contract, UT_Crypto.generateCertainKey(32).PrivateKey);
            account.Lock = false;

            // Fake balance
            var snapshot = store.GetSnapshot();
            var key = NativeContract.GAS.CreateStorageKey(20, account.ScriptHash);
            var entry = snapshot.Storages.GetAndChange(key, () => new StorageItem
            {
                Value = new Nep5AccountState().ToByteArray()
            });
            entry.Value = new Nep5AccountState()
            {
                Balance = 10000 * NativeContract.GAS.Factor
            }
            .ToByteArray();

            wallet.GetAvailable(NativeContract.GAS.Hash).Should().Be(new BigDecimal(1000000000000, 8));

            entry.Value = new Nep5AccountState()
            {
                Balance = 0
            }
            .ToByteArray();
        }

        [TestMethod]
        public void TestGetBalance()
        {
            MyWallet wallet = new MyWallet();
            Contract contract = Contract.Create(new ContractParameterType[] { ContractParameterType.Boolean }, new byte[] { 1 });
            WalletAccount account = wallet.CreateAccount(contract, UT_Crypto.generateCertainKey(32).PrivateKey);
            account.Lock = false;

            // Fake balance
            var snapshot = store.GetSnapshot();
            var key = NativeContract.GAS.CreateStorageKey(20, account.ScriptHash);
            var entry = snapshot.Storages.GetAndChange(key, () => new StorageItem
            {
                Value = new Nep5AccountState().ToByteArray()
            });
            entry.Value = new Nep5AccountState()
            {
                Balance = 10000 * NativeContract.GAS.Factor
            }
            .ToByteArray();

            wallet.GetBalance(UInt160.Zero, new UInt160[] { account.ScriptHash }).Should().Be(new BigDecimal(0, 0));
            wallet.GetBalance(NativeContract.GAS.Hash, new UInt160[] { account.ScriptHash }).Should().Be(new BigDecimal(1000000000000, 8));

            entry.Value = new Nep5AccountState()
            {
                Balance = 0
            }
            .ToByteArray();
        }

        [TestMethod]
        public void TestGetPrivateKeyFromWIF()
        {
            Action action = () => Wallet.GetPrivateKeyFromWIF(null);
            action.ShouldThrow<ArgumentNullException>();

            action = () => Wallet.GetPrivateKeyFromWIF("3vQB7B6MrGQZaxCuFg4oh");
            action.ShouldThrow<FormatException>();

            Wallet.GetPrivateKeyFromWIF("L3tgppXLgdaeqSGSFw1Go3skBiy8vQAM7YMXvTHsKQtE16PBncSU").Should().BeEquivalentTo(new byte[] { 199, 19, 77, 111, 216, 231, 61, 129, 158, 130, 117, 92, 100, 201, 55, 136, 216, 219, 9, 97, 146, 158, 2, 90, 83, 54, 60, 76, 192, 42, 105, 98 });
        }

        [TestMethod]
        public void TestImport1()
        {
            MyWallet wallet = new MyWallet();
            wallet.Import("L3tgppXLgdaeqSGSFw1Go3skBiy8vQAM7YMXvTHsKQtE16PBncSU").Should().NotBeNull();
        }

        [TestMethod]
        public void TestImport3()
        {
            MyWallet wallet = new MyWallet();
            X509Certificate2 cert = new X509Certificate2(@"C:\cer\11.p12", "123456");
            wallet.Import(cert).Should().NotBeNull();
        }

        [TestMethod]
        public void TestMakeTransaction1()
        {
            MyWallet wallet = new MyWallet();
            Contract contract = Contract.Create(new ContractParameterType[] { ContractParameterType.Boolean }, new byte[] { 1 });
            WalletAccount account = wallet.CreateAccount(contract, UT_Crypto.generateCertainKey(32).PrivateKey);
            account.Lock = false;

            Action action = () => wallet.MakeTransaction(new TransferOutput[]
            {
                new TransferOutput()
                {
                     AssetId = NativeContract.GAS.Hash,
                     ScriptHash = account.ScriptHash,
                     Value = new BigDecimal(1,8)
                }
            }, UInt160.Zero);
            action.ShouldThrow<ArgumentException>();

            action = () => wallet.MakeTransaction(new TransferOutput[]
            {
                new TransferOutput()
                {
                     AssetId = NativeContract.GAS.Hash,
                     ScriptHash = account.ScriptHash,
                     Value = new BigDecimal(1,8)
                }
            }, account.ScriptHash);
            action.ShouldThrow<InvalidOperationException>();

            action = () => wallet.MakeTransaction(new TransferOutput[]
            {
                new TransferOutput()
                {
                     AssetId = UInt160.Zero,
                     ScriptHash = account.ScriptHash,
                     Value = new BigDecimal(1,8)
                }
            }, account.ScriptHash);
            action.ShouldThrow<InvalidOperationException>();

            // Fake balance
            var snapshot = store.GetSnapshot();
            var key = NativeContract.GAS.CreateStorageKey(20, account.ScriptHash);
            var entry = snapshot.Storages.GetAndChange(key, () => new StorageItem
            {
                Value = new Nep5AccountState().ToByteArray()
            });
            entry.Value = new Nep5AccountState()
            {
                Balance = 10000 * NativeContract.GAS.Factor
            }
            .ToByteArray();

            key = NativeContract.NEO.CreateStorageKey(20, account.ScriptHash);
            entry = snapshot.Storages.GetAndChange(key, () => new StorageItem
            {
                Value = new Nep5AccountState().ToByteArray()
            });
            entry.Value = new NeoToken.AccountState()
            {
                Balance = 10000 * NativeContract.NEO.Factor
            }
            .ToByteArray();

            var tx = wallet.MakeTransaction(new TransferOutput[]
            {
                new TransferOutput()
                {
                     AssetId = NativeContract.GAS.Hash,
                     ScriptHash = account.ScriptHash,
                     Value = new BigDecimal(1,8)
                }
            });
            tx.Should().NotBeNull();

            tx = wallet.MakeTransaction(new TransferOutput[]
            {
                new TransferOutput()
                {
                     AssetId = NativeContract.NEO.Hash,
                     ScriptHash = account.ScriptHash,
                     Value = new BigDecimal(1,8)
                }
            });
            tx.Should().NotBeNull();

            entry.Value = new NeoToken.AccountState()
            {
                Balance = 0
            }
            .ToByteArray();
        }

        [TestMethod]
        public void TestMakeTransaction2()
        {
            MyWallet wallet = new MyWallet();
            Action action = () => wallet.MakeTransaction(new TransactionAttribute[] { }, new byte[] { }, UInt160.Zero);
            action.ShouldThrow<ArgumentException>();

            Contract contract = Contract.Create(new ContractParameterType[] { ContractParameterType.Boolean }, new byte[] { 1 });
            WalletAccount account = wallet.CreateAccount(contract, UT_Crypto.generateCertainKey(32).PrivateKey);
            account.Lock = false;

            // Fake balance
            var snapshot = store.GetSnapshot();
            var key = NativeContract.GAS.CreateStorageKey(20, account.ScriptHash);
            var entry = snapshot.Storages.GetAndChange(key, () => new StorageItem
            {
                Value = new Nep5AccountState().ToByteArray()
            });
            entry.Value = new Nep5AccountState()
            {
                Balance = 1000000 * NativeContract.GAS.Factor
            }
            .ToByteArray();

            var tx = wallet.MakeTransaction(new TransactionAttribute[] { }, new byte[] { }, account.ScriptHash);
            tx.Should().NotBeNull();

            tx = wallet.MakeTransaction(new TransactionAttribute[] { }, new byte[] { });
            tx.Should().NotBeNull();

            entry.Value = new NeoToken.AccountState()
            {
                Balance = 0
            }
            .ToByteArray();
        }

        [TestMethod]
        public void TestVerifyPassword()
        {
            MyWallet wallet = new MyWallet();
            Action action = () => wallet.VerifyPassword("Test");
            action.ShouldNotThrow();
        }
    }
}