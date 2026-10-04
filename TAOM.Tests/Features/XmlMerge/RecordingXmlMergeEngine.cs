using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using TAOM.Adapters;

namespace TAOM.Tests.Features.XmlMerge;

/// <summary>
/// Hand-written fake of the engine's merge helpers (plan 042). Documents come from an in-memory map of path to XML text;
/// the two conversions are the engine's own System.Xml code (MBObjectManager.cs:1008-1031); MergeElements appends
/// element2's child elements to element1. Every call appends a token to <see cref="Calls"/> so a test can pin the exact
/// order the service calls the engine in.
/// </summary>
internal sealed class RecordingXmlMergeEngine : IXmlMergeEngineAdapter
{
    public Dictionary<string, string> Files { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public List<string> Calls { get; } = new List<string>();

    /// <summary>Every document CreateDocumentFromXmlFile returned, in order.</summary>
    public List<XmlDocument> Loaded { get; } = new List<XmlDocument>();

    /// <summary>When set, CreateDocumentFromXmlFile throws it.</summary>
    public Exception? ThrowOnLoad { get; set; }

    public string? BindingProblem { get; set; }

    public List<string> Foreign { get; set; } = new List<string>();

    public XmlDocument CreateDocumentFromXmlFile(string xmlPath, string xsdPath, bool forceSkipValidation)
    {
        Calls.Add("load " + xmlPath);
        if (ThrowOnLoad != null)
            throw ThrowOnLoad;
        if (!Files.TryGetValue(xmlPath, out var text))
            throw new FileNotFoundException("no such fixture", xmlPath);

        var document = new XmlDocument();
        document.LoadXml(text);
        Loaded.Add(document);
        return document;
    }

    public XDocument ToXDocument(XmlDocument document)
    {
        Calls.Add("toX");
        using var reader = new XmlNodeReader(document);
        reader.MoveToContent();
        return XDocument.Load(reader);
    }

    public XmlDocument ToXmlDocument(XDocument document)
    {
        Calls.Add("toXml");
        var xml = new XmlDocument();
        xml.Load(document.CreateReader());
        return xml;
    }

    public void MergeElements(XElement element1, XElement element2, string xsdPath)
    {
        Calls.Add("merge");
        element1.Add(element2.Elements().ToList());
    }

    public IReadOnlyList<string> ForeignPatches() => Foreign;
}
