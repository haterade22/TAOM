using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FieldCamp.UI;

namespace TAOM.Tests.Features.FieldCamp;

/// <summary>
/// Wiring regression guard for the FieldCamp UI arc, in the HeroRaceWiringTests shape. Every seam
/// here fails SILENTLY when dropped: an unregistered service NREs nothing (the MapView just never
/// resolves it), an unapplied Patch74 category simply never draws the nameplate icon, a missing
/// prefab renders a blank layer, and a binding typo is a dead widget with no log (gui-ui.md). Each
/// is pinned as a red test instead. Binding assertions walk the ACTUAL prefab XML and collect
/// failures inside the loop (the Patch72 array-bounds lesson: assert inside the structure you
/// claim to check, so the test can fail by construction).
/// </summary>
[TestClass]
public class FieldCampWiringTests
{
    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static string PrefabPath => Path.Combine(
        RepoRoot, @"Main\_Module\GUI\PreFabs\FieldCamp\TaomFieldCampOverlay.xml");

    private static string SpriteDataPath => Path.Combine(
        RepoRoot, @"Main\_Module\GUI\TAOMSpriteData.xml");

    private static string BrushesDir => Path.Combine(RepoRoot, @"Main\_Module\GUI\Brushes");

    // Vanilla assets the overlay deliberately uses. Anything beyond this set must be registered
    // in TAOMSpriteData.xml / Main/_Module/GUI/Brushes or it renders blank with no error.
    private static readonly HashSet<string> KnownVanillaSprites = new HashSet<string>
    {
        "BlankWhiteSquare_9",
    };

    // Every entry here must be grep-verified against the installed game's Brushes/*.xml before
    // it is added: the allowlist previously vouched for Popup.Text.Medium, which existed nowhere
    // and silently rendered with the engine default brush (round-B critic finding).
    private static readonly HashSet<string> KnownVanillaBrushes = new HashSet<string>
    {
        "Popup.Frame",
        "ButtonBrush2",
        "Popup.Description.Text",
        "Popup.Button.Text",
    };

    private static string ReadSource(params string[] parts)
    {
        var path = Path.Combine(new[] { RepoRoot }.Concat(parts).ToArray());
        Assert.IsTrue(File.Exists(path), $"Expected source file not found: {path}");
        return File.ReadAllText(path);
    }

    // ---- IoC registrations (FieldCampIoC.cs is the orchestrator's file; this pins its content) ----

    [TestMethod]
    public void FieldCampIoC_RegistersEveryServiceTheUiResolves()
    {
        var src = ReadSource("Main", "Features", "FieldCamp", "FieldCampIoC.cs");

        StringAssert.Contains(src, "ICampSettingsProvider, CampSettingsProvider",
            "The settings provider is no longer registered; the MapView resolve throws at CreateLayout.");
        StringAssert.Contains(src, "ICampService, CampService",
            "The camp service is no longer registered; overlay, menus and ticks all go dead.");
        StringAssert.Contains(src, "ICampVisualService, CampVisualService",
            "The visual service is no longer registered; camp tents never appear or leak across sessions.");
        StringAssert.Contains(src, "ICampTerrainService, CampTerrainService",
            "The terrain service is no longer registered.");
        StringAssert.Contains(src, "ICampAmbushService, CampAmbushService",
            "The ambush service is no longer registered.");
        StringAssert.Contains(src, "IPartySpottingContributor, LookoutSpottingContributor",
            "The lookout spotting contributor is no longer registered; the lookout bonus silently "
            + "stops reaching TaomMapVisibilityModel.");
        StringAssert.Contains(src, "PartyNameplateCampIconPatch.Initialize",
            "FieldCampIoC no longer hands the camp service to Patch74; the patch null-guards a "
            + "missing service, so the nameplate icon silently never draws.");
    }

    [TestMethod]
    public void FieldCampIoC_RegistrationMethod_NeverResolvesEagerly()
    {
        // Round-A CRITICAL: an eager Resolve inside the registration method materializes
        // CampService's IEnumerable<ICampOverlayContributor> BEFORE Refuge registers its
        // contributor, baking the collection permanently empty (DryIoc snapshots injected
        // enumerables). Eager patch-static init lives in InitializePatchStatics, which
        // IoC.Configure calls after the LAST feature registration.
        var src = ReadSource("Main", "Features", "FieldCamp", "FieldCampIoC.cs");

        int registerBody = src.IndexOf("RegisterFieldCampFeature", StringComparison.Ordinal);
        int initStatics = src.IndexOf("InitializePatchStatics", StringComparison.Ordinal);
        Assert.IsTrue(registerBody >= 0 && initStatics > registerBody,
            "FieldCampIoC lost its RegisterFieldCampFeature / InitializePatchStatics split.");

        var registrationSection = src.Substring(registerBody, initStatics - registerBody);
        Assert.IsFalse(registrationSection.Contains("container.Resolve"),
            "RegisterFieldCampFeature resolves eagerly again; contributor collections registered "
            + "by later features (Refuge's camp-block) would be baked empty and silently dead.");
    }

    // ---- SubModule wiring (single-owner file; these pin the two lines FieldCamp depends on) ----

    [TestMethod]
    public void SubModule_AddsTheFieldCampBehavior()
    {
        var src = ReadSource("Main", "SubModule.cs");

        StringAssert.Contains(src, "new Features.FieldCamp.Hooks.FieldCampCampaignBehavior(",
            "SubModule.cs no longer adds FieldCampCampaignBehavior; menus, SyncData and every tick "
            + "fan-out are gone with no error.");
    }

    [TestMethod]
    public void SubModule_AppliesThePatch74Category()
    {
        var src = ReadSource("Main", "SubModule.cs");

        StringAssert.Contains(src, ".PatchCategory(\"Patch74_FieldCampNameplateIcon\")",
            "SubModule.cs no longer applies Patch74_FieldCampNameplateIcon, so Harmony is never "
            + "asked to apply the nameplate-icon postfix and the icon goes dead silently.");
    }

    // ---- Prefab existence + binding parity (SupplyOrderPrefabBindingTests pattern) ----

    private static XElement LoadPrefabRoot()
    {
        Assert.IsTrue(File.Exists(PrefabPath), $"prefab missing at {PrefabPath}");
        var root = XDocument.Load(PrefabPath).Root;
        Assert.IsNotNull(root, "prefab has no root element");
        return root!;
    }

    [TestMethod]
    public void Prefab_EveryBinding_ExistsOnTheOverlayVm()
    {
        var failures = new List<string>();
        Walk(LoadPrefabRoot(), failures);
        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    private static void Walk(XElement element, List<string> failures)
    {
        foreach (var attr in element.Attributes())
        {
            var name = attr.Name.LocalName;

            // This overlay binds a single flat VM; a DataSource collection appearing here means
            // the prefab grew a template this test does not know how to type-check.
            if (name == "DataSource")
            {
                failures.Add($"unexpected DataSource '{attr.Value}' on <{element.Name}>; teach this test its item VM type");
                continue;
            }

            if (name.StartsWith("Command.", StringComparison.Ordinal))
            {
                var method = typeof(FieldCampOverlayVM).GetMethod(
                    attr.Value, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (method == null)
                    failures.Add($"FieldCampOverlayVM has no public parameterless method '{attr.Value}' for {name}");
            }
            else if (attr.Value.StartsWith("@", StringComparison.Ordinal))
            {
                AssertBoundProperty(attr.Value.Substring(1), failures);
            }
        }

        foreach (var child in element.Elements())
            Walk(child, failures);
    }

    private static void AssertBoundProperty(string propertyName, List<string> failures)
    {
        var property = typeof(FieldCampOverlayVM).GetProperty(
            propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property == null)
        {
            failures.Add($"FieldCampOverlayVM has no public property '{propertyName}'");
            return;
        }

        // Gauntlet only refreshes properties carrying [DataSourceProperty]; a bare property binds
        // once and then never updates.
        var hasAttribute = property.GetCustomAttributes(true)
            .Any(a => a.GetType().Name.StartsWith("DataSourceProperty", StringComparison.Ordinal));
        if (!hasAttribute)
            failures.Add($"FieldCampOverlayVM.{propertyName} is bound in the prefab but lacks [DataSourceProperty]");
    }

    [TestMethod]
    public void OverlayVm_EveryDataSourceProperty_IsBoundInThePrefab()
    {
        var boundNames = new HashSet<string>(StringComparer.Ordinal);
        CollectBoundNames(LoadPrefabRoot(), boundNames);

        var failures = new List<string>();
        foreach (var property in typeof(FieldCampOverlayVM).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var hasAttribute = property.GetCustomAttributes(true)
                .Any(a => a.GetType().Name.StartsWith("DataSourceProperty", StringComparison.Ordinal));
            if (hasAttribute && !boundNames.Contains(property.Name))
                failures.Add($"FieldCampOverlayVM.{property.Name} is [DataSourceProperty] but nothing in "
                    + "the prefab binds it (dead code per gui-ui.md; the source module shipped four of these)");
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    private static void CollectBoundNames(XElement element, HashSet<string> names)
    {
        foreach (var attr in element.Attributes())
        {
            if (attr.Value.StartsWith("@", StringComparison.Ordinal))
                names.Add(attr.Value.Substring(1));
        }

        foreach (var child in element.Elements())
            CollectBoundNames(child, names);
    }

    [TestMethod]
    public void Prefab_EverySpriteAndBrush_IsVanillaKnownOrRegistered()
    {
        var allowedSprites = new HashSet<string>(KnownVanillaSprites, StringComparer.Ordinal);
        Assert.IsTrue(File.Exists(SpriteDataPath), $"TAOMSpriteData.xml missing at {SpriteDataPath}");
        foreach (var nameElement in XDocument.Load(SpriteDataPath).Descendants("Name"))
            allowedSprites.Add(nameElement.Value);

        var allowedBrushes = new HashSet<string>(KnownVanillaBrushes, StringComparer.Ordinal);
        if (Directory.Exists(BrushesDir))
        {
            foreach (var brushFile in Directory.GetFiles(BrushesDir, "*.xml"))
            {
                foreach (var brush in XDocument.Load(brushFile).Descendants("Brush"))
                {
                    var name = brush.Attribute("Name")?.Value;
                    if (!string.IsNullOrEmpty(name))
                        allowedBrushes.Add(name!);
                }
            }
        }

        var failures = new List<string>();
        foreach (var element in LoadPrefabRoot().DescendantsAndSelf())
        {
            var sprite = element.Attribute("Sprite")?.Value;
            if (sprite != null && !allowedSprites.Contains(sprite))
                failures.Add($"Sprite '{sprite}' is neither known-vanilla nor registered in TAOMSpriteData.xml (renders blank silently)");

            var brush = element.Attribute("Brush")?.Value;
            if (brush != null && !allowedBrushes.Contains(brush))
                failures.Add($"Brush '{brush}' is neither known-vanilla nor declared in Main/_Module/GUI/Brushes (renders default-styled silently)");
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }
}
