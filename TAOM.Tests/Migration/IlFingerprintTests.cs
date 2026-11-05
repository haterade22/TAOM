using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Migration;

/// <summary>
/// <see cref="IlCallScanner.Fingerprint"/> must tell apart two bodies that call the same-named methods but would behave
/// differently (convergence review of plan 042): a changed constant argument, another overload of the same name, the
/// same member on a list of another element type, another comparison. Each pair below differs in exactly one of those.
/// Runs on the test assembly's own methods, so no game is needed.
/// </summary>
[TestClass]
public class IlFingerprintTests
{
    [DataTestMethod]
    [DataRow(nameof(PassesFalse), nameof(PassesTrue), DisplayName = "constant argument (keepDuplicates false to true)")]
    [DataRow(nameof(LoopFromOne), nameof(LoopFromZero), DisplayName = "loop start index")]
    [DataRow(nameof(ComparesToEmpty), nameof(ComparesToOther), DisplayName = "string literal")]
    [DataRow(nameof(CallsObjectOverload), nameof(CallsStringOverload), DisplayName = "same-name overload")]
    [DataRow(nameof(CountsStrings), nameof(CountsTuples), DisplayName = "generic list element type")]
    [DataRow(nameof(LessThan), nameof(LessOrEqual), DisplayName = "comparison")]
    public void BodiesThatDifferInOneOperand_HaveDifferentFingerprints(string first, string second)
    {
        var a = Of(first);
        var b = Of(second);

        Assert.AreNotEqual(0, a.Length, first + " fingerprinted to nothing; the scan failed.");
        Assert.IsFalse(a.SequenceEqual(b),
            first + " and " + second + " differ in one operand but fingerprint the same: { " + string.Join(", ", a) + " }");
    }

    [TestMethod]
    public void Fingerprint_NamesTheCallWithItsConstructedTypeAndParameters_AndTheConstantsThatFeedIt()
    {
        var tokens = Of(nameof(PassesFalse));

        CollectionAssert.AreEqual(new[] { "ldc.i4 0", "call IlFingerprintTests::Sink(Boolean)" }, tokens);
        CollectionAssert.Contains(Of(nameof(CountsTuples)), "callvirt List<Tuple<String,String>>::get_Count()");
    }

    [TestMethod]
    public void Fingerprint_OfTheSameBody_IsStable()
    {
        CollectionAssert.AreEqual(Of(nameof(LoopFromOne)), Of(nameof(LoopFromOne)));
    }

    private static string[] Of(string name)
    {
        var method = typeof(IlFingerprintTests).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;
        return IlCallScanner.Fingerprint(method, method.GetMethodBody()!.GetILAsByteArray()).ToArray();
    }

    private static void Sink(bool value)
    {
    }

    private static void Over(object value)
    {
    }

    private static void Over(string value)
    {
    }

    private static void PassesFalse() => Sink(false);

    private static void PassesTrue() => Sink(true);

    private static int LoopFromOne(List<string> items)
    {
        var n = 0;
        for (var i = 1; i < items.Count; i++)
            n++;
        return n;
    }

    private static int LoopFromZero(List<string> items)
    {
        var n = 0;
        for (var i = 0; i < items.Count; i++)
            n++;
        return n;
    }

    private static bool ComparesToEmpty(string value) => value != "";

    private static bool ComparesToOther(string value) => value != " ";

    private static void CallsObjectOverload(string value) => Over((object)value);

    private static void CallsStringOverload(string value) => Over(value);

    private static int CountsStrings(List<string> items) => items.Count;

    private static int CountsTuples(List<Tuple<string, string>> items) => items.Count;

    private static bool LessThan(int a, int b) => a < b;

    private static bool LessOrEqual(int a, int b) => a <= b;
}
