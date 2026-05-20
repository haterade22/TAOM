using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Tests.Features.CultureMarketplace;

/// <summary>
/// Regression tests for the prefix-fallback table in ItemPoolAdapter. These exercise the
/// private ResolveByPrefix(string) helper via reflection. Real LOTRLOME item IDs (Mirkwood
/// crafted weapons + wm_harad_glaive) were silently dropped from injection because they had
/// no culture attribute AND no PrefixMap row — Codex review 2026-05-20 (C3).
/// </summary>
[TestClass]
public class ItemPoolAdapterPrefixTests
{
    private static string ResolveByPrefix(string itemId)
    {
        var type = typeof(ItemPoolAdapter);
        var method = type.GetMethod("ResolveByPrefix", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "ResolveByPrefix method not found on ItemPoolAdapter");
        return (string)method.Invoke(null, new object[] { itemId });
    }

    [TestMethod]
    public void ResolveByPrefix_MirkwoodCraftedWeapon_ResolvesToMirkwood()
    {
        Assert.AreEqual("mirkwood", ResolveByPrefix("mirkwood_sword_a01"));
        Assert.AreEqual("mirkwood", ResolveByPrefix("mirkwood_spear_a01"));
        Assert.AreEqual("mirkwood", ResolveByPrefix("mirkwood_glaive_a01"));
    }

    [TestMethod]
    public void ResolveByPrefix_HaradCraftedWeapon_ResolvesToAserai()
    {
        Assert.AreEqual("aserai", ResolveByPrefix("wm_harad_glaive_a01"));
    }

    // Existing prefixes — guard against accidental removal.

    [TestMethod]
    public void ResolveByPrefix_GondorIdPrefixes_ResolveToGondor()
    {
        Assert.AreEqual("gondor", ResolveByPrefix("sk_gd_ano_gloves_a"));
        Assert.AreEqual("gondor", ResolveByPrefix("ithilien_bracers"));
        Assert.AreEqual("gondor", ResolveByPrefix("gondor_swan_horse_armor_1"));
        Assert.AreEqual("gondor", ResolveByPrefix("anduril"));
    }

    [TestMethod]
    public void ResolveByPrefix_MordorIdPrefixes_ResolveToMordor()
    {
        Assert.AreEqual("mordor", ResolveByPrefix("sm_mordor_shield_mid_a"));
        Assert.AreEqual("mordor", ResolveByPrefix("morannon_armor"));
        Assert.AreEqual("mordor", ResolveByPrefix("morgul_armor_a"));
        Assert.AreEqual("mordor", ResolveByPrefix("witchking_sword"));
    }

    [TestMethod]
    public void ResolveByPrefix_RohanAndDunlandPrefixes_ResolveToVanillaCultureIds()
    {
        Assert.AreEqual("vlandia", ResolveByPrefix("rohan_horse_armor_scalemail"));
        Assert.AreEqual("vlandia", ResolveByPrefix("whiterun_bracers"));
        Assert.AreEqual("vlandia", ResolveByPrefix("cts_rohan_shield"));
        Assert.AreEqual("empire", ResolveByPrefix("dunland_caerdh_chainmail_elite_a"));
    }

    [TestMethod]
    public void ResolveByPrefix_NullOrEmptyId_ReturnsNull()
    {
        Assert.IsNull(ResolveByPrefix(null));
        Assert.IsNull(ResolveByPrefix(""));
    }

    [TestMethod]
    public void ResolveByPrefix_UnknownPrefix_ReturnsNull()
    {
        Assert.IsNull(ResolveByPrefix("aelorothian_phantom_blade"));
    }
}
