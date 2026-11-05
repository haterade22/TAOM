using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;

namespace TAOM.Tests.Features.TextureAdvice;

/// <summary>The marker file half of the adapter, the only part that runs without the engine.</summary>
[TestClass]
public class TextureAdviceAdapterTests
{
    private string _root = null!;
    private string _marker = null!;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "taom-tests-" + Guid.NewGuid().ToString("N"));
        _marker = Path.Combine(_root, "sub", "marker");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    public void WasAdviceShown_NoMarker_IsFalse()
        => Assert.IsFalse(new TextureAdviceAdapter(_marker).WasAdviceShown());

    [TestMethod]
    public void RememberAdviceShown_CreatesTheFolderAndTheMarker()
    {
        var sut = new TextureAdviceAdapter(_marker);

        sut.RememberAdviceShown();

        Assert.IsTrue(File.Exists(_marker));
        Assert.IsTrue(sut.WasAdviceShown());
    }
}
