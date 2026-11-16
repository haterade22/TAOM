using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CombatMechanics;
using TAOM.Features.CombatMechanics.Domain;

namespace TAOM.Tests.Features.CombatMechanics;

[TestClass]
public class CleavingBlowServiceTests
{
    private ICombatMechanicsConfigProvider _configProvider = null!;
    private ICombatMechanicsSettingsProvider _settings = null!;
    private CombatMechanicsConfig _config = null!;
    private CleavingBlowService _sut = null!;

    [TestInitialize]
    public void SetUp()
    {
        _configProvider = Substitute.For<ICombatMechanicsConfigProvider>();
        _settings = Substitute.For<ICombatMechanicsSettingsProvider>();
        _config = new CombatMechanicsConfig();
        _configProvider.GetConfig().Returns(_config);
        _settings.CleavingBlowsEnabled.Returns(true);
        _settings.CleavingBlowsAiEnabled.Returns(false);
        _settings.CleavingBlowsMaxTargets.Returns(3);
        _sut = CreateSut();
    }

    // The config is cached at construction; a test that changes _config builds a fresh SUT.
    private CleavingBlowService CreateSut() => new CleavingBlowService(_configProvider, _settings);

    // Defaults: a player on foot with a two-handed axe, a swing that hit a collider for 30 after armour took 10, not a kill,
    // equal skills.
    private static CleavingBlowContext Context(
        object? key = null,
        CleaveAttackerKind kind = CleaveAttackerKind.Player,
        bool isHuman = true,
        bool isMounted = false,
        bool isSwing = true,
        bool isAlternative = false,
        bool isPassive = false,
        bool isHorseCharge = false,
        bool isCollider = true,
        bool blockedByShield = false,
        bool anotherBone = false,
        bool shruggedOff = false,
        bool isFatal = false,
        int inflicted = 30,
        float absorbed = 10f,
        string? weaponClass = "TwoHandedAxe",
        int attackerSkill = 100,
        int victimSkill = 100)
        => new CleavingBlowContext(key ?? new object(), kind, isHuman, isMounted, isSwing, isAlternative, isPassive, isHorseCharge,
            isCollider, blockedByShield, anotherBone, shruggedOff, isFatal, inflicted, absorbed, weaponClass, attackerSkill, victimSkill);

    // ---- the carry ----

    [TestMethod]
    public void PlayerTwoHandedAxe_CarriesTheClassFactorLessTheArmourShare()
    {
        var result = _sut.CalculateMomentum(Context(), 1f);

        Assert.AreEqual(0.6f * (1f - 10f / 40f), result!.Value, 0.0001f);
        Assert.AreEqual(0.45f, result.Value, 0.0001f);
    }

    [TestMethod]
    public void AKill_AddsTheKillBonus()
    {
        var result = _sut.CalculateMomentum(Context(isFatal: true), 1f);

        Assert.AreEqual(0.6f, result!.Value, 0.0001f);
    }

    [TestMethod]
    public void ASkillLeadOfTheTargetDelta_AddsTheFullSkillBonus()
    {
        var result = _sut.CalculateMomentum(Context(attackerSkill: 235, victimSkill: 100), 1f);

        Assert.AreEqual(0.55f, result!.Value, 0.0001f);
    }

    [TestMethod]
    public void ASkillLeadInsideTheDeadZone_AddsNothing()
    {
        var result = _sut.CalculateMomentum(Context(attackerSkill: 120, victimSkill: 100), 1f);

        Assert.AreEqual(0.45f, result!.Value, 0.0001f);
    }

    [TestMethod]
    public void ASumAboveTheCap_ReturnsTheCapTimesTheOriginalMomentum()
    {
        // 0.6 class factor + 0.15 kill + 0.10 skill = 0.85, capped at 0.75.
        var result = _sut.CalculateMomentum(Context(isFatal: true, absorbed: 0f, attackerSkill: 235), 0.8f);

        Assert.AreEqual(0.75f * 0.8f, result!.Value, 0.0001f);
    }

    [TestMethod]
    public void AResultBelowTheFloor_ReturnsZeroNotNull()
    {
        // 0.45 x 0.5 = 0.225, below vanilla's 0.25 floor: the rule owns the hit and ends the swing.
        var result = _sut.CalculateMomentum(Context(), 0.5f);

        Assert.IsNotNull(result);
        Assert.AreEqual(0f, result!.Value);
    }

    [TestMethod]
    public void ASwordCarriesWhereVanillaNeverDid()
    {
        var result = _sut.CalculateMomentum(Context(weaponClass: "TwoHandedSword", absorbed: 0f), 1f);

        Assert.AreEqual(0.5f, result!.Value, 0.0001f);
    }

    [TestMethod]
    public void APolearmSwingCarriesItsOwnFactor()
    {
        var result = _sut.CalculateMomentum(Context(weaponClass: "TwoHandedPolearm", absorbed: 0f), 1f);

        Assert.AreEqual(0.4f, result!.Value, 0.0001f);
    }

    // ---- gates: each answers null so vanilla decides ----

    [DataTestMethod]
    [DataRow("featureOff")]
    [DataRow("kindNone")]
    [DataRow("aiWithAiOff")]
    [DataRow("aiOneHandedAxe")]
    [DataRow("aiPolearm")]
    [DataRow("notHuman")]
    [DataRow("mounted")]
    [DataRow("thrust")]
    [DataRow("alternative")]
    [DataRow("passive")]
    [DataRow("horseCharge")]
    [DataRow("notCollider")]
    [DataRow("shield")]
    [DataRow("anotherBone")]
    [DataRow("shruggedOff")]
    [DataRow("noDamage")]
    [DataRow("absorbedNaN")]
    [DataRow("absorbedNegative")]
    [DataRow("absorbedInfinity")]
    [DataRow("classNotListed")]
    [DataRow("classNull")]
    [DataRow("noIdentity")]
    public void EachGate_AnswersNull_AndShouldSliceThroughAnswersFalse(string gate)
    {
        _settings.CleavingBlowsAiEnabled.Returns(gate.StartsWith("ai") ? gate != "aiWithAiOff" : false);
        if (gate == "featureOff")
            _settings.CleavingBlowsEnabled.Returns(false);
        var context = GateContext(gate);

        Assert.IsNull(_sut.CalculateMomentum(in context, 1f), gate);
        Assert.IsFalse(_sut.ShouldSliceThrough(in context, 0.4f), gate);
    }

    private static CleavingBlowContext GateContext(string gate) => gate switch
    {
        "kindNone" => Context(kind: CleaveAttackerKind.None),
        "aiWithAiOff" => Context(kind: CleaveAttackerKind.Ai),
        "aiOneHandedAxe" => Context(kind: CleaveAttackerKind.Ai, weaponClass: "OneHandedAxe"),
        "aiPolearm" => Context(kind: CleaveAttackerKind.Ai, weaponClass: "TwoHandedPolearm"),
        "notHuman" => Context(isHuman: false),
        "mounted" => Context(isMounted: true),
        "thrust" => Context(isSwing: false),
        "alternative" => Context(isAlternative: true),
        "passive" => Context(isPassive: true),
        "horseCharge" => Context(isHorseCharge: true),
        "notCollider" => Context(isCollider: false),
        "shield" => Context(blockedByShield: true),
        "anotherBone" => Context(anotherBone: true),
        "shruggedOff" => Context(shruggedOff: true),
        "noDamage" => Context(inflicted: 0),
        "absorbedNaN" => Context(absorbed: float.NaN),
        "absorbedNegative" => Context(absorbed: -1f),
        "absorbedInfinity" => Context(absorbed: float.PositiveInfinity),
        "classNotListed" => Context(weaponClass: "OneHandedSword"),
        "classNull" => Context(weaponClass: null),
        "noIdentity" => new CleavingBlowContext(null, CleaveAttackerKind.Player, true, false, true, false, false, false, true, false, false, false, false, 30, 10f, "TwoHandedAxe", 100, 100),
        _ => Context(),
    };

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(0f)]
    [DataRow(-1f)]
    [DataRow(float.PositiveInfinity)]
    public void ABadOriginalMomentum_AnswersNull(float original)
    {
        Assert.IsNull(_sut.CalculateMomentum(Context(), original));
    }

    [TestMethod]
    public void AMountedAttacker_CarriesWhenTheConfigIncludesThem()
    {
        _config.CleavingBlows.IncludeMountedAttackers = true;
        var sut = CreateSut();

        var result = sut.CalculateMomentum(Context(isMounted: true), 1f);

        Assert.AreEqual(0.45f, result!.Value, 0.0001f);
    }

    // ---- the AI switch ----

    [TestMethod]
    public void AnAiTwoHandedAxeHit_IsVanillaWithTheAiSwitchOff_AndCarriesWithItOn()
    {
        var context = Context(kind: CleaveAttackerKind.Ai);

        Assert.IsNull(_sut.CalculateMomentum(in context, 1f));

        _settings.CleavingBlowsAiEnabled.Returns(true);
        var result = _sut.CalculateMomentum(in context, 1f);

        Assert.AreEqual(0.45f, result!.Value, 0.0001f);
    }

    [TestMethod]
    public void WithTheAiSwitchOn_AnAiOneHanderOrPolearmStillGetsVanilla()
    {
        _settings.CleavingBlowsAiEnabled.Returns(true);

        Assert.IsNull(_sut.CalculateMomentum(Context(kind: CleaveAttackerKind.Ai, weaponClass: "OneHandedAxe"), 1f));
        Assert.IsNull(_sut.CalculateMomentum(Context(kind: CleaveAttackerKind.Ai, weaponClass: "TwoHandedPolearm"), 1f));
    }

    [TestMethod]
    public void WithTheAiSwitchOn_AnAiTwoHandedSwordAndMaceCarry()
    {
        _settings.CleavingBlowsAiEnabled.Returns(true);

        Assert.IsNotNull(_sut.CalculateMomentum(Context(kind: CleaveAttackerKind.Ai, weaponClass: "TwoHandedSword", absorbed: 0f), 1f));
        Assert.IsNotNull(_sut.CalculateMomentum(Context(kind: CleaveAttackerKind.Ai, weaponClass: "TwoHandedMace", absorbed: 0f), 1f));
    }

    [TestMethod]
    public void ThePlayersValue_DoesNotDependOnTheAiSwitch()
    {
        var off = _sut.CalculateMomentum(Context(), 1f);
        _settings.CleavingBlowsAiEnabled.Returns(true);
        var on = _sut.CalculateMomentum(Context(), 1f);

        Assert.AreEqual(off!.Value, on!.Value);
    }

    // ---- the per-swing cap ----

    // A kill with no armour carries 0.75, so the floor (0.25) never ends the swing before the cap does.
    private float? Hit(object key, float original) => _sut.CalculateMomentum(Context(key: key, isFatal: true, absorbed: 0f), original);

    [TestMethod]
    public void MaxThree_CarriesTwiceThenStops()
    {
        var key = new object();

        var first = Hit(key, 1f);
        var second = Hit(key, first!.Value);
        var third = Hit(key, second!.Value);

        Assert.AreEqual(0.75f, first.Value);
        Assert.AreEqual(0.5625f, second.Value);
        Assert.AreEqual(0f, third!.Value);
    }

    [TestMethod]
    public void MaxTwo_CarriesOnceThenStops()
    {
        _settings.CleavingBlowsMaxTargets.Returns(2);
        var key = new object();

        var first = Hit(key, 1f);
        var second = Hit(key, first!.Value);

        Assert.AreEqual(0.75f, first.Value);
        Assert.AreEqual(0f, second!.Value);
    }

    [TestMethod]
    public void AnOriginalOfOneAfterAChain_RestartsAtHitOne()
    {
        var key = new object();
        var first = Hit(key, 1f);
        var second = Hit(key, first!.Value);
        Hit(key, second!.Value);

        var fresh = Hit(key, 1f);
        var freshSecond = Hit(key, fresh!.Value);

        Assert.AreEqual(0.75f, fresh.Value);
        Assert.AreEqual(0.5625f, freshSecond!.Value);
    }

    [TestMethod]
    public void TwoAttackers_CountApart()
    {
        var a = new object();
        var b = new object();

        var a1 = Hit(a, 1f);
        var b1 = Hit(b, 1f);
        var a2 = Hit(a, a1!.Value);
        var b2 = Hit(b, b1!.Value);
        var a3 = Hit(a, a2!.Value);

        Assert.AreEqual(0.5625f, a2.Value);
        Assert.AreEqual(0.5625f, b2!.Value);
        Assert.AreEqual(0f, a3!.Value);
    }

    [TestMethod]
    public void ADeclinedCallBetweenTwoHits_LeavesTheChainAsItWas()
    {
        var key = new object();
        var first = Hit(key, 1f);

        var declined = _sut.CalculateMomentum(Context(key: key, isSwing: false), first!.Value);
        var second = Hit(key, first.Value);
        var third = Hit(key, second!.Value);

        Assert.IsNull(declined);
        Assert.AreEqual(0.5625f, second.Value);
        Assert.AreEqual(0f, third!.Value);
    }

    [TestMethod]
    public void MaxTargets_IsReadLive_BetweenSwings()
    {
        var key = new object();
        var first = Hit(key, 1f);
        var second = Hit(key, first!.Value);
        var third = Hit(key, second!.Value);
        Assert.AreEqual(0f, third!.Value);

        _settings.CleavingBlowsMaxTargets.Returns(2);
        var swing2First = Hit(key, 1f);
        var swing2Second = Hit(key, swing2First!.Value);

        Assert.AreEqual(0.75f, swing2First.Value);
        Assert.AreEqual(0f, swing2Second!.Value);
    }

    // ---- never below vanilla ----

    // Vanilla CalculateDefaultRemainingMomentum for a two-handed axe or mace: orig x (1 - absorbed / inflicted) x 0.5, 0 below 0.25.
    private static float VanillaCarry(float original, float absorbed, float inflicted)
    {
        float result = original * (1f - absorbed / inflicted) * 0.5f;
        return result >= 0.25f ? result : 0f;
    }

    [DataTestMethod]
    [DataRow("TwoHandedAxe")]
    [DataRow("TwoHandedMace")]
    public void ForAPlayersAxeOrMace_TheResultIsNeverBelowVanilla(string weaponClass)
    {
        for (int absorbed = 0; absorbed <= 60; absorbed += 5)
        {
            for (int inflicted = 1; inflicted <= 80; inflicted += 7)
            {
                var key = new object();
                float vanilla1 = VanillaCarry(1f, absorbed, inflicted);
                float vanilla2 = VanillaCarry(vanilla1, absorbed, inflicted);

                var first = _sut.CalculateMomentum(Context(key: key, weaponClass: weaponClass, absorbed: absorbed, inflicted: inflicted), 1f)!.Value;
                Assert.IsTrue(first >= vanilla1, $"{weaponClass} a={absorbed} i={inflicted} hit 1: {first} < {vanilla1}");
                if (first <= 0f)
                    continue;

                var second = _sut.CalculateMomentum(Context(key: key, weaponClass: weaponClass, absorbed: absorbed, inflicted: inflicted), first)!.Value;
                Assert.IsTrue(second >= vanilla2, $"{weaponClass} a={absorbed} i={inflicted} hit 2: {second} < {vanilla2}");
            }
        }
    }

    // ---- the reaction ----

    [TestMethod]
    public void ShouldSliceThrough_QualifyingHitWithMomentumLeft_IsTrue()
    {
        Assert.IsTrue(_sut.ShouldSliceThrough(Context(), 0.4f));
    }

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(-0.1f)]
    [DataRow(float.NaN)]
    public void ShouldSliceThrough_NoMomentumLeft_IsFalse(float momentum)
    {
        Assert.IsFalse(_sut.ShouldSliceThrough(Context(), momentum));
    }

    [TestMethod]
    public void ShouldSliceThrough_BetweenTwoMomentumCalls_DoesNotMoveTheChain()
    {
        var key = new object();
        var first = Hit(key, 1f);

        Assert.IsTrue(_sut.ShouldSliceThrough(Context(key: key), first!.Value));
        var second = Hit(key, first.Value);
        Assert.IsTrue(_sut.ShouldSliceThrough(Context(key: key), second!.Value));
        var third = Hit(key, second.Value);

        Assert.AreEqual(0.5625f, second.Value);
        Assert.AreEqual(0f, third!.Value);
    }
}
