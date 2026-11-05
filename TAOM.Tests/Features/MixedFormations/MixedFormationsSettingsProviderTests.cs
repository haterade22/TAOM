using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.MixedFormations;
using TAOM.Features.MixedFormations.Models;

namespace TAOM.Tests.Features.MixedFormations;

/// <summary>
/// Read every frame by MixedFormationsMissionBehavior and per unit by Patch30, so the provider caches
/// the MCM reference and reads through it. <c>TaomSettings.Instance</c> is null in tests (MCM is never
/// initialised), so the public constructor pins the no-MCM fallbacks.
/// </summary>
[TestClass]
public class MixedFormationsSettingsProviderTests
{
    [TestMethod]
    public void IsEnabled_NoMcm_DefaultsTrue()
        => Assert.IsTrue(new MixedFormationsSettingsProvider().IsEnabled);

    [TestMethod]
    public void DefaultLayout_NoMcm_DefaultsInfantryFrontRangedBack()
        => Assert.AreEqual(FormationLayoutType.InfantryFrontRangedBack, new MixedFormationsSettingsProvider().DefaultLayout);

    [TestMethod]
    public void CycleHotkey_NoMcm_DefaultsL()
        => Assert.AreEqual("L", new MixedFormationsSettingsProvider().CycleHotkey);

    [TestMethod]
    public void IsDebugMode_NoMcm_DefaultsFalse()
        => Assert.IsFalse(new MixedFormationsSettingsProvider().IsDebugMode);

    // Read THROUGH the cached reference: an edit made after every getter has been read once must
    // reach its own getter. One fresh settings object and provider per row.
    [TestMethod]
    public void Getters_ReadThroughTheCachedSettings_SoLiveMcmEditsApply()
    {
        var rows = new (Action<TaomSettings> Edit, Func<MixedFormationsSettingsProvider, object> Get, object Expected)[]
        {
            (s => s.EnableMixedFormations = false, p => p.IsEnabled, false),
            (s => s.MixedFormationsDefaultLayout = 3, p => p.DefaultLayout, FormationLayoutType.Checkerboard),
            (s => s.MixedFormationsCycleHotkey = "K", p => p.CycleHotkey, "K"),
            (s => s.MixedFormationsDebug = true, p => p.IsDebugMode, true),
        };

        for (int i = 0; i < rows.Length; i++)
        {
            var mcm = new TaomSettings();
            var sut = new MixedFormationsSettingsProvider(mcm);
            foreach (var p in typeof(IMixedFormationsSettingsProvider).GetProperties())
                _ = p.GetValue(sut);

            rows[i].Edit(mcm);

            Assert.AreEqual(rows[i].Expected, rows[i].Get(sut), "row " + i);
        }
    }
}
