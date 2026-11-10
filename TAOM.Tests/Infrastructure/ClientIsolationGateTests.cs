using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Bannerlord.UIExtenderEx.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CoopInterop;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Core never references TAOM.Client (ADR-013). The client submodule reaches core, never the reverse;
/// core sees the client only through TAOM.Composition.ClientComposition.
/// </summary>
[TestClass]
public class ClientIsolationGateTests
{
    [TestMethod]
    [TestCategory("RequiresGame")]
    public void CoreAssemblies_DoNotReferenceTaomClient()
    {
        var core = new[] { typeof(TAOM.SubModule).Assembly, typeof(TAOM.Dependencies.SubModule).Assembly };

        var offenders = core
            .SelectMany(a => a.GetReferencedAssemblies().Select(r => $"{a.GetName().Name} -> {r.Name}"))
            .Where(s => s.EndsWith("-> TAOM.Client", StringComparison.Ordinal))
            .ToList();

        CollectionAssert.AreEqual(new string[0], offenders,
            "Core reaches the client only through ClientComposition (ADR-013).");
    }

    // Each of these is found by a scan over Main (or the built assembly) that does not read Client/.
    // Wire the scan first, then move the declaration. Patches and UIExtenderEx extensions are not on the list
    // any more: B5 made the patch-category pass (LoadedTaomAssemblies) and the UIExtenderEx registration
    // (ClientSubModule) read TAOM.Client, and TaomCore_DeclaresNoUiExtension pins the core side.
    private static readonly Regex ForbiddenInTheClient = new Regex(
        @"\bSaveableTypeDefiner\b"
        + @"|\b(AttributeGlobalSettings|AttributePerCampaignSettings|AttributePerSaveSettings)\b|\bMCM\.Abstractions\b");

    [TestMethod]
    public void ClientSource_DeclaresNoSaveDefinerOrSettingsType()
    {
        var files = RepoPaths.CSharpFiles("Client").ToList();
        Assert.IsTrue(files.Count > 0, "The scan read no file under Client/.");

        var offenders = files
            .Where(f => ForbiddenInTheClient.IsMatch(RepoPaths.StripComments(File.ReadAllText(f))))
            .Select(Path.GetFileName)
            .ToList();

        CollectionAssert.AreEqual(new string[0], offenders,
            "TAOM.Client may not declare a SaveableTypeDefiner or an MCM settings type until the scans that find them "
            + "also read Client/: the save definer registration and the settings scans.");
    }

    [DataTestMethod]
    [DataRow("class D : SaveableTypeDefiner")]
    [DataRow("class S : AttributeGlobalSettings<S>")]
    [DataRow("class S : AttributePerCampaignSettings<S>")]
    [DataRow("class S : AttributePerSaveSettings<S>")]
    [DataRow("using MCM.Abstractions.Base;")]
    [DataRow("class D : TaleWorlds.SaveSystem.SaveableTypeDefiner")]
    [DataRow("class S : MCM.Abstractions.Base.Global.AttributeGlobalSettings<S>")]
    public void TripwirePattern_MatchesEachForbiddenDeclaration(string source) =>
        Assert.IsTrue(ForbiddenInTheClient.IsMatch(source), source);

    // The scan is a word search, so a subclass of a non-sealed core definer (class D : TaomSaveDefiner) escapes it.
    [DataTestMethod]
    [DataRow("[SaveableField(1)]")]
    [DataRow("class PresetSaveableTypeDefinerTests")]
    public void TripwirePattern_IgnoresSaveAttributesAndLookalikeNames(string source) =>
        Assert.IsFalse(ForbiddenInTheClient.IsMatch(source), source);

    // UIExtenderEx's Register(Assembly) reads one assembly and refuses a second call under one module name, so TAOM
    // registers once, from ClientSubModule.OnGameStart, and every extension lives in TAOM.Client (ADR-013).
    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TaomCore_DeclaresNoUiExtension()
    {
        if (!TAOM.Tests.Migration.GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("No game install resolved: the core types cannot be read.");

        // Strict GetTypes (a partial load throws) and UIExtenderEx's own selection: any custom attribute whose type
        // derives from BaseUIExtenderAttribute.
        var coreTypes = typeof(TAOM.SubModule).Assembly.GetTypes();
        var inCore = coreTypes
            .Where(t => t.CustomAttributes.Any(a => a.AttributeType.IsSubclassOf(typeof(BaseUIExtenderAttribute))))
            .Select(t => t.FullName!)
            .OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.IsTrue(coreTypes.Length > 1000, "The scan read only " + coreTypes.Length + " core types.");
        CollectionAssert.AreEqual(new string[0], inCore,
            "UIExtenderEx reads only TAOM.Client (ClientSubModule.OnGameStart), so a core extension would never load.");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TaomClient_DeclaresEveryUiExtension()
    {
        TAOM.Tests.Migration.GameAssemblies.EnsureLoaded();

        var inClient = CoopUiRegistrationPolicy.CollectUiExtensionTypes(typeof(TAOM.Client.ClientSubModule).Assembly);

        Assert.AreEqual(59, inClient.Count,
            "27 view model mixins and 32 prefab extension classes: " + string.Join(", ", inClient.Select(t => t.Name)));
        var names = inClient.Select(t => t.Name).ToArray();
        // #1095 circular battle HUD: its ring values, and the native AgentStatus bars and previews it replaces.
        foreach (var battleHud in new[]
        {
            "MissionAgentStatusBattleHudMixin",
            "BattleHudHideNativeHealthBar", "BattleHudHideNativeHorseBar", "BattleHudHideNativeShieldBar",
            "BattleHudHidePrimaryWeaponPreview", "BattleHudHideOffhandWeaponPreview",
            "BattleHudMoveWeaponColumn", "BattleHudHideNativeAmmoCounter", "BattleHudMoveLanceRow",
        })
            CollectionAssert.Contains(names, battleHud, "the battle HUD extension " + battleHud);
        CollectionAssert.Contains(names, "EncyclopediaClanGalleryMixin", "the clan page roster, fief galleries and leader model");
        CollectionAssert.Contains(names, "EncyclopediaRelationshipGalleryMixin", "the hero page relationship galleries");
        CollectionAssert.Contains(names, "EncyclopediaKingdomGalleryMixin", "the kingdom page clan and fief galleries");
        CollectionAssert.AreEquivalent(
            new[]
            {
                "TimeAccelerationMixin", "PrefabCenterPanel", "PrefabFastForwardButton", "PrefabPlayButton",
                "PrefabPauseButton", "PrefabInsertExtraFastForward",
                "MakeToolsMixin", "MakeToolsPrefab",
            },
            CoopUiRegistrationPolicy.Suppressed(inClient).Select(t => t.Name).ToArray(),
            "the co-op registration drops exactly the time-control extensions and the Make Tools card");
    }

    [TestMethod]
    public void CoreSource_NamesTaomClientOnlyInComments()
    {
        var repoDir = RepoPaths.RepoPath();
        var offenders = RepoPaths.CSharpFiles("Main", "Dependencies")
            .Select(f => f.Substring(repoDir.Length).TrimStart('\\', '/').Replace('\\', '/'))
            .Where(rel => RepoPaths.MentionsOutsideComments(Path.Combine(repoDir, rel), "\"TAOM.Client"))
            .ToList();

        CollectionAssert.AreEqual(new string[0], offenders,
            "A string literal naming TAOM.Client (Type.GetType, Assembly.Load) is a back door; core reaches the client only through ClientComposition (ADR-013).");
    }
}
