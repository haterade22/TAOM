using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.AlignmentDesertion;
using TAOM.Features.Execution;

namespace TAOM.Tests.Features.AlignmentDesertion;

[TestClass]
public class AlignmentDesertionServiceTests
{
    private const string EvilKingdom = "empire_s";
    private const string FreeKingdom = "empire_w";
    private const string NeutralKingdom = "umbar";

    private IAlignmentService _alignment;
    private IAlignmentDesertionSettingsProvider _settings;
    private AlignmentDesertionService _service;

    [TestInitialize]
    public void Setup()
    {
        _alignment = Substitute.For<IAlignmentService>();
        _settings = Substitute.For<IAlignmentDesertionSettingsProvider>();

        // Default: feature fully on, all toggles on, 50% rate.
        _settings.IsEnabled.Returns(true);
        _settings.Rate.Returns(0.5f);
        _settings.ApplyToAi.Returns(true);
        _settings.ApplyToPlayer.Returns(true);
        _settings.ApplyToParties.Returns(true);
        _settings.ApplyToGarrisons.Returns(true);

        // Side table mirrors alignment.json: empire_s=Evil, empire_w=Free, umbar=Neutral;
        // mordor=Evil, gondor=Free, umbar culture=Neutral.
        _alignment.GetKingdomSide(EvilKingdom).Returns(FactionSide.Evil);
        _alignment.GetKingdomSide(FreeKingdom).Returns(FactionSide.Free);
        _alignment.GetKingdomSide(NeutralKingdom).Returns(FactionSide.Neutral);
        _alignment.GetCultureSide("mordor").Returns(FactionSide.Evil);
        _alignment.GetCultureSide("gondor").Returns(FactionSide.Free);
        _alignment.GetCultureSide("umbar").Returns(FactionSide.Neutral);

        _service = new AlignmentDesertionService(_alignment, _settings);
    }

    private static List<DesertionTroopInfo> Troops(params DesertionTroopInfo[] t) => new(t);

    // ── Master toggle / empty ──

    [TestMethod]
    public void CalculateDesertion_Disabled_ReturnsEmpty()
    {
        _settings.IsEnabled.Returns(false);
        var troops = Troops(new DesertionTroopInfo("gondor_knight", "gondor", false, 20));

        var result = _service.CalculateDesertion(EvilKingdom, false, false, troops);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateDesertion_NoTroops_ReturnsEmpty()
    {
        var result = _service.CalculateDesertion(EvilKingdom, false, false, new List<DesertionTroopInfo>());

        Assert.AreEqual(0, result.Count);
    }

    // ── Core alignment logic ──

    [TestMethod]
    public void CalculateDesertion_EvilOwner_FreeTroops_Deserts50Percent()
    {
        var troops = Troops(new DesertionTroopInfo("gondor_knight", "gondor", false, 20));

        var result = _service.CalculateDesertion(EvilKingdom, false, false, troops);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("gondor_knight", result[0].TroopId);
        Assert.AreEqual(10, result[0].DesertCount); // 50% of 20
    }

    [TestMethod]
    public void CalculateDesertion_FreeOwner_EvilTroops_Deserts50Percent()
    {
        var troops = Troops(new DesertionTroopInfo("mordor_orc", "mordor", false, 10));

        var result = _service.CalculateDesertion(FreeKingdom, false, false, troops);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(5, result[0].DesertCount); // 50% of 10
    }

    [TestMethod]
    public void CalculateDesertion_SameSide_ReturnsEmpty()
    {
        // Free owner holding Free troops — loyal, no desertion.
        var troops = Troops(new DesertionTroopInfo("gondor_knight", "gondor", false, 20));

        var result = _service.CalculateDesertion(FreeKingdom, false, false, troops);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateDesertion_NeutralOwner_ReturnsEmpty()
    {
        var troops = Troops(new DesertionTroopInfo("gondor_knight", "gondor", false, 20));

        var result = _service.CalculateDesertion(NeutralKingdom, false, false, troops);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateDesertion_KingdomlessOwner_ReturnsEmpty()
    {
        // Independent clan (null/empty kingdom) resolves Neutral → no desertion.
        _alignment.GetKingdomSide("").Returns(FactionSide.Neutral);
        var troops = Troops(new DesertionTroopInfo("gondor_knight", "gondor", false, 20));

        var result = _service.CalculateDesertion("", false, false, troops);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateDesertion_NeutralTroop_ReturnsEmpty()
    {
        // Umbar (mercenary) troops serve anyone.
        var troops = Troops(new DesertionTroopInfo("umbar_corsair", "umbar", false, 20));

        var result = _service.CalculateDesertion(EvilKingdom, false, false, troops);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateDesertion_HeroTroop_Skipped()
    {
        var troops = Troops(new DesertionTroopInfo("gondor_companion", "gondor", true, 1));

        var result = _service.CalculateDesertion(EvilKingdom, false, false, troops);

        Assert.AreEqual(0, result.Count);
    }

    // ── Owner gate (player / AI) ──

    [TestMethod]
    public void CalculateDesertion_PlayerOwner_ApplyToPlayerOff_ReturnsEmpty()
    {
        _settings.ApplyToPlayer.Returns(false);
        var troops = Troops(new DesertionTroopInfo("gondor_knight", "gondor", false, 20));

        var result = _service.CalculateDesertion(EvilKingdom, isPlayerOwned: true, false, troops);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateDesertion_PlayerOwner_ApplyToPlayerOn_Deserts()
    {
        var troops = Troops(new DesertionTroopInfo("gondor_knight", "gondor", false, 20));

        var result = _service.CalculateDesertion(EvilKingdom, isPlayerOwned: true, false, troops);

        Assert.AreEqual(10, result[0].DesertCount);
    }

    [TestMethod]
    public void CalculateDesertion_AiOwner_ApplyToAiOff_ReturnsEmpty()
    {
        _settings.ApplyToAi.Returns(false);
        var troops = Troops(new DesertionTroopInfo("gondor_knight", "gondor", false, 20));

        var result = _service.CalculateDesertion(EvilKingdom, isPlayerOwned: false, false, troops);

        Assert.AreEqual(0, result.Count);
    }

    // ── Location gate (parties / garrisons) ──

    [TestMethod]
    public void CalculateDesertion_Party_ApplyToPartiesOff_ReturnsEmpty()
    {
        _settings.ApplyToParties.Returns(false);
        var troops = Troops(new DesertionTroopInfo("gondor_knight", "gondor", false, 20));

        var result = _service.CalculateDesertion(EvilKingdom, false, isGarrison: false, troops);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateDesertion_Garrison_ApplyToGarrisonsOff_ReturnsEmpty()
    {
        _settings.ApplyToGarrisons.Returns(false);
        var troops = Troops(new DesertionTroopInfo("gondor_knight", "gondor", false, 20));

        var result = _service.CalculateDesertion(EvilKingdom, false, isGarrison: true, troops);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateDesertion_Garrison_ApplyToGarrisonsOn_Deserts()
    {
        var troops = Troops(new DesertionTroopInfo("gondor_knight", "gondor", false, 20));

        var result = _service.CalculateDesertion(EvilKingdom, false, isGarrison: true, troops);

        Assert.AreEqual(10, result[0].DesertCount);
    }

    [TestMethod]
    public void CalculateDesertion_Garrison_ApplyToPartiesOffStillDeserts()
    {
        // The parties toggle must not gate garrisons.
        _settings.ApplyToParties.Returns(false);
        var troops = Troops(new DesertionTroopInfo("gondor_knight", "gondor", false, 20));

        var result = _service.CalculateDesertion(EvilKingdom, false, isGarrison: true, troops);

        Assert.AreEqual(10, result[0].DesertCount);
    }

    // ── Count math ──

    [TestMethod]
    public void CalculateDesertion_RateRoundsBelowOne_MinimumOne()
    {
        // 50% of 1 = 0.5 → (int)0 → floored to 1.
        var troops = Troops(new DesertionTroopInfo("gondor_knight", "gondor", false, 1));

        var result = _service.CalculateDesertion(EvilKingdom, false, false, troops);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(1, result[0].DesertCount);
    }

    [TestMethod]
    public void CalculateDesertion_FullRate_CappedAtCount()
    {
        _settings.Rate.Returns(1.0f);
        var troops = Troops(new DesertionTroopInfo("gondor_knight", "gondor", false, 7));

        var result = _service.CalculateDesertion(EvilKingdom, false, false, troops);

        Assert.AreEqual(7, result[0].DesertCount); // never exceeds the stack
    }

    [TestMethod]
    public void CalculateDesertion_RateZero_ReturnsEmpty()
    {
        // Rate 0 = no desertion; the min-1 floor must NOT manufacture a removal at 0%. (Codex #3.)
        _settings.Rate.Returns(0f);
        var troops = Troops(new DesertionTroopInfo("gondor_knight", "gondor", false, 20));

        var result = _service.CalculateDesertion(EvilKingdom, false, false, troops);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateDesertion_MixedRoster_OnlyOppositeDeserts()
    {
        var troops = Troops(
            new DesertionTroopInfo("mordor_orc", "mordor", false, 10),     // same side as Evil owner → skip
            new DesertionTroopInfo("gondor_knight", "gondor", false, 8),   // opposite → desert 4
            new DesertionTroopInfo("umbar_corsair", "umbar", false, 6),    // neutral → skip
            new DesertionTroopInfo("gondor_captain", "gondor", true, 1));  // hero → skip

        var result = _service.CalculateDesertion(EvilKingdom, false, false, troops);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("gondor_knight", result[0].TroopId);
        Assert.AreEqual(4, result[0].DesertCount);
    }

    [TestMethod]
    public void CalculateDesertion_ZeroCountTroop_Skipped()
    {
        var troops = Troops(new DesertionTroopInfo("gondor_knight", "gondor", false, 0));

        var result = _service.CalculateDesertion(EvilKingdom, false, false, troops);

        Assert.AreEqual(0, result.Count);
    }
}
