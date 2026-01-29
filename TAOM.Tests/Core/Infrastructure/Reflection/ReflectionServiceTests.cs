using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using System;

namespace TAOM.Tests.Core.Infrastructure.Reflection;

[TestClass]
public class ReflectionServiceTests
{
    private ReflectionService _sut;
    private IModLogger _logger;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _sut = new ReflectionService(_logger);
    }

    [TestMethod]
    public void GetFieldValue_PrivateField_ReturnsValue()
    {
        var target = new TestTarget();

        var result = _sut.GetFieldValue<TestTarget, string>(target, "_privateField");

        Assert.AreEqual("private_value", result);
    }

    [TestMethod]
    public void GetFieldValue_PublicField_ReturnsValue()
    {
        var target = new TestTarget { PublicField = 42 };

        var result = _sut.GetFieldValue<TestTarget, int>(target, "PublicField");

        Assert.AreEqual(42, result);
    }

    [TestMethod]
    public void SetFieldValue_PrivateField_SetsValue()
    {
        var target = new TestTarget();

        _sut.SetFieldValue(target, "_privateField", "new_value");

        var result = _sut.GetFieldValue<TestTarget, string>(target, "_privateField");
        Assert.AreEqual("new_value", result);
    }

    [TestMethod]
    public void SetFieldValue_NonExistentField_ThrowsAndLogs()
    {
        var target = new TestTarget();

        Assert.ThrowsException<InvalidOperationException>(() =>
            _sut.SetFieldValue(target, "_nonexistent", "value"));

        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("_nonexistent")));
    }

    [TestMethod]
    public void GetPropertyValue_PublicProperty_ReturnsValue()
    {
        var target = new TestTarget();

        var result = _sut.GetPropertyValue<TestTarget, string>(target, "ReadOnlyProp");

        Assert.AreEqual("readonly_value", result);
    }

    [TestMethod]
    public void GetPropertyValue_NonGenericReturn_ReturnsObject()
    {
        var target = new TestTarget();

        var result = _sut.GetPropertyValue<TestTarget>(target, "ReadOnlyProp");

        Assert.AreEqual("readonly_value", result);
    }

    [TestMethod]
    public void SetPropertyValue_WritableProperty_SetsValue()
    {
        var target = new TestTarget();

        _sut.SetPropertyValue(target, "WritableProp", 99);

        Assert.AreEqual(99, target.WritableProp);
    }

    [TestMethod]
    public void SetPropertyValue_NonExistentProperty_ThrowsAndLogs()
    {
        var target = new TestTarget();

        Assert.ThrowsException<InvalidOperationException>(() =>
            _sut.SetPropertyValue(target, "DoesNotExist", "value"));

        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("DoesNotExist")));
    }

    [TestMethod]
    public void SetPropertyValue_ReadOnlyProperty_ThrowsAndLogs()
    {
        var target = new TestTarget();

        Assert.ThrowsException<InvalidOperationException>(() =>
            _sut.SetPropertyValue(target, "ReadOnlyProp", "value"));

        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("read-only")));
    }

    [TestMethod]
    public void CallPrivateMethod_ReturnsResult()
    {
        var target = new TestTarget();

        var result = _sut.CallPrivateMethod(target, "PrivateAdd", new object[] { 3, 4 });

        Assert.AreEqual(7, result);
    }

    [TestMethod]
    public void CallPrivateMethod_VoidMethod_ReturnsNull()
    {
        var target = new TestTarget();

        var result = _sut.CallPrivateMethod(target, "PrivateVoidMethod", new object[] { });

        Assert.IsNull(result);
    }

    [TestMethod]
    public void CallPrivateMethod_NonExistentMethod_ThrowsAndLogs()
    {
        var target = new TestTarget();

        Assert.ThrowsException<InvalidOperationException>(() =>
            _sut.CallPrivateMethod(target, "NoSuchMethod", new object[] { }));

        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("NoSuchMethod")));
    }

    [TestMethod]
    public void GetFieldValue_StaticField_ReturnsValue()
    {
        var result = _sut.GetFieldValue<TestTarget, int>(null, "StaticField");

        Assert.AreEqual(100, result);
    }

    internal class TestTarget
    {
        private string _privateField = "private_value";
        public int PublicField;
        internal static int StaticField = 100;

        public string ReadOnlyProp { get; } = "readonly_value";
        public int WritableProp { get; set; } = 0;

        private int PrivateAdd(int a, int b) => a + b;
        private void PrivateVoidMethod() { }
    }
}
