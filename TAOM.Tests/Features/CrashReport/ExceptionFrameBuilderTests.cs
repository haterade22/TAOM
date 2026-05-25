using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CrashReport.Collectors;

namespace TAOM.Tests.Features.CrashReport;

[TestClass]
public class ExceptionFrameBuilderTests
{
    [TestMethod]
    public void Build_NullException_ReturnsNull()
    {
        var frame = ExceptionFrameBuilder.Build(null);
        Assert.IsNull(frame);
    }

    [TestMethod]
    public void Build_SimpleException_CapturesTypeAndMessage()
    {
        var ex = new InvalidOperationException("oops");

        var frame = ExceptionFrameBuilder.Build(ex);

        Assert.IsNotNull(frame);
        Assert.AreEqual("System.InvalidOperationException", frame!.Type);
        Assert.AreEqual("oops", frame.Message);
        Assert.IsNull(frame.InnerException);
    }

    [TestMethod]
    public void Build_NestedInnerExceptions_WalksChainUpToCap()
    {
        // Build a 15-deep chain; default cap is 10 — only first 10 should appear.
        Exception current = new Exception("level-14");
        for (int i = 13; i >= 0; i--)
            current = new Exception("level-" + i, current);

        var frame = ExceptionFrameBuilder.Build(current);

        Assert.IsNotNull(frame);
        int depth = 0;
        var cur = frame;
        while (cur != null) { depth++; cur = cur!.InnerException; }
        Assert.AreEqual(ExceptionFrameBuilder.DefaultMaxDepth, depth, "depth cap should be enforced");
    }

    [TestMethod]
    public void Build_DataDictionaryPopulated_CapturesEntries()
    {
        var ex = new Exception("x");
        ex.Data["foo"] = "bar";
        ex.Data["count"] = 42;

        var frame = ExceptionFrameBuilder.Build(ex);

        Assert.IsNotNull(frame);
        Assert.AreEqual(2, frame!.Data.Count);
    }

    [TestMethod]
    public void Build_ExplicitMaxDepth_RespectsOverride()
    {
        Exception current = new Exception("inner");
        for (int i = 0; i < 5; i++) current = new Exception("level-" + i, current);

        var frame = ExceptionFrameBuilder.Build(current, maxDepth: 2);

        Assert.IsNotNull(frame);
        Assert.IsNotNull(frame!.InnerException);
        Assert.IsNull(frame.InnerException!.InnerException, "should stop at depth 2");
    }
}
