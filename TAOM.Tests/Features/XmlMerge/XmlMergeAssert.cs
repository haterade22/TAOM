using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.XmlMerge;

/// <summary>
/// Character-for-character comparison of two merged documents (plan 042). A difference fails with
/// both lengths, the first differing index and the text around it from each side, so a report
/// names the exact spot without dumping megabytes of XML.
/// </summary>
internal static class XmlMergeAssert
{
    private const int ContextBefore = 120;
    private const int ContextLength = 300;

    public static void SameDocument(string engineXml, string fastXml, string label)
    {
        if (string.Equals(engineXml, fastXml, StringComparison.Ordinal))
            return;

        int shorter = Math.Min(engineXml.Length, fastXml.Length);
        int index = 0;
        while (index < shorter && engineXml[index] == fastXml[index])
            index++;

        Assert.Fail(
            $"{label}: documents differ; engine length {engineXml.Length}, fast length {fastXml.Length}, " +
            $"first difference at {index}; engine: {Context(engineXml, index)}; fast: {Context(fastXml, index)}");
    }

    private static string Context(string text, int index)
    {
        int start = Math.Max(0, index - ContextBefore);
        int length = Math.Min(ContextLength, text.Length - start);
        return length > 0 ? text.Substring(start, length) : "";
    }
}
