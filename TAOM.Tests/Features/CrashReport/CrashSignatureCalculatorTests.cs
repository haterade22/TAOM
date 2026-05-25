using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CrashReport.Collectors;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Tests.Features.CrashReport;

[TestClass]
public class CrashSignatureCalculatorTests
{
    [TestMethod]
    public void Compute_SameInputs_ReturnsSameSignature()
    {
        var stack = MakeStack("A", "B", "C");

        var s1 = CrashSignatureCalculator.Compute("System.Exception", "Foo.Bar", stack);
        var s2 = CrashSignatureCalculator.Compute("System.Exception", "Foo.Bar", stack);

        Assert.AreEqual(s1, s2);
    }

    [TestMethod]
    public void Compute_DifferentExceptionType_ReturnsDifferentSignature()
    {
        var stack = MakeStack("A", "B");

        var s1 = CrashSignatureCalculator.Compute("System.Exception", "Foo.Bar", stack);
        var s2 = CrashSignatureCalculator.Compute("System.NullReferenceException", "Foo.Bar", stack);

        Assert.AreNotEqual(s1, s2);
    }

    [TestMethod]
    public void Compute_DifferentOriginatingTarget_ReturnsDifferentSignature()
    {
        var stack = MakeStack("A", "B");

        var s1 = CrashSignatureCalculator.Compute("System.Exception", "Foo.Bar", stack);
        var s2 = CrashSignatureCalculator.Compute("System.Exception", "Other.Method", stack);

        Assert.AreNotEqual(s1, s2);
    }

    [TestMethod]
    public void Compute_OnlyTopFiveFramesMatter_ChangesBelowDepthIgnored()
    {
        var stackA = MakeStack("A", "B", "C", "D", "E", "X-different-but-deep");
        var stackB = MakeStack("A", "B", "C", "D", "E", "Y-different-but-deep");

        var sA = CrashSignatureCalculator.Compute("Ex", "Origin", stackA);
        var sB = CrashSignatureCalculator.Compute("Ex", "Origin", stackB);

        Assert.AreEqual(sA, sB, "frames beyond index 4 should not contribute to signature");
    }

    [TestMethod]
    public void Short_PrefixesTo8Chars()
    {
        var full = "abcdef0123456789";
        Assert.AreEqual("abcdef01", CrashSignatureCalculator.Short(full));
    }

    private static IReadOnlyList<StackFrameSnapshot> MakeStack(params string[] methodNames)
    {
        var list = new List<StackFrameSnapshot>(methodNames.Length);
        for (int i = 0; i < methodNames.Length; i++)
            list.Add(new StackFrameSnapshot(i, methodNames[i], null, null, null, 0, 0));
        return list;
    }
}
