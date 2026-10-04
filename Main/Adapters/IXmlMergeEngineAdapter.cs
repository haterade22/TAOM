using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;

namespace TAOM.Adapters;

/// <summary>
/// The engine's module-XML merge helpers on <c>MBObjectManager</c>, for the XmlMerge fast path
/// (docs/features/xml-merge-fast-path.md). The service calls these exactly as the engine's own
/// <c>CreateMergedXmlFile</c> does, so the document it builds is the engine's.
/// </summary>
public interface IXmlMergeEngineAdapter
{
    /// <summary>Null when every engine member the fast path calls resolved; otherwise which did not.</summary>
    string? BindingProblem { get; }

    XmlDocument CreateDocumentFromXmlFile(string xmlPath, string xsdPath, bool forceSkipValidation);

    XDocument ToXDocument(XmlDocument document);

    XmlDocument ToXmlDocument(XDocument document);

    void MergeElements(XElement element1, XElement element2, string xsdPath);

    /// <summary>"owner kind Type.Method" for every patch that makes the fast path unsafe (Design, "Standing aside"); empty when none.</summary>
    IReadOnlyList<string> ForeignPatches();
}
