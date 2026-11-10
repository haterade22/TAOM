using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.CulturalFeats;

/// <summary>
/// Bannerlord v1.5.7 put <c>bool isCurrentlyAtSea</c> in front of <c>bool includeDescriptions</c> in
/// <c>PartySizeLimitModel.GetPartyMemberSizeLimit</c> (#1112). A call written for v1.5.6 as
/// <c>(party, true)</c> still compiles, and it now asks for the at-sea limit with no descriptions.
/// The rule here: no <c>true</c> or <c>false</c> literal goes to that method by position. A bool
/// variable and <c>default</c> are outside the rule. The scanner blanks comments and string and
/// character literals, then reads balanced brackets, so a nested call in an argument is still seen
/// (Codex pre-review F1, 2026-11-10). It does not read raw string literals or interpolation holes.
/// </summary>
[TestClass]
public class PartyMemberSizeLimitCallShapeTests
{
    private const string Method = "GetPartyMemberSizeLimit";

    [TestMethod]
    public void TaomCallers_PassNoPositionalBoolLiteral()
    {
        var calls = CSharpFiles("Main", "Client", "Dependencies")
            .SelectMany(file => FindCalls(File.ReadAllText(file)).Select(call => (File: file, Call: call)))
            .ToList();

        // TaomPartySizeModel's base call is always there. No invocation means the scan broke, not that the code is clean.
        Assert.IsTrue(calls.Any(c => c.Call.IsInvocation), $"found no invocation of {Method} under Main, Client or Dependencies");

        var offenders = calls.Where(c => HasPositionalBoolLiteral(c.Call.Arguments))
            .Select(c => $"{Path.GetFileName(c.File)}: {Method}({string.Join(", ", c.Call.Arguments)})")
            .ToList();
        Assert.AreEqual(0, offenders.Count, "name the bool argument: " + string.Join("; ", offenders));
    }

    [DataTestMethod]
    [DataRow("m.GetPartyMemberSizeLimit(party, true);", 1, 1, DisplayName = "a positional literal")]
    [DataRow("m.GetPartyMemberSizeLimit(ResolveParty(), true);", 1, 1, DisplayName = "a nested call in an argument")]
    [DataRow("m.GetPartyMemberSizeLimit (party, false);", 1, 1, DisplayName = "a space before the bracket")]
    [DataRow("m.GetPartyMemberSizeLimit(\n    party,\r\n    true);", 1, 1, DisplayName = "arguments over several lines")]
    [DataRow("m.GetPartyMemberSizeLimit(party, atSea, includeDescriptions: true);", 1, 0, DisplayName = "a named literal")]
    [DataRow("m.GetPartyMemberSizeLimit(party, describe);", 1, 0, DisplayName = "a variable is outside the rule")]
    [DataRow("m.GetPartyMemberSizeLimit(p, Pick(a, true), includeDescriptions: false);", 1, 0, DisplayName = "a literal inside a nested call belongs to that call")]
    [DataRow("var s = \"m.GetPartyMemberSizeLimit(party, true)\";", 0, 0, DisplayName = "inside a string")]
    [DataRow("var s = @\"m.GetPartyMemberSizeLimit(party, true)\";", 0, 0, DisplayName = "inside a verbatim string")]
    [DataRow("// m.GetPartyMemberSizeLimit(party, true)", 0, 0, DisplayName = "inside a line comment")]
    [DataRow("/* m.GetPartyMemberSizeLimit(party, true) */", 0, 0, DisplayName = "inside a block comment")]
    [DataRow("var u = \"http://x\"; m.GetPartyMemberSizeLimit(party, true);", 1, 1, DisplayName = "after a string that holds two slashes")]
    [DataRow("public override ExplainedNumber GetPartyMemberSizeLimit(PartyBase party, bool isCurrentlyAtSea, bool includeDescriptions = false)", 0, 0, DisplayName = "a declaration")]
    [DataRow("var name = nameof(PartySizeLimitModel.GetPartyMemberSizeLimit);", 0, 0, DisplayName = "a nameof")]
    public void FindCalls_ReadsEachShape(string source, int invocations, int offenders)
    {
        var calls = FindCalls(source).ToList();

        Assert.AreEqual(invocations, calls.Count(c => c.IsInvocation), "invocations");
        Assert.AreEqual(offenders, calls.Count(c => HasPositionalBoolLiteral(c.Arguments)), "offenders");
    }

    private readonly struct Call
    {
        public Call(bool isInvocation, IReadOnlyList<string> arguments)
        {
            IsInvocation = isInvocation;
            Arguments = arguments;
        }

        /// <summary>Reached through a member access. A declaration follows its return type.</summary>
        public bool IsInvocation { get; }

        public IReadOnlyList<string> Arguments { get; }
    }

    private static bool HasPositionalBoolLiteral(IReadOnlyList<string> arguments) =>
        arguments.Any(a => a == "true" || a == "false");

    /// <summary>Every <c>GetPartyMemberSizeLimit(...)</c> in code, with its top-level arguments.</summary>
    private static IEnumerable<Call> FindCalls(string source)
    {
        var code = BlankCommentsAndLiterals(source);
        for (var at = code.IndexOf(Method, StringComparison.Ordinal); at >= 0; at = code.IndexOf(Method, at + Method.Length, StringComparison.Ordinal))
        {
            if (at > 0 && IsIdentifierChar(code[at - 1]))
                continue;

            var open = at + Method.Length;
            while (open < code.Length && char.IsWhiteSpace(code[open]))
                open++;
            if (open >= code.Length || code[open] != '(')
                continue;

            var arguments = SplitArguments(code, open, out var closed);
            if (!closed)
                continue;

            var before = at - 1;
            while (before >= 0 && char.IsWhiteSpace(code[before]))
                before--;
            yield return new Call(before >= 0 && code[before] == '.', arguments);
        }
    }

    private static List<string> SplitArguments(string code, int open, out bool closed)
    {
        var arguments = new List<string>();
        var depth = 0;
        var start = open + 1;
        for (var i = open; i < code.Length; i++)
        {
            var c = code[i];
            if (c == '(' || c == '[' || c == '{')
            {
                depth++;
            }
            else if (c == ')' || c == ']' || c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    var last = code.Substring(start, i - start).Trim();
                    if (last.Length > 0 || arguments.Count > 0)
                        arguments.Add(last);
                    closed = true;
                    return arguments;
                }
            }
            else if (c == ',' && depth == 1)
            {
                arguments.Add(code.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }

        closed = false;
        return arguments;
    }

    /// <summary>Comments and string and character literals become spaces; line breaks and lengths stay.</summary>
    private static string BlankCommentsAndLiterals(string source)
    {
        var text = new StringBuilder(source);
        var i = 0;
        while (i < source.Length)
        {
            var c = source[i];
            var next = i + 1 < source.Length ? source[i + 1] : '\0';
            var third = i + 2 < source.Length ? source[i + 2] : '\0';
            int end;
            if (c == '/' && next == '/')
                end = EndOfLine(source, i);
            else if (c == '/' && next == '*')
                end = EndOfBlockComment(source, i + 2);
            else if ((c == '@' && next == '"') || (c == '$' && next == '@' && third == '"') || (c == '@' && next == '$' && third == '"'))
                end = EndOfVerbatimString(source, source.IndexOf('"', i) + 1);
            else if (c == '"')
                end = EndOfQuoted(source, i + 1, '"');
            else if (c == '\'')
                end = EndOfQuoted(source, i + 1, '\'');
            else
            {
                i++;
                continue;
            }

            for (var k = i; k < end; k++)
            {
                if (source[k] != '\n' && source[k] != '\r')
                    text[k] = ' ';
            }

            i = end;
        }

        return text.ToString();
    }

    private static int EndOfLine(string source, int from)
    {
        var end = source.IndexOf('\n', from);
        return end < 0 ? source.Length : end;
    }

    private static int EndOfBlockComment(string source, int from)
    {
        var end = source.IndexOf("*/", from, StringComparison.Ordinal);
        return end < 0 ? source.Length : end + 2;
    }

    private static int EndOfVerbatimString(string source, int from)
    {
        for (var i = from; i < source.Length; i++)
        {
            if (source[i] != '"')
                continue;
            if (i + 1 < source.Length && source[i + 1] == '"')
            {
                i++;
                continue;
            }

            return i + 1;
        }

        return source.Length;
    }

    private static int EndOfQuoted(string source, int from, char quote)
    {
        for (var i = from; i < source.Length; i++)
        {
            if (source[i] == '\\')
                i++;
            else if (source[i] == quote)
                return i + 1;
            else if (source[i] == '\n')
                return i;
        }

        return source.Length;
    }

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
