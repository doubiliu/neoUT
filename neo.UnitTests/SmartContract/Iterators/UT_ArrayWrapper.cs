using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.SmartContract.Iterators;
using Neo.VM;
using Neo.VM.Types;
using System;
using System.Collections.Generic;

namespace Neo.UnitTests.SmartContract.Iterators
{
    [TestClass]
    public class UT_ArrayWrapper
    {
        [TestMethod]
        public void TestGeneratorAndDispose()
        {
            ArrayWrapper arrayWrapper = new ArrayWrapper(new List<StackItem>());
            Assert.IsNotNull(arrayWrapper);
            Action action = () => arrayWrapper.Dispose();
            action.ShouldNotThrow<Exception>();
        }

        [TestMethod]
        public void TestKeyAndValue()
        {
            List<StackItem> list = new List<StackItem>();
            StackItem stackItem = new Integer(0);
            list.Add(stackItem);
            ArrayWrapper arrayWrapper = new ArrayWrapper(list);
            Action action1 = () => arrayWrapper.Key();
            action1.ShouldThrow<InvalidOperationException>();
            Action action2 = () => arrayWrapper.Value();
            action2.ShouldThrow<InvalidOperationException>();
            arrayWrapper.Next();
            Assert.AreEqual(stackItem, arrayWrapper.Key());
            Assert.AreEqual(stackItem, arrayWrapper.Value());
        }

        [TestMethod]
        public void TestNext()
        {
            List<StackItem> list = new List<StackItem>();
            ArrayWrapper arrayWrapper = new ArrayWrapper(list);
            Assert.AreEqual(false, arrayWrapper.Next());
            StackItem stackItem = new Integer(0);
            list.Add(stackItem);
            Assert.AreEqual(true, arrayWrapper.Next());
        }
    }
}