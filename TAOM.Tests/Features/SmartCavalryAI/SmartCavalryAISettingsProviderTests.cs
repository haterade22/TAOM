using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.SmartCavalryAI;

namespace TAOM.Tests.Features.SmartCavalryAI;

/// <summary>
/// Read every frame, and per cavalry formation, by SmartCavalryAIMissionBehavior, so the provider
/// caches the MCM reference and reads through it. <c>TaomSettings.Instance</c> is null in tests, so
/// the public constructor pins the no-MCM fallbacks.
/// </summary>
[TestClass]
public class SmartCavalryAISettingsProviderTests
{
    [TestMethod]
    public void Getters_NoMcm_ReturnTheCompiledFallbacks()
    {
        var sut = new SmartCavalryAISettingsProvider();

        Assert.IsFalse(sut.IsEnabled, nameof(sut.IsEnabled));
        Assert.IsTrue(sut.AvoidFriendlies, nameof(sut.AvoidFriendlies));
        Assert.AreEqual(0.7f, sut.ChargeFormationStrictness, 0.0001f, nameof(sut.ChargeFormationStrictness));
        Assert.AreEqual(25f, sut.ReformDistanceAfterCharge, 0.0001f, nameof(sut.ReformDistanceAfterCharge));
        Assert.AreEqual(1.2f, sut.ChargeLineSpacing, 0.0001f, nameof(sut.ChargeLineSpacing));
        Assert.AreEqual(4f, sut.MaxLineUpSeconds, 0.0001f, nameof(sut.MaxLineUpSeconds));
        Assert.IsFalse(sut.IsDebugMode, nameof(sut.IsDebugMode));
    }

    // Read THROUGH the cached reference: an edit made after every getter has been read once must
    // reach its own getter. One fresh settings object and provider per row.
    [TestMethod]
    public void Getters_ReadThroughTheCachedSettings_SoLiveMcmEditsApply()
    {
        var rows = new (Action<TaomSettings> Edit, Func<SmartCavalryAISettingsProvider, object> Get, object Expected)[]
        {
            (s => s.EnableSmartCavalryAI = true, p => p.IsEnabled, true),
            (s => s.SmartCavalryAvoidFriendlies = false, p => p.AvoidFriendlies, false),
            (s => s.SmartCavalryChargeStrictness = 0.3f, p => p.ChargeFormationStrictness, 0.3f),
            (s => s.SmartCavalryReformDistance = 40f, p => p.ReformDistanceAfterCharge, 40f),
            (s => s.SmartCavalryLineSpacing = 2f, p => p.ChargeLineSpacing, 2f),
            (s => s.SmartCavalryMaxLineUpSeconds = 8f, p => p.MaxLineUpSeconds, 8f),
            (s => s.SmartCavalryDebug = true, p => p.IsDebugMode, true),
        };

        for (int i = 0; i < rows.Length; i++)
        {
            var mcm = new TaomSettings();
            var sut = new SmartCavalryAISettingsProvider(mcm);
            foreach (var p in typeof(ISmartCavalryAISettingsProvider).GetProperties())
                _ = p.GetValue(sut);

            rows[i].Edit(mcm);

            Assert.AreEqual(rows[i].Expected, rows[i].Get(sut), "row " + i);
        }
    }
}
