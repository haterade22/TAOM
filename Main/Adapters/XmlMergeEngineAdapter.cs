using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using HarmonyLib;
using TaleWorlds.ObjectSystem;

namespace TAOM.Adapters;

/// <summary>
/// The engine's merge helpers on <c>MBObjectManager</c> (v1.5.3) for the XmlMerge fast path. The private
/// <c>CreateDocumentFromXmlFile</c> (MBObjectManager.cs:1339) is bound once to a cached delegate; the public
/// <c>ToXDocument</c>, <c>ToXmlDocument</c> and <c>MergeElements</c> are direct calls, each in its own method, so a
/// member missing after an engine update fails when that method is compiled, inside the service's try. The constructor
/// checks every member up front and names any that did not resolve in <see cref="BindingProblem"/>, which turns the fast
/// path off before the first merge. A reflection-site row in ReflectionSiteBindingTests pins the private binding, and
/// XmlMergeBindingTests the rest.
/// </summary>
public sealed class XmlMergeEngineAdapter : IXmlMergeEngineAdapter
{
    internal const string CreateDocumentFromXmlFileName = "CreateDocumentFromXmlFile";

    /// <summary>
    /// Engine methods the fast path bypasses or calls a different number of times, each with the exact parameter list it
    /// is resolved by: any patch on one makes the fast path stand aside. XmlMergeBindingTests resolves the same lists.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, Type[]> WatchedMethods = new Dictionary<string, Type[]>(StringComparer.Ordinal)
    {
        ["ApplyXslt"] = new[] { typeof(string), typeof(XmlDocument) },
        ["MergeTwoXmls"] = new[] { typeof(XmlDocument), typeof(XmlDocument), typeof(string), typeof(bool) },
        ["ToXDocument"] = new[] { typeof(XmlDocument) },
        ["ToXmlDocument"] = new[] { typeof(XDocument) },
    };

    // The owners whose patches are TAOM's own: this mod and PatchShield's crash finalizers.
    private static readonly HashSet<string> OwnOwners = new HashSet<string>(StringComparer.Ordinal)
    {
        "com.taom.mod",
        "TAOM.Dependencies.Foundation.PatchShield",
    };

    private readonly Func<string, string, bool, XmlDocument>? _createDocument;
    private readonly MethodInfo? _target;
    private readonly List<MethodInfo> _watched = new List<MethodInfo>();

    public XmlMergeEngineAdapter()
    {
        var problems = new List<string>();
        try
        {
            var load = AccessTools.Method(typeof(MBObjectManager), CreateDocumentFromXmlFileName,
                new[] { typeof(string), typeof(string), typeof(bool) });
            if (load == null || !load.IsStatic || load.ReturnType != typeof(XmlDocument))
                problems.Add("MBObjectManager.CreateDocumentFromXmlFile(string, string, bool) not found as a static XmlDocument method");
            else
                _createDocument = (Func<string, string, bool, XmlDocument>)Delegate.CreateDelegate(
                    typeof(Func<string, string, bool, XmlDocument>), load);

            Require(problems, "MergeElements", "XElement, XElement, string", typeof(XElement), typeof(XElement), typeof(string));
            Require(problems, "ToXDocument", "XmlDocument", typeof(XmlDocument));
            Require(problems, "ToXmlDocument", "XDocument", typeof(XDocument));

            _target = AccessTools.Method(typeof(MBObjectManager), nameof(MBObjectManager.CreateMergedXmlFile),
                new[] { typeof(List<Tuple<string, string>>), typeof(List<string>), typeof(bool) });
            if (_target == null)
                problems.Add("MBObjectManager.CreateMergedXmlFile(List<Tuple<string, string>>, List<string>, bool) not found");

            foreach (var pair in WatchedMethods)
            {
                var watched = AccessTools.Method(typeof(MBObjectManager), pair.Key, pair.Value);
                if (watched == null)
                    problems.Add("MBObjectManager." + pair.Key + " not found (the engine's merge loop changed)");
                else
                    _watched.Add(watched);
            }
        }
        catch (Exception ex)
        {
            problems.Add("binding the engine's merge helpers threw " + ex.GetType().Name + ": " + ex.Message);
        }

        BindingProblem = problems.Count == 0 ? null : string.Join("; ", problems);
    }

    public string? BindingProblem { get; }

    public XmlDocument CreateDocumentFromXmlFile(string xmlPath, string xsdPath, bool forceSkipValidation)
    {
        if (_createDocument == null)
            throw new InvalidOperationException("MBObjectManager.CreateDocumentFromXmlFile is not bound: " + BindingProblem);
        return _createDocument(xmlPath, xsdPath, forceSkipValidation);
    }

    public XDocument ToXDocument(XmlDocument document) => MBObjectManager.ToXDocument(document);

    public XmlDocument ToXmlDocument(XDocument document) => MBObjectManager.ToXmlDocument(document);

    public void MergeElements(XElement element1, XElement element2, string xsdPath) =>
        MBObjectManager.MergeElements(element1, element2, xsdPath);

    /// <summary>
    /// Another owner's prefix, transpiler, inner prefix or inner postfix on <c>CreateMergedXmlFile</c> (its postfixes and
    /// finalizers still run after the fast path), or any patch at all on a method the fast path bypasses. All six of
    /// Harmony's patch collections are read (lessons/harmony-il.md, "Read every patch collection Harmony exposes").
    /// </summary>
    public IReadOnlyList<string> ForeignPatches() => ForeignPatches(_target, _watched, Harmony.GetPatchInfo);

    /// <summary>
    /// Which method's postfixes and finalizers count: the target's never, a watched method's always. The patch info is
    /// a parameter so XmlMergeForeignPatchFilterTests pins this choice without patching the engine.
    /// </summary>
    internal static IReadOnlyList<string> ForeignPatches(MethodInfo? target, IEnumerable<MethodInfo> watched,
        Func<MethodBase, Patches?> patchInfo)
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        if (target != null)
            CollectForeign(found, patchInfo(target), Where(target), includeAfterOriginal: false);
        foreach (var method in watched)
            CollectForeign(found, patchInfo(method), Where(method), includeAfterOriginal: true);
        return found.ToList();
    }

    private static void Require(List<string> problems, string name, string signature, params Type[] parameters)
    {
        var method = AccessTools.Method(typeof(MBObjectManager), name, parameters);
        if (method == null || !method.IsStatic)
            problems.Add("MBObjectManager." + name + "(" + signature + ") not found as a static method");
    }

    private static string Where(MethodInfo method) => "MBObjectManager." + method.Name;

    /// <summary>
    /// The stand-aside rule over one method's patch info: every patch whose owner is not TAOM's own, from the collections
    /// that run before or instead of the original, plus postfixes and finalizers when <paramref name="includeAfterOriginal"/>.
    /// Pure, so XmlMergeForeignPatchFilterTests pins it without patching the engine.
    /// </summary>
    internal static void CollectForeign(SortedSet<string> found, Patches? info, string where, bool includeAfterOriginal)
    {
        if (info == null)
            return;

        Add(found, info.Prefixes, "prefix", where);
        Add(found, info.Transpilers, "transpiler", where);
        Add(found, info.InnerPrefixes, "inner-prefix", where);
        Add(found, info.InnerPostfixes, "inner-postfix", where);
        if (includeAfterOriginal)
        {
            Add(found, info.Postfixes, "postfix", where);
            Add(found, info.Finalizers, "finalizer", where);
        }
    }

    private static void Add(SortedSet<string> found, IEnumerable<Patch>? patches, string kind, string where)
    {
        if (patches == null)
            return;
        foreach (var patch in patches)
        {
            if (patch == null || OwnOwners.Contains(patch.owner))
                continue;
            found.Add(patch.owner + " " + kind + " " + where);
        }
    }
}
