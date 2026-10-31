using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TAOM.Tests.Infrastructure;

/// <summary>One <c>catch</c> clause: its keyword, its body braces (inclusive) and the verdict.</summary>
internal sealed class CatchBlock
{
    internal CatchBlock(int line, int keywordIndex, int bodyStart, int bodyEnd, bool parsed, bool silent)
    {
        Line = line;
        KeywordIndex = keywordIndex;
        BodyStart = bodyStart;
        BodyEnd = bodyEnd;
        Parsed = parsed;
        Silent = silent;
    }

    internal int Line { get; }
    internal int KeywordIndex { get; }

    /// <summary>Index of the body's <c>{</c>; -1 when the catch did not parse.</summary>
    internal int BodyStart { get; }

    /// <summary>Index of the body's matching <c>}</c>; -1 when the catch did not parse.</summary>
    internal int BodyEnd { get; }

    internal bool Parsed { get; }
    internal bool Silent { get; }
}

/// <summary>
/// Three views of one source, each the source's length with its line breaks kept, so an offset in one is the same
/// character in the others. <see cref="Code"/> blanks comments and the contents of string and char literals;
/// <see cref="CommentVisible"/> blanks only literal contents; <see cref="NoComments"/> blanks only comments.
/// </summary>
internal sealed class SourceMasks
{
    internal SourceMasks(string source, string code, string commentVisible, string noComments)
    {
        Source = source;
        Code = code;
        CommentVisible = commentVisible;
        NoComments = noComments;
    }

    internal string Source { get; }
    internal string Code { get; }
    internal string CommentVisible { get; }
    internal string NoComments { get; }
}

/// <summary>
/// The scan behind <see cref="SilentCatchRatchetTests"/>: which files it reads, where each catch body runs, and
/// whether that body is silent. Text in, verdict out, so fixtures test it without files. The lexer knows C# 10
/// literals (regular, verbatim, interpolated in both orders, char); raw string literals arrive with C# 11 and do
/// not occur here (<c>Directory.Build.props</c> LangVersion). <see cref="RepoPaths.StripComments"/> is not used
/// because it is not string-literal aware.
/// </summary>
internal static class SilentCatchScan
{
    /// <summary>
    /// A body holding one of these is not silent. The survey's list (#1069 design section 2) plus the TAOM
    /// helpers that log or hand the exception back. <c>Report</c> and <c>Record</c> count as calls and a logger
    /// as a member access, so <c>lastReport</c> or <c>var logger = x</c> clear nothing. <c>Fail(</c> also matches the tactic status setters
    /// (<c>TaomBehaviorBase</c>, <c>TaomTacticBase</c>, <c>TacticStagedObjective</c>), which do not log: an
    /// accepted undercount.
    /// </summary>
    private static readonly Regex Tokens = new(
        @"\bLog\w*\s*\(|\w*Logger\s*\??\.|\bDiagLog\b|Fault\s*\(|\bNote\w*\s*\(|\bReport\w*\s*\(|\bRecord\w*\s*\(|Console\.Write|problems\.Add"
        + @"|Debug\.Print\s*\(|\bSwallowed\s*\(|\bFail\s*\(|\bStandDown\s*\(|\.Error\s*\(|\bHandBack\s*\("
        + @"|\bPreserveForRethrow\s*\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Throw = new(@"\bthrow\b", RegexOptions.Compiled);
    private static readonly Regex CatchKeyword = new(@"\bcatch\b", RegexOptions.Compiled);

    // The marker sits in a comment and carries a reason on the same line.
    private static readonly Regex SwallowOk = new(@"(//|/\*)[ \t]*swallow-ok:[ \t]*[^\s*/]", RegexOptions.Compiled);

    private static readonly Regex HarmonyPatchAttribute =
        new(@"\[[^\]]*?(?<![\w.])(global::)?(HarmonyLib\.)?HarmonyPatch\b(?!Category)", RegexOptions.Compiled);

    // Starts with Patch and a digit, or ends with Patch or Patches: HarmonyPatchInspector and
    // PatchCategoryIndex are console and index code, not patches.
    private static readonly Regex PatchClassName =
        new(@"\b(class|struct)\s+(Patch\d\w*|\w*Patch(es)?)\b", RegexOptions.Compiled);

    /// <summary>
    /// A file is scanned when a directory segment of its repo-relative path is <c>Hooks</c>, or its code (comments
    /// and strings blank) carries a <c>[HarmonyPatch]</c> attribute or declares a patch-named class.
    /// </summary>
    internal static bool IsSelected(string relativePath, string source)
    {
        var segments = relativePath.Split('/', '\\');
        if (segments.Take(segments.Length - 1).Any(s => s == "Hooks"))
            return true;
        if (source.IndexOf("Patch", System.StringComparison.Ordinal) < 0)
            return false;

        var code = Masks(source).Code;
        return HarmonyPatchAttribute.IsMatch(code) || PatchClassName.IsMatch(code);
    }

    /// <summary>Every catch of <paramref name="source"/> (LF or CRLF), in source order.</summary>
    internal static List<CatchBlock> Catches(string source) => Catches(Masks(source));

    internal static List<CatchBlock> Catches(SourceMasks masks)
    {
        var code = masks.Code;
        var result = new List<CatchBlock>();
        var found = new List<(Match Keyword, int BodyStart, int BodyEnd)>();
        foreach (Match m in CatchKeyword.Matches(code))
        {
            var start = FindBody(code, m.Index + m.Length);
            found.Add((m, start, start < 0 ? -1 : MatchClose(code, start, '{', '}')));
        }

        foreach (var (m, bodyStart, bodyEnd) in found)
        {
            var line = 1 + CountNewlines(code, m.Index);
            if (bodyEnd < 0)
            {
                result.Add(new CatchBlock(line, m.Index, -1, -1, parsed: false, silent: false));
                continue;
            }

            // A log call or swallow-ok marker inside a nested catch belongs to that catch, not to this one.
            var body = code.Substring(bodyStart, bodyEnd - bodyStart + 1).ToCharArray();
            var span = masks.CommentVisible.Substring(m.Index, bodyEnd - m.Index + 1).ToCharArray();
            foreach (var nested in found.Where(n => n.BodyEnd > 0 && n.Keyword.Index > bodyStart && n.BodyEnd < bodyEnd))
            {
                Blank(body, nested.Keyword.Index - bodyStart, nested.BodyEnd - bodyStart + 1);
                Blank(span, nested.Keyword.Index - m.Index, nested.BodyEnd - m.Index + 1);
            }

            var bodyText = new string(body);
            var silent = !Tokens.IsMatch(bodyText) && !Throw.IsMatch(bodyText) && !SwallowOk.IsMatch(new string(span));
            result.Add(new CatchBlock(line, m.Index, bodyStart, bodyEnd, parsed: true, silent));
        }

        return result;
    }

    /// <summary>
    /// After the keyword: an optional <c>(Type name)</c>, an optional <c>when (filter)</c>, then the body's
    /// <c>{</c>. Returns its index, or -1 when anything else comes first (the gate fails closed on that).
    /// </summary>
    private static int FindBody(string code, int i)
    {
        i = SkipSpace(code, i);
        if (i < code.Length && code[i] == '(')
        {
            i = MatchClose(code, i, '(', ')');
            if (i < 0)
                return -1;
            i = SkipSpace(code, i + 1);
        }

        if (string.CompareOrdinal(code, i, "when", 0, 4) == 0 && (i + 4 >= code.Length || !IsIdent(code[i + 4])))
        {
            i = SkipSpace(code, i + 4);
            if (i >= code.Length || code[i] != '(')
                return -1;
            i = MatchClose(code, i, '(', ')');
            if (i < 0)
                return -1;
            i = SkipSpace(code, i + 1);
        }

        return i < code.Length && code[i] == '{' ? i : -1;
    }

    private static int MatchClose(string code, int open, char openCh, char closeCh)
    {
        var depth = 0;
        for (var i = open; i < code.Length; i++)
        {
            if (code[i] == openCh)
                depth++;
            else if (code[i] == closeCh && --depth == 0)
                return i;
        }

        return -1;
    }

    private static int SkipSpace(string s, int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i]))
            i++;
        return i;
    }

    private static int CountNewlines(string s, int end)
    {
        var n = 0;
        for (var i = 0; i < end; i++)
        {
            if (s[i] == '\n')
                n++;
        }

        return n;
    }

    private static bool IsIdent(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>One left-to-right pass; CRLF becomes LF first, as <see cref="RepoPaths.ReadSource"/> does.</summary>
    internal static SourceMasks Masks(string source)
    {
        source = source.Replace("\r\n", "\n");
        var code = source.ToCharArray();
        var visible = source.ToCharArray();
        var noComments = source.ToCharArray();
        var i = 0;
        while (i < source.Length)
        {
            var c = source[i];
            var next = i + 1 < source.Length ? source[i + 1] : '\0';
            if (c == '/' && next == '/')
            {
                var end = source.IndexOf('\n', i);
                if (end < 0)
                    end = source.Length;
                Blank(code, i, end);
                Blank(noComments, i, end);
                i = end;
            }
            else if (c == '/' && next == '*')
            {
                var close = source.IndexOf("*/", i + 2, System.StringComparison.Ordinal);
                var end = close < 0 ? source.Length : close + 2;
                Blank(code, i, end);
                Blank(noComments, i, end);
                i = end;
            }
            else if (c == '\'' || c == '"' || ((c == '@' || c == '$') && IsStringPrefix(source, i)))
            {
                var open = source.IndexOf(c == '\'' ? '\'' : '"', i);
                var end = SkipLiteral(source, i);
                Blank(code, open + 1, end - 1);
                Blank(visible, open + 1, end - 1);
                i = end;
            }
            else
            {
                i++;
            }
        }

        return new SourceMasks(source, new string(code), new string(visible), new string(noComments));
    }

    private static bool IsStringPrefix(string s, int i)
    {
        string Peek(int n) => i + n <= s.Length ? s.Substring(i, n) : "";
        return Peek(2) == "@\"" || Peek(2) == "$\"" || Peek(3) == "$@\"" || Peek(3) == "@$\"";
    }

    /// <summary>
    /// <paramref name="start"/> is the literal's first character (a quote, or the <c>@</c>/<c>$</c> prefix).
    /// Returns the index just past the closing quote, or the end of the source for an unterminated literal.
    /// </summary>
    private static int SkipLiteral(string s, int start)
    {
        if (s[start] == '\'')
            return SkipChar(s, start);

        var verbatim = false;
        var interpolated = false;
        var i = start;
        while (s[i] != '"')
        {
            verbatim |= s[i] == '@';
            interpolated |= s[i] == '$';
            i++;
        }

        i++;
        while (i < s.Length)
        {
            var c = s[i];
            var next = i + 1 < s.Length ? s[i + 1] : '\0';
            if (!verbatim && c == '\\')
                i += 2;
            else if (!verbatim && c == '\n')
                return i;
            else if (c == '"' && verbatim && next == '"')
                i += 2;
            else if (c == '"')
                return i + 1;
            else if (interpolated && (c == '{' || c == '}') && next == c)
                i += 2;
            else if (interpolated && c == '{')
                i = SkipHole(s, i + 1);
            else
                i++;
        }

        return s.Length;
    }

    private static int SkipChar(string s, int start)
    {
        var i = start + 1;
        while (i < s.Length && s[i] != '\'' && s[i] != '\n')
            i += s[i] == '\\' ? 2 : 1;
        return i < s.Length && s[i] == '\'' ? i + 1 : i;
    }

    /// <summary>Code inside an interpolation hole, to the <c>}</c> that closes it; nested literals are skipped whole.</summary>
    private static int SkipHole(string s, int i)
    {
        var depth = 0;
        while (i < s.Length)
        {
            var c = s[i];
            if (c == '\'' || c == '"' || ((c == '@' || c == '$') && IsStringPrefix(s, i)))
            {
                i = SkipLiteral(s, i);
                continue;
            }

            if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
            {
                var close = s.IndexOf("*/", i + 2, System.StringComparison.Ordinal);
                i = close < 0 ? s.Length : close + 2;
                continue;
            }

            if (c == '('|| c == '[' || c == '{')
                depth++;
            else if (c == ')' || c == ']')
                depth--;
            else if (c == '}' && depth-- == 0)
                return i + 1;
            i++;
        }

        return s.Length;
    }

    private static void Blank(char[] chars, int from, int to)
    {
        for (var k = from; k < to && k < chars.Length; k++)
        {
            if (chars[k] != '\n')
                chars[k] = ' ';
        }
    }
}
