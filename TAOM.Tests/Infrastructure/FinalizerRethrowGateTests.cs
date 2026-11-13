using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Gate for #1155. A Harmony finalizer that returns a value makes Harmony end the wrapper with
/// <c>throw</c>, which replaces the exception's stack trace, so a finalizer that hands its
/// exception back must pass it through <c>RethrowStackPreserver.PreserveForRethrow</c>, and one
/// that only observes is <c>void</c> (<c>.claude/rules/harmony-patches.md</c>; lessons/harmony-il.md,
/// "A value-returning finalizer that hands back its exception erases the throw site"). Nine sites
/// did not, and a tenth arrived by copy (PR #1147).
///
/// The scan is by source text, keyed on the name <c>__exception</c>, over comment-stripped code. It
/// sees the direct spellings: a return, a parenthesised return, an expression body, either arm of a
/// conditional, with or without <c>!</c>, on one line or split over lines. It does NOT see a local
/// alias (<c>var e = __exception; return e;</c>), a helper that returns its argument, a
/// <c>throw __exception</c>, a cast (<c>(Exception)__exception</c>, <c>__exception as Exception</c>),
/// <c>__exception ?? x</c> or a switch-expression arm, and the statement inside a string literal is
/// a false hit. A review still has to read a new finalizer.
/// </summary>
[TestClass]
public class FinalizerRethrowGateTests
{
    private static readonly Regex BareHandBack = new(
        @"\breturn\s+__exception\s*!?\s*;" +                    // return __exception;
        @"|\breturn\s*\(\s*__exception\s*!?\s*\)\s*;" +         // return (__exception);
        @"|=>\s*__exception\s*!?\s*;" +                         // => __exception;
        @"|\?[^;?]*:\s*__exception\s*!?\s*;" +                  // c ? null : __exception;
        @"|\?\s*__exception\s*!?\s*:");                         // c ? __exception : null;

    private static readonly string[] Roots = { "Main", "Client", "Dependencies", "CoopCompat" };

    [TestMethod]
    public void NoFinalizerHandsItsExceptionBackBare()
    {
        var repoDir = RepoPaths.RepoPath();
        var offenders = new List<string>();

        foreach (var file in RepoPaths.CSharpFiles(Roots))
        {
            var raw = RepoPaths.Text(file);
            if (raw.IndexOf("__exception", System.StringComparison.Ordinal) < 0)
                continue;

            // StripComments keeps length and line breaks, so a match offset gives the real line.
            var code = RepoPaths.StripComments(raw);
            foreach (Match match in BareHandBack.Matches(code))
            {
                var line = code.Take(match.Index).Count(c => c == '\n') + 1;
                var rel = file.Substring(repoDir.Length).TrimStart('\\', '/').Replace('\\', '/');
                offenders.Add($"{rel}:{line}");
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "A finalizer hands __exception back bare, so Harmony's throw erases the throw site. Write " +
            "`return RethrowStackPreserver.PreserveForRethrow(__exception, null);`, or make a finalizer " +
            "that only observes `void`:\n  " + string.Join("\n  ", offenders));
    }

    [DataTestMethod]
    [DataRow("        return __exception;")]
    [DataRow("if (x) return  __exception ;")]
    [DataRow("return\n            __exception;")]
    [DataRow("return (__exception);")]
    [DataRow("return __exception!;")]
    [DataRow("static Exception F(Exception __exception) => __exception;")]
    [DataRow("return __exception is InvalidOperationException ? null : __exception;")]
    [DataRow("return swallow ? __exception : null;")]
    public void TheScan_FlagsABareHandBack(string code)
    {
        Assert.IsTrue(BareHandBack.IsMatch(RepoPaths.StripComments(code)), code);
    }

    [DataTestMethod]
    [DataRow("return RethrowStackPreserver.PreserveForRethrow(__exception, null);")]
    [DataRow("return ok ? null : RethrowStackPreserver.PreserveForRethrow(__exception, site);")]
    [DataRow("Report(hero, __exception);")]
    [DataRow("Log(ex: __exception);")]
    [DataRow("if (__exception == null) return null;")]
    [DataRow("// it used to say: return __exception;")]
    [DataRow("/// <c>return __exception;</c> erases the trace")]
    [DataRow("/* return __exception; */ return null;")]
    public void TheScan_LeavesOtherUsesAlone(string code)
    {
        Assert.IsFalse(BareHandBack.IsMatch(RepoPaths.StripComments(code)), code);
    }
}
