using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features;
using TAOM.Features.DreadAura;
using TAOM.Features.DreadAura.Domain;

namespace TAOM.Tests.Features.DreadAura;

/// <summary>
/// Read every frame by DreadAuraMissionLogic, so the provider caches the MCM reference and reads
/// through it. <c>TaomSettings.Instance</c> is null in tests, so the public constructor pins the
/// no-MCM fallbacks, which come from the validated JSON.
/// </summary>
[TestClass]
public class DreadAuraSettingsProviderTests
{
    private static IDreadAuraConfigProvider ConfigReturning(DreadAuraConfig config)
    {
        var provider = Substitute.For<IDreadAuraConfigProvider>();
        provider.GetConfig().Returns(config);
        return provider;
    }

    [TestMethod]
    public void Getters_NoMcm_FallBackToJson()
    {
        var config = new DreadAuraConfig { Enabled = false };
        config.Profile.Radius = 15f;
        config.Profile.MoralePerSecond = 6f;

        var sut = new DreadAuraSettingsProvider(ConfigReturning(config));

        Assert.IsFalse(sut.IsEnabled);
        Assert.AreEqual(15f, sut.Radius, 0.0001f);
        Assert.AreEqual(6f, sut.MoralePerSecond, 0.0001f);
        Assert.IsTrue(sut.AffectsPlayerTroops);
        Assert.AreEqual(config.Profile.InnerRadius, sut.InnerRadius, 0.0001f);
    }

    // Read THROUGH the cached reference: an edit made after every getter has been read once must
    // reach its own getter. One fresh settings object and provider per row.
    [TestMethod]
    public void Getters_ReadThroughTheCachedSettings_SoLiveMcmEditsApply()
    {
        var rows = new (Action<TaomSettings> Edit, Func<DreadAuraSettingsProvider, object> Get, object Expected)[]
        {
            (s => s.EnableDreadAura = false, p => p.IsEnabled, false),
            (s => s.DreadAuraRadius = 20f, p => p.Radius, 20f),
            (s => s.DreadAuraMoralePerSecond = 8f, p => p.MoralePerSecond, 8f),
            (s => s.DreadAuraAffectsPlayerTroops = false, p => p.AffectsPlayerTroops, false),
        };

        for (int i = 0; i < rows.Length; i++)
        {
            var mcm = new TaomSettings();
            var sut = new DreadAuraSettingsProvider(ConfigReturning(new DreadAuraConfig()), mcm);
            foreach (var p in typeof(IDreadAuraSettingsProvider).GetProperties())
                _ = p.GetValue(sut);

            rows[i].Edit(mcm);

            Assert.AreEqual(rows[i].Expected, rows[i].Get(sut), "row " + i);
        }
    }
}
