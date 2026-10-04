using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Tests.Migration;
using TaleWorlds.ObjectSystem;

namespace TAOM.Tests.Features.XmlMerge;

/// <summary>
/// Drift guard for Patch99 (plan 042). Every engine member the patch, the adapter and the harness reach is pinned
/// here: the target's signature and the parameter names Harmony binds by, the private loader the adapter binds to a
/// delegate, the public helpers it calls directly, the methods whose patching makes the fast path stand aside, and the
/// XSD tables the harness fills. The IL tests pin the shape of the engine's loop itself: if CreateMergedXmlFile or
/// MergeTwoXmls ever calls different MBObjectManager helpers, the merge algorithm changed and the fast path must be
/// re-proven with XmlMergeLiveEquivalenceTests before it ships again.
/// </summary>
[TestClass]
public class XmlMergeBindingTests
{
    private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CreateMergedXmlFile_IsPublicStaticWithThePrefixesParameterNames()
    {
        RequireGame();

        var method = AccessTools.Method(typeof(MBObjectManager), nameof(MBObjectManager.CreateMergedXmlFile),
            new[] { typeof(List<Tuple<string, string>>), typeof(List<string>), typeof(bool) });

        Assert.IsNotNull(method, "MBObjectManager.CreateMergedXmlFile(List<Tuple<string, string>>, List<string>, bool) is gone: Patch99 applies to nothing.");
        Assert.IsTrue(method.IsStatic && method.IsPublic, "CreateMergedXmlFile is no longer public static.");
        Assert.AreEqual(typeof(XmlDocument), method.ReturnType);
        // Harmony binds prefix arguments by NAME; a rename leaves every type check passing while the arguments arrive null.
        CollectionAssert.AreEqual(new[] { "toBeMerged", "xsltList", "skipValidation" },
            method.GetParameters().Select(p => p.Name).ToArray(),
            "CreateMergedXmlFile's parameters were renamed: Patch99's prefix binds them by name.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CreateDocumentFromXmlFile_ResolvesAsPrivateStaticWithTheAdaptersSignature()
    {
        RequireGame();

        var method = AccessTools.Method(typeof(MBObjectManager), XmlMergeEngineAdapter.CreateDocumentFromXmlFileName,
            new[] { typeof(string), typeof(string), typeof(bool) });

        Assert.IsNotNull(method, "MBObjectManager.CreateDocumentFromXmlFile(string, string, bool) is gone: the fast path turns itself off.");
        Assert.IsTrue(method.IsStatic, "CreateDocumentFromXmlFile is no longer static: the adapter's delegate cannot bind.");
        Assert.AreEqual(typeof(XmlDocument), method.ReturnType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PublicHelpers_ResolveWithTheAdaptersSignatures()
    {
        RequireGame();

        AssertPublicStatic("MergeElements", typeof(void), typeof(XElement), typeof(XElement), typeof(string));
        AssertPublicStatic("ToXDocument", typeof(XDocument), typeof(XmlDocument));
        AssertPublicStatic("ToXmlDocument", typeof(XmlDocument), typeof(XDocument));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void WatchedMethods_ResolveWithTheAdaptersSignatures()
    {
        RequireGame();

        Assert.AreEqual(4, XmlMergeEngineAdapter.WatchedMethods.Count);
        foreach (var watched in XmlMergeEngineAdapter.WatchedMethods)
        {
            // The adapter resolves each watched method by this exact parameter list; a miss turns the fast path off for
            // every player, so the gate must fail on a changed signature, not only on a missing name.
            var method = AccessTools.Method(typeof(MBObjectManager), watched.Key, watched.Value);
            Assert.IsNotNull(method, "MBObjectManager." + watched.Key + "(" +
                string.Join(", ", watched.Value.Select(t => t.Name)) + ") is gone or changed its parameters: " +
                "the fast path turns itself off, and the engine's merge loop changed under Patch99.");
            Assert.IsTrue(method.IsStatic, "MBObjectManager." + watched.Key + " is no longer static.");
        }
    }

    // The IL fingerprint (IlCallScanner.Fingerprint) of every engine body the fast path re-implements (CreateMergedXmlFile,
    // MergeTwoXmls, ApplyXslt) or whose round trip it drops (ToXDocument, ToXmlDocument): in order, each call with its
    // constructed declaring type and parameter list, each constant (keepDuplicates, the loop start, list indices, ""),
    // and each comparison and conditional branch. An engine update that reorders the loop, calls another overload or
    // member, changes a constant argument or a comparison changes a fingerprint here; the fast path would otherwise
    // return a different document with no exception and no fallback. Not pinned: which local or argument feeds a call,
    // which statements a branch skips (a dropped else, an added continue or a nested if leaves every token in place),
    // and anything outside these five bodies (the XSLT settings and reader options other code passes, the engine's
    // MergeElements). So run XmlMergeLiveEquivalenceTests after every engine update whatever this gate says, then re-pin.
    private static readonly Dictionary<string, (Type[] Parameters, string[] Tokens)> PinnedFingerprints =
        new Dictionary<string, (Type[], string[])>(StringComparer.Ordinal)
        {
            // v1.5.3 MBObjectManager.cs:962-978: load entry 0; for i from 1 while i < toBeMerged.Count: XSLT if
            // xsltList[i] != "", then load and MergeTwoXmls(keepDuplicates: false) if toBeMerged[i].Item1 != "".
            ["CreateMergedXmlFile"] = (new[] { typeof(List<Tuple<string, string>>), typeof(List<string>), typeof(bool) }, new[]
            {
                "ldc.i4 0", "callvirt List<Tuple<String,String>>::get_Item(Int32)", "callvirt Tuple<String,String>::get_Item1()",
                "ldc.i4 0", "callvirt List<Tuple<String,String>>::get_Item(Int32)", "callvirt Tuple<String,String>::get_Item2()",
                "call MBObjectManager::CreateDocumentFromXmlFile(String,String,Boolean)",
                "ldc.i4 1",
                "callvirt List<String>::get_Item(Int32)", "ldstr \"\"", "call String::op_Inequality(String,String)", "brfalse",
                "callvirt List<String>::get_Item(Int32)", "call MBObjectManager::ApplyXslt(String,XmlDocument)",
                "callvirt List<Tuple<String,String>>::get_Item(Int32)", "callvirt Tuple<String,String>::get_Item1()",
                "ldstr \"\"", "call String::op_Inequality(String,String)", "brfalse",
                "callvirt List<Tuple<String,String>>::get_Item(Int32)", "callvirt Tuple<String,String>::get_Item1()",
                "callvirt List<Tuple<String,String>>::get_Item(Int32)", "callvirt Tuple<String,String>::get_Item2()",
                "call MBObjectManager::CreateDocumentFromXmlFile(String,String,Boolean)",
                "callvirt List<Tuple<String,String>>::get_Item(Int32)", "callvirt Tuple<String,String>::get_Item2()",
                "ldc.i4 0", "call MBObjectManager::MergeTwoXmls(XmlDocument,XmlDocument,String,Boolean)",
                "ldc.i4 1", "callvirt List<Tuple<String,String>>::get_Count()", "blt",
            }),
            // :993-1006: convert the accumulated document, convert the next; append when keepDuplicates or the XSD path
            // is "", else MergeElements; convert back.
            ["MergeTwoXmls"] = (new[] { typeof(XmlDocument), typeof(XmlDocument), typeof(string), typeof(bool) }, new[]
            {
                "call MBObjectManager::ToXDocument(XmlDocument)", "call MBObjectManager::ToXDocument(XmlDocument)", "brtrue",
                "ldstr \"\"", "call String::op_Equality(String,String)", "brfalse",
                "callvirt XDocument::get_Root()", "callvirt XDocument::get_Root()", "callvirt XContainer::Elements()",
                "callvirt XContainer::Add(Object)",
                "callvirt XDocument::get_Root()", "callvirt XDocument::get_Root()",
                "call MBObjectManager::MergeElements(XElement,XElement,String)",
                "call MBObjectManager::ToXmlDocument(XDocument)",
            }),
            // :980-991. XsltTransformCache copies this body line for line, with the compiled stylesheet cached.
            ["ApplyXslt"] = (new[] { typeof(string), typeof(XmlDocument) }, new[]
            {
                "newobj XmlNodeReader::.ctor(XmlNode)", "newobj StreamReader::.ctor(String)", "call XmlReader::Create(TextReader)",
                "newobj XslCompiledTransform::.ctor()", "callvirt XslCompiledTransform::Load(XmlReader)",
                "callvirt XmlNode::CreateNavigator()", "callvirt XPathNavigator::get_NameTable()",
                "newobj XmlDocument::.ctor(XmlNameTable)", "callvirt XmlNode::CreateNavigator()",
                "callvirt XPathNavigator::AppendChild()", "callvirt XslCompiledTransform::Transform(XmlReader,XmlWriter)",
                "callvirt XmlWriter::Close()", "brfalse", "callvirt IDisposable::Dispose()",
            }),
            // :1008-1021, Debug.Print's default arguments included.
            ["ToXDocument"] = (new[] { typeof(XmlDocument) }, new[]
            {
                "newobj XmlNodeReader::.ctor(XmlNode)", "callvirt XmlReader::MoveToContent()", "call XDocument::Load(XmlReader)",
                "callvirt Exception::get_Message()", "ldc.i4 0", "ldc.i4 12", "ldc.i8 17592186044416",
                "call Debug::Print(String,Int32,DebugColor,UInt64)", "brfalse", "callvirt IDisposable::Dispose()",
            }),
            // :1023-1031.
            ["ToXmlDocument"] = (new[] { typeof(XDocument) }, new[]
            {
                "newobj XmlDocument::.ctor()", "callvirt XNode::CreateReader()", "callvirt XNode::CreateReader()",
                "callvirt XmlDocument::Load(XmlReader)", "brfalse", "callvirt IDisposable::Dispose()",
            }),
        };

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void MirroredEngineBodies_CallExactlyThePinnedSequence()
    {
        RequireGame();

        var drift = new List<string>();
        foreach (var pinned in PinnedFingerprints)
        {
            var method = AccessTools.Method(typeof(MBObjectManager), pinned.Key, pinned.Value.Parameters);
            Assert.IsNotNull(method, "MBObjectManager." + pinned.Key + " is gone or changed its parameters.");
            var il = method.GetMethodBody()?.GetILAsByteArray();
            Assert.IsNotNull(il, pinned.Key + " has no readable IL body.");
            var tokens = IlCallScanner.Fingerprint(method, il!).ToArray();
            Assert.AreNotEqual(0, tokens.Length, pinned.Key + " fingerprinted to nothing; the scan failed, not the method.");
            if (!tokens.SequenceEqual(pinned.Value.Tokens))
                drift.Add(pinned.Key + ": { " + string.Join(", ", tokens.Select(c => "\"" + c.Replace("\"", "\\\"") + "\"")) + " }");
        }

        Assert.AreEqual(0, drift.Count,
            "The engine's merge code changed under Patch99. Re-prove the fast path with XmlMergeLiveEquivalenceTests " +
            "(and the fixtures in XmlMergeEngineEquivalenceTests), then re-pin. Actual sequences:\n" + string.Join("\n", drift));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void XsdTables_AreThePublicStaticsTheHarnessUses()
    {
        RequireGame();

        var read = AccessTools.Method(typeof(XmlResource), nameof(XmlResource.ReadXsdFileAndExtractInformation), new[] { typeof(string) });
        Assert.IsNotNull(read, "XmlResource.ReadXsdFileAndExtractInformation(string) is gone.");
        Assert.IsTrue(read.IsStatic && read.IsPublic);

        var table = typeof(XmlResource).GetField(nameof(XmlResource.XsdElementDictionary), AnyStatic);
        Assert.IsNotNull(table, "XmlResource.XsdElementDictionary is gone: MergeElements reads its unique attributes there.");
        Assert.IsTrue(table.IsPublic);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void CreateMergedXmlFile_CallsExactlyTheThreeHelpers()
    {
        RequireGame();

        var called = EngineHelpersCalledBy(nameof(MBObjectManager.CreateMergedXmlFile));

        CollectionAssert.AreEquivalent(new[] { "CreateDocumentFromXmlFile", "ApplyXslt", "MergeTwoXmls" }, called,
            "CreateMergedXmlFile calls different MBObjectManager helpers: the engine's merge algorithm changed. " +
            "Re-prove the fast path with XmlMergeLiveEquivalenceTests before Patch99 ships again.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void MergeTwoXmls_ConvertsMergesAndConvertsBack()
    {
        RequireGame();

        var called = EngineHelpersCalledBy(nameof(MBObjectManager.MergeTwoXmls));

        CollectionAssert.AreEquivalent(new[] { "ToXDocument", "MergeElements", "ToXmlDocument" }, called,
            "MergeTwoXmls calls different MBObjectManager helpers: the engine's merge algorithm changed. " +
            "Re-prove the fast path with XmlMergeLiveEquivalenceTests before Patch99 ships again.");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Adapter_OnInstalledEngine_HasNoBindingProblem()
    {
        RequireGame();

        var adapter = new XmlMergeEngineAdapter();

        Assert.IsNull(adapter.BindingProblem, adapter.BindingProblem);
        Assert.AreEqual(0, adapter.ForeignPatches().Count, "nothing patches the merge helpers in the test process");
    }

    private static void AssertPublicStatic(string name, Type returns, params Type[] parameters)
    {
        var method = AccessTools.Method(typeof(MBObjectManager), name, parameters);
        Assert.IsNotNull(method, "MBObjectManager." + name + " is gone or changed its parameters: the fast path turns itself off.");
        Assert.IsTrue(method.IsStatic && method.IsPublic, "MBObjectManager." + name + " is no longer public static.");
        Assert.AreEqual(returns, method.ReturnType, "MBObjectManager." + name + " changed its return type.");
    }

    private static string[] EngineHelpersCalledBy(string methodName)
    {
        var method = typeof(MBObjectManager).GetMethods(AnyStatic).Single(m => m.Name == methodName);
        var il = method.GetMethodBody()?.GetILAsByteArray();
        Assert.IsNotNull(il, methodName + " has no readable IL body.");
        var called = IlCallScanner.ExtractCalledMethods(method, il!).ToArray();
        Assert.AreNotEqual(0, called.Length, methodName + " resolved no calls; the scan failed, not the method.");
        return called.Where(m => m.DeclaringType == typeof(MBObjectManager)).Select(m => m.Name).Distinct().ToArray();
    }
}
