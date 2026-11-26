using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Incidents;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Features.IncidentVoices;
using TAOM.Features.IncidentVoices.Hooks;
using TAOM.Tests.Migration;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.IncidentVoices;

/// <summary>
/// Pins the vanilla seams Incident Voices rides (Patch175) against the installed engine, with no campaign: the target keeps its shape
/// and is too big for the JIT to inline, the text objects are written through a public field, the 101 incident ids and each one's
/// option count are still what <c>incidents_lotr.json</c> lists, and the category attaches one postfix.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class IncidentVoicesBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static MethodInfo Target()
    {
        RequireGame();
        var method = AccessTools.Method(typeof(Incident), "CanIncidentBeInvoked");
        Assert.IsNotNull(method, "Incident.CanIncidentBeInvoked did not resolve.");
        return method!;
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CanIncidentBeInvoked_IsAPublicNonVirtualInstanceBool_WithNoParameters()
    {
        var target = Target();

        Assert.IsTrue(target.IsPublic);
        Assert.IsFalse(target.IsStatic);
        Assert.IsFalse(target.IsVirtual, "a virtual target would be patched on the base only");
        Assert.AreEqual(typeof(bool), target.ReturnType);
        Assert.AreEqual(0, target.GetParameters().Length);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CanIncidentBeInvoked_IsTooLargeForTheClrToInline()
    {
        var il = Target().GetMethodBody()!.GetILAsByteArray()!.Length;

        Assert.IsTrue(il > 32, "IL size " + il + " could be inlined into a caller compiled first, which would skip the postfix");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheTextSeams_ArePublic_TitleDescriptionOptionTextAndTheValueField()
    {
        RequireGame();
        var value = typeof(TextObject).GetField("Value", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(value, "TextObject.Value must be a public field");
        Assert.AreEqual(typeof(string), value!.FieldType);

        var cache = typeof(TextObject).GetMethod("CacheTokens", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
        Assert.IsNotNull(cache, "TextObject.CacheTokens() must be public");

        Assert.AreEqual(typeof(TextObject), typeof(Incident).GetProperty("Title")!.PropertyType);
        Assert.AreEqual(typeof(TextObject), typeof(Incident).GetProperty("Description")!.PropertyType);
        Assert.AreEqual(typeof(int), typeof(Incident).GetProperty("NumOfOptions")!.PropertyType);
        var option = typeof(Incident).GetMethod("GetOptionText", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(option);
        Assert.AreEqual(typeof(TextObject), option!.ReturnType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheVanillaIncidentIds_AndTheirOptionCounts_EqualTheCatalog()
    {
        RequireGame();
        var register = AccessTools.Method(typeof(IncidentsCampaignBehaviour), "InitializeIncidents");
        Assert.IsNotNull(register, "IncidentsCampaignBehaviour.InitializeIncidents did not resolve.");

        var counts = new Dictionary<string, int>();
        string? current = null;
        foreach (var instruction in PatchProcessor.GetOriginalInstructions(register!))
        {
            if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string text && text.StartsWith("incident_", StringComparison.Ordinal))
            {
                current = text;
                counts[text] = 0;
            }
            else if (current != null && (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
                && instruction.operand is MethodInfo called && called.Name == "AddOption" && called.DeclaringType == typeof(Incident))
            {
                counts[current]++;
            }
        }

        var catalog = ShippedCatalog();
        CollectionAssert.AreEquivalent(counts.Keys.ToList(), catalog.Rules.Select(r => r.Id).ToList(), "the catalog and vanilla's registered ids differ");
        var wrong = catalog.Rules.Where(r => counts[r.Id] != r.Options).Select(r => r.Id + " vanilla " + counts[r.Id] + " catalog " + r.Options).ToList();
        Assert.AreEqual(0, wrong.Count, string.Join("; ", wrong));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheVariablesTheCatalogListsForPlaces_AreStillInVanillasIl()
    {
        RequireGame();
        // vanilla sets a place variable inside the condition lambda, a method of a compiler-generated nested class
        var methods = new List<MethodBase> { AccessTools.Method(typeof(IncidentsCampaignBehaviour), "InitializeIncidents")! };
        foreach (var nested in typeof(IncidentsCampaignBehaviour).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            methods.AddRange(nested.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(m => m.GetMethodBody() != null));
        var strings = methods.SelectMany(m => PatchProcessor.GetOriginalInstructions(m)).Where(i => i.opcode == OpCodes.Ldstr).Select(i => (string)i.operand).ToHashSet();

        foreach (var rule in ShippedCatalog().Rules)
            foreach (var variable in rule.Places.Select(p => p.Variable))
                Assert.IsTrue(strings.Contains(variable), rule.Id + " " + variable);
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void PatchCategory_AppliedToTheInstalledEngine_AttachesOnePostfix_AndTheJitAccepts()
    {
        const string id = "taom.tests.incidentvoices.jit";
        var harmony = new Harmony(id);
        try
        {
            PatchCategoryIndex.Build(typeof(IncidentVoicesModule).Assembly).Apply(harmony, IncidentVoicesModule.PatchCategory);

            var info = Harmony.GetPatchInfo(Target());
            Assert.IsNotNull(info, "CanIncidentBeInvoked was not patched: the class is not in the category");
            Assert.AreEqual(1, info!.Postfixes.Count(p => p.owner == id));
            Assert.AreEqual(0, info.Prefixes.Count(p => p.owner == id));
            Assert.AreEqual(0, info.Transpilers.Count(p => p.owner == id));
            Assert.AreEqual(0, info.Finalizers.Count(p => p.owner == id));
            RuntimeHelpers.PrepareMethod(Target().MethodHandle);
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }

    private static IncidentCatalog ShippedCatalog()
    {
        var paths = Substitute.For<TAOM.Core.Infrastructure.IPathService>();
        paths.ModuleDataPath.Returns(RepoPath("Main", "_Module", "ModuleData"));
        return new IncidentCatalogProvider(paths, Substitute.For<TAOM.Core.Logging.IModLogger>()).GetCatalog();
    }
}
