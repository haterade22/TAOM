using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features;
using TAOM.Features.CultureDoctrine;
using TAOM.Features.CultureDoctrine.Domain;

namespace TAOM.Tests.Features.CultureDoctrine;

/// <summary>
/// Read per agent stat update (TaomAgentStatCalculateModel through CultureAggressionService), so the
/// provider caches the MCM reference and reads through it. <c>TaomSettings.Instance</c> is null in
/// tests, so the public constructor pins today's no-MCM fallbacks. The Morale and Aggression
/// fallbacks are false while their compiled MCM defaults are true; that mismatch predates the cache
/// and is pinned as it stands, not endorsed.
/// </summary>
[TestClass]
public class CultureDoctrineSettingsProviderTests
{
    private static ICultureDoctrineConfigProvider ConfigWithCatalog(bool enabled)
    {
        var catalog = new DoctrineCatalog(enabled, DoctrineCatalog.VanillaDefault(), new Doctrine[0]);
        var config = Substitute.For<ICultureDoctrineConfigProvider>();
        config.GetCatalog().Returns(catalog);
        return config;
    }

    [TestMethod]
    public void Getters_NoMcm_ReturnTodaysFallbacks()
    {
        var sut = new CultureDoctrineSettingsProvider(ConfigWithCatalog(enabled: true));

        Assert.IsFalse(sut.IsEnabled, nameof(sut.IsEnabled));
        Assert.IsFalse(sut.IsDebug, nameof(sut.IsDebug));
        Assert.IsFalse(sut.IsMoraleEnabled, nameof(sut.IsMoraleEnabled));
        Assert.IsFalse(sut.IsAggressionEnabled, nameof(sut.IsAggressionEnabled));
    }

    // Read THROUGH the cached reference: edits made after every getter has been read once must reach
    // their getters. One fresh settings object and provider per row.
    [TestMethod]
    public void Getters_ReadThroughTheCachedSettings_SoLiveMcmEditsApply()
    {
        var rows = new (bool CatalogEnabled, Action<TaomSettings> Edit, Func<CultureDoctrineSettingsProvider, bool> Get, bool Expected, string Name)[]
        {
            (true, s => s.EnableCultureDoctrine = true, p => p.IsEnabled, true, "IsEnabled"),
            (true, s => s.EnableCultureDoctrine = true, p => p.IsMoraleEnabled, true, "IsMoraleEnabled"),
            (true, s => s.EnableCultureDoctrine = true, p => p.IsAggressionEnabled, true, "IsAggressionEnabled"),
            (true, s => { s.EnableCultureDoctrine = true; s.CultureDoctrineMorale = false; }, p => p.IsMoraleEnabled, false, "IsMoraleEnabled, morale off"),
            (true, s => { s.EnableCultureDoctrine = true; s.CultureDoctrineAggression = false; }, p => p.IsAggressionEnabled, false, "IsAggressionEnabled, aggression off"),
            (true, s => s.CultureDoctrineDebug = true, p => p.IsDebug, true, "IsDebug"),
            (false, s => s.EnableCultureDoctrine = true, p => p.IsEnabled, false, "IsEnabled, catalog disabled"),
        };

        foreach (var row in rows)
        {
            var mcm = new TaomSettings();
            var sut = new CultureDoctrineSettingsProvider(ConfigWithCatalog(row.CatalogEnabled), mcm);
            foreach (var p in typeof(ICultureDoctrineSettingsProvider).GetProperties())
                _ = p.GetValue(sut);

            row.Edit(mcm);

            Assert.AreEqual(row.Expected, row.Get(sut), row.Name);
        }
    }
}
