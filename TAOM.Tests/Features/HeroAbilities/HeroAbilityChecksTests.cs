using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroAbilities.Hooks;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Tests.Features.HeroAbilities;

/// <summary>
/// The verdicts behind the #1359 [Check] lines (tools/ingame_checks/1359.toml). The detail strings are pinned word for word,
/// because the manifest's pass patterns read them.
/// </summary>
[TestClass]
public class HeroAbilityChecksTests
{
    [TestMethod]
    public void Gate_CombatConfigAndKits_Pass()
    {
        var v = HeroAbilityChecks.Gate(longDefeat: true, combat: true, config: true, kits: 2);

        Assert.AreEqual(CheckResult.Pass, v.Result);
        Assert.AreEqual("long_defeat=1 combat=1 config=1 kits=2", v.Detail);
    }

    [TestMethod]
    public void Gate_NotACombatMission_Fail()
    {
        var v = HeroAbilityChecks.Gate(false, combat: false, config: true, kits: 2);

        Assert.AreEqual(CheckResult.Fail, v.Result);
        Assert.AreEqual("long_defeat=0 combat=0 config=1 kits=2", v.Detail);
    }

    [TestMethod]
    public void Gate_ConfigOff_SkipConfigOff()
    {
        var v = HeroAbilityChecks.Gate(true, combat: true, config: false, kits: 2);

        Assert.AreEqual(CheckResult.Skip, v.Result);
        Assert.AreEqual("reason=config-off long_defeat=1 combat=1 config=0 kits=2", v.Detail);
    }

    [TestMethod]
    public void Gate_NoKits_Skip()
    {
        Assert.AreEqual(CheckResult.Skip, HeroAbilityChecks.Gate(true, true, true, kits: 0).Result);
    }

    [TestMethod]
    public void Fired_OneCast_Pass()
    {
        var v = HeroAbilityChecks.Fired("taom_hero_gimli", fired: 1, contactSeconds: 0, allies: 5, shocked: 3, owned: true);

        Assert.AreEqual(CheckResult.Pass, v.Result);
        Assert.AreEqual("hero=taom_hero_gimli fired=1 contact_s=0 allies=5 shocked=3 owned=1", v.Detail);
    }

    [TestMethod]
    public void Fired_NoneAfterLongContact_Fail()
    {
        var v = HeroAbilityChecks.Fired("taom_hero_aragorn", 0, contactSeconds: 20, 0, 0, owned: true);

        Assert.AreEqual(CheckResult.Fail, v.Result);
        Assert.AreEqual("hero=taom_hero_aragorn fired=0 contact_s=20 allies=0 shocked=0 owned=1", v.Detail);
    }

    [TestMethod]
    public void Fired_NoneAfterShortContact_SkipNoContact()
    {
        var v = HeroAbilityChecks.Fired("taom_hero_aragorn", 0, contactSeconds: 19, 0, 0, owned: true);

        Assert.AreEqual(CheckResult.Skip, v.Result);
        Assert.AreEqual("reason=no-contact hero=taom_hero_aragorn fired=0 contact_s=19 allies=0 shocked=0 owned=1", v.Detail);
    }

    [TestMethod]
    public void Fired_NeverOwnedHere_SkipNotOwned()
    {
        var v = HeroAbilityChecks.Fired("taom_hero_aragorn", 0, contactSeconds: 60, 0, 0, owned: false);

        Assert.AreEqual(CheckResult.Skip, v.Result);
        StringAssert.StartsWith(v.Detail, "reason=not-owned ");
    }

    [TestMethod]
    public void Stats_WithinOnePercent_Pass()
    {
        var v = HeroAbilityChecks.Stats("gimli_baruk_khazad", "taom_hero_gimli", ratio: 1.2f, want: 1.2f);

        Assert.AreEqual(CheckResult.Pass, v.Result);
        Assert.AreEqual("ability=gimli_baruk_khazad troop=taom_hero_gimli ratio=1.200 want=1.200", v.Detail);
    }

    [TestMethod]
    public void Stats_StatsDidNotMove_Fail()
    {
        Assert.AreEqual(CheckResult.Fail, HeroAbilityChecks.Stats("a", "t", ratio: 1f, want: 1.2f).Result);
        Assert.AreEqual(CheckResult.Fail, HeroAbilityChecks.Stats("a", "t", ratio: 1.22f, want: 1.2f).Result);
    }

    [TestMethod]
    public void Stats_NaNOrZero_Fail()
    {
        Assert.AreEqual(CheckResult.Fail, HeroAbilityChecks.Stats("a", "t", ratio: float.NaN, want: 1.2f).Result);
        Assert.AreEqual(CheckResult.Fail, HeroAbilityChecks.Stats("a", "t", ratio: 1.2f, want: float.NaN).Result);
        Assert.AreEqual(CheckResult.Fail, HeroAbilityChecks.Stats("a", "t", ratio: 0f, want: 0f).Result);
    }

    [TestMethod]
    public void NoStatsSample_Skip()
    {
        var v = HeroAbilityChecks.NoStatsSample(grants: 4);

        Assert.AreEqual(CheckResult.Skip, v.Result);
        Assert.AreEqual("reason=no-swing-grant grants=4", v.Detail);
    }

    [TestMethod]
    public void SwingRatio_BuffOverNothing_IsTheBuffFactor()
    {
        var want = HeroAbilityChecks.SwingRatio(null, new RaceAbilityEffects { SwingSpeedPercent = 20f });

        Assert.AreEqual(1.2f, want, 1e-5f);
        Assert.IsTrue(HeroAbilityChecks.IsStatsSample(want));
    }

    [TestMethod]
    public void SwingRatio_WeakerBuffUnderStronger_IsOneAndNoSample()
    {
        var before = new RaceAbilityEffects { SwingSpeedPercent = 25f };
        var want = HeroAbilityChecks.SwingRatio(before, new RaceAbilityEffects { SwingSpeedPercent = 25f });

        Assert.AreEqual(1f, want, 1e-6f);
        Assert.IsFalse(HeroAbilityChecks.IsStatsSample(want));
        Assert.IsFalse(HeroAbilityChecks.IsStatsSample(float.NaN));
    }

    [TestMethod]
    public void EveryDetail_IsNameValueWords()
    {
        var details = new[]
        {
            HeroAbilityChecks.Gate(true, true, true, 1).Detail,
            HeroAbilityChecks.Gate(true, true, false, 1).Detail,
            HeroAbilityChecks.Fired("x", 0, 1, 2, 3, true).Detail,
            HeroAbilityChecks.Stats("a", "t", 1f, 1f).Detail,
            HeroAbilityChecks.NoStatsSample(1).Detail,
        };

        foreach (var detail in details)
            Assert.IsTrue(detail.Split(' ').All(w => w.IndexOf('=') > 0), detail);
    }
}
