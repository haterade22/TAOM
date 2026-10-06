using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.CreatureBandits;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;
using TaleWorlds.Core;

namespace TAOM.Tests.Features.CreatureBandits;

/// <summary>
/// The creature bandits' live numbers (#746). The damage step asks for one damage-taken factor on every hit against a
/// creature, on the damage model's thread, so <see cref="CreatureBanditTuning.CurrentTakenFactor"/> reads the kept MCM
/// object and allocates nothing, and must answer exactly what the full tuning's <c>TakenFactor</c> does. The MCM object
/// is taken once and read through, so an edit applies at once (HotPathSettingsProvidersTests holds the IL rule).
/// </summary>
[TestClass]
public class CreatureBanditTuningLiveTests
{
    [TestCleanup]
    public void Cleanup() => CreatureBanditTuning.UseSettings(null);

    [DataTestMethod]
    [DataRow(50, 100, 100, 100)]
    [DataRow(20, 75, 140, 0)]
    [DataRow(-5, 400, 60, 101)]
    [DataRow(0, 0, 0, 0)]
    public void CurrentTakenFactor_MatchesTheFullTuningsRule(int missile, int cut, int pierce, int blunt)
    {
        var settings = new TaomSettings
        {
            CreatureBanditMissileTakenPercent = missile, CreatureBanditCutTakenPercent = cut,
            CreatureBanditPierceTakenPercent = pierce, CreatureBanditBluntTakenPercent = blunt,
        };
        CreatureBanditTuning.UseSettings(settings);
        var full = CreatureBanditTuning.Current;

        foreach (var isMissile in new[] { false, true })
        foreach (var bluntByRule in new[] { false, true })
        foreach (var type in new[] { DamageTypes.Cut, DamageTypes.Pierce, DamageTypes.Blunt, DamageTypes.Invalid })
            Assert.AreEqual(full.TakenFactor(isMissile, type, bluntByRule), CreatureBanditTuning.CurrentTakenFactor(isMissile, type, bluntByRule),
                $"missile={isMissile} blunt={bluntByRule} type={type}");
    }

    [TestMethod]
    public void CurrentTakenFactor_ReadsAnEditAtOnce()
    {
        var settings = new TaomSettings { CreatureBanditCutTakenPercent = 100 };
        CreatureBanditTuning.UseSettings(settings);
        Assert.AreEqual(1f, CreatureBanditTuning.CurrentTakenFactor(false, DamageTypes.Cut));

        settings.CreatureBanditCutTakenPercent = 40;

        Assert.AreEqual(0.4f, CreatureBanditTuning.CurrentTakenFactor(false, DamageTypes.Cut), 1e-6f);
        Assert.AreEqual(0.4f, CreatureBanditTuning.Current.CutTakenFactor, 1e-6f);
    }

    // HotPathSettingsProvidersTests' counting test constructs each provider; this static has no instance, so the same
    // check runs here. The read-through tests install their object with UseSettings, which an accessor that never keeps
    // the reference would pass too.
    [TestMethod]
    public void CurrentTakenFactor_AsksMcmUntilItHasTheSettings_ThenNeverAgain_AndReadsThrough()
    {
        CreatureBanditTuning.UseSettings(null);
        using var mcm = new CountingMcm();

        // MCM is not up: the defaults apply, and every read asks again.
        Assert.AreEqual(1f, CreatureBanditTuning.CurrentTakenFactor(false, DamageTypes.Cut));
        int whileDown = mcm.Lookups;
        Assert.IsTrue(whileDown >= 1, "a read resolves the settings through MCM");
        CreatureBanditTuning.CurrentTakenFactor(false, DamageTypes.Cut);
        Assert.IsTrue(mcm.Lookups > whileDown, "a read while MCM has nothing must ask again, or a null is pinned for the session");

        // MCM comes up: the next read takes its value.
        var settings = new TaomSettings { CreatureBanditCutTakenPercent = 40 };
        mcm.Register(settings);
        Assert.AreEqual(0.4f, CreatureBanditTuning.CurrentTakenFactor(false, DamageTypes.Cut), 1e-6f);
        int resolved = mcm.Lookups;

        // Kept: further reads, per hit and per spawn, never ask MCM.
        for (int i = 0; i < 5; i++)
            CreatureBanditTuning.CurrentTakenFactor(false, DamageTypes.Cut);
        _ = CreatureBanditTuning.Current;
        Assert.AreEqual(resolved, mcm.Lookups, "once resolved, the settings object must be kept; every creature hit walks MCM's containers otherwise");

        // Read through: an edit to that same object shows on the next read, with no new lookup.
        settings.CreatureBanditCutTakenPercent = 70;
        Assert.AreEqual(0.7f, CreatureBanditTuning.CurrentTakenFactor(false, DamageTypes.Cut), 1e-6f);
        Assert.AreEqual(resolved, mcm.Lookups, "reading an edit must not look the object up again");
    }

    [TestMethod]
    public void CurrentTakenFactor_NoMcm_IsTheDefaults()
    {
        foreach (var isMissile in new[] { false, true })
        foreach (var type in new[] { DamageTypes.Cut, DamageTypes.Pierce, DamageTypes.Blunt })
            Assert.AreEqual(CreatureBanditTuning.Defaults.TakenFactor(isMissile, type), CreatureBanditTuning.CurrentTakenFactor(isMissile, type));
    }

    [TestMethod]
    public void CurrentTakenFactor_AllocatesNothing()
    {
        var method = typeof(CreatureBanditTuning).GetMethod(nameof(CreatureBanditTuning.CurrentTakenFactor), BindingFlags.Public | BindingFlags.Static)!;
        var found = IlCallScanner.ExtractOpCodes(method.GetMethodBody()!.GetILAsByteArray()!)
            .Where(op => op == OpCodes.Newobj || op == OpCodes.Newarr || op == OpCodes.Box)
            .Select(op => op.Name)
            .ToList();

        Assert.AreEqual(0, found.Count, "CurrentTakenFactor runs on every creature hit and must allocate nothing: " + string.Join(", ", found));
    }

    [TestMethod]
    public void Damage_AsksTheAllocationFreeFactor()
    {
        var src = RepoPaths.ReadSource("Main/Features/CreatureBandits/Hooks/CreatureBanditDamage.cs", stripComments: true);

        // The statement that applies the factor, so a dropped result fails here, not only a dropped call.
        StringAssert.Contains(src, "return damage * CreatureBanditTuning.CurrentTakenFactor(collisionData.IsMissile, (DamageTypes)collisionData.DamageType,");
        Assert.IsFalse(src.Contains("CreatureBanditTuning.Current."), "the per-hit step must not build the full tuning");
    }
}
