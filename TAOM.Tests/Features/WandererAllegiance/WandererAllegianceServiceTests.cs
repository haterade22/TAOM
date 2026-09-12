using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Execution;
using TAOM.Features.NamedCompanions;
using TAOM.Features.NamedCompanions.Domain;
using TAOM.Features.WandererAllegiance;

namespace TAOM.Tests.Features.WandererAllegiance;

/// <summary>
/// The whole hire rule, pure. The dialogue behavior only converts four engine ids into these
/// arguments, so every row of the policy is pinned here and the behavior itself is verified in game.
/// </summary>
[TestClass]
public class WandererAllegianceServiceTests
{
    private const string Aragorn = "named_companion_aragorn";
    private const string GenericWanderer = "spc_wanderer_mordor_3";
    private const string WandererCulture = "gondor";
    private const string PlayerKingdom = "empire_s";
    private const string PlayerCulture = "mordor";

    private IAlignmentService _alignment = null!;
    private IWandererAllegianceSettingsProvider _settings = null!;
    private INamedCompanionConfigProvider _named = null!;
    private WandererAllegianceService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _alignment = Substitute.For<IAlignmentService>();
        _settings = Substitute.For<IWandererAllegianceSettingsProvider>();
        _named = Substitute.For<INamedCompanionConfigProvider>();

        _settings.IsEnabled.Returns(true);
        _settings.Scope.Returns(WandererAllegianceScope.AllWanderers);
        _named.GetCompanions().Returns(new List<NamedCompanionDefinition>
        {
            new() { CharacterId = Aragorn, Enabled = true },
            new() { CharacterId = "named_companion_igor", Enabled = false },
        });

        _sut = new WandererAllegianceService(_alignment, _settings, _named);
    }

    // NSubstitute returns default(FactionSide) == Free for unconfigured calls, so both sides are
    // always configured explicitly.
    private void SetSides(FactionSide wanderer, FactionSide player)
    {
        _alignment.GetCultureSide(WandererCulture).Returns(wanderer);
        _alignment.ResolveSide(PlayerKingdom, PlayerCulture).Returns(player);
    }

    private WandererHireVerdict Evaluate(string? heroId = GenericWanderer) =>
        _sut.Evaluate(heroId, WandererCulture, PlayerKingdom, PlayerCulture);

    // ---- the side matrix -----------------------------------------------------------------

    [TestMethod]
    public void Evaluate_FreeWandererEvilPlayer_RefusedByFreeWanderer()
    {
        SetSides(FactionSide.Free, FactionSide.Evil);

        Assert.AreEqual(WandererHireVerdict.RefusedByFreeWanderer, Evaluate());
    }

    [TestMethod]
    public void Evaluate_EvilWandererFreePlayer_RefusedByEvilWanderer()
    {
        SetSides(FactionSide.Evil, FactionSide.Free);

        Assert.AreEqual(WandererHireVerdict.RefusedByEvilWanderer, Evaluate());
    }

    [DataTestMethod]
    [DataRow(FactionSide.Free, FactionSide.Free)]
    [DataRow(FactionSide.Evil, FactionSide.Evil)]
    public void Evaluate_SameSide_Allowed(FactionSide wanderer, FactionSide player)
    {
        SetSides(wanderer, player);

        Assert.AreEqual(WandererHireVerdict.Allowed, Evaluate());
    }

    [DataTestMethod]
    [DataRow(FactionSide.Free)]
    [DataRow(FactionSide.Evil)]
    [DataRow(FactionSide.Neutral)]
    public void Evaluate_NeutralWanderer_Allowed(FactionSide player)
    {
        // Umbar, Dunland, Shaghana and Abanissa wanderers serve anyone.
        SetSides(FactionSide.Neutral, player);

        Assert.AreEqual(WandererHireVerdict.Allowed, Evaluate());
    }

    [DataTestMethod]
    [DataRow(FactionSide.Free)]
    [DataRow(FactionSide.Evil)]
    public void Evaluate_NeutralPlayer_Allowed(FactionSide wanderer)
    {
        // A kingdomless Umbar player is refused by nobody.
        SetSides(wanderer, FactionSide.Neutral);

        Assert.AreEqual(WandererHireVerdict.Allowed, Evaluate());
    }

    [TestMethod]
    public void Evaluate_AllNineSideCombinations_RefusesExactlyTwo()
    {
        // The invariant behind the rows above, stated once over the whole input space.
        var sides = new[] { FactionSide.Free, FactionSide.Evil, FactionSide.Neutral };
        var refusals = 0;

        foreach (var wanderer in sides)
        foreach (var player in sides)
        {
            SetSides(wanderer, player);
            var verdict = Evaluate();

            var expectRefusal = wanderer != FactionSide.Neutral && player != FactionSide.Neutral && wanderer != player;
            Assert.AreEqual(expectRefusal, verdict != WandererHireVerdict.Allowed, $"wanderer={wanderer} player={player}");

            if (verdict != WandererHireVerdict.Allowed)
                refusals++;
        }

        Assert.AreEqual(2, refusals, "only Free/Evil and Evil/Free may refuse");
    }

    // ---- inputs ---------------------------------------------------------------------------

    [TestMethod]
    public void Evaluate_PlayerSide_UsesResolveSideKingdomFirst()
    {
        // ResolveSide is kingdom-first with a culture fallback, which is what makes a Gondor-born
        // vassal or mercenary of Mordor read Evil. The service must not re-derive that from the
        // culture alone.
        SetSides(FactionSide.Free, FactionSide.Evil);

        Evaluate();

        _alignment.Received(1).ResolveSide(PlayerKingdom, PlayerCulture);
        _alignment.DidNotReceive().GetCultureSide(PlayerCulture);
    }

    [TestMethod]
    public void Evaluate_NeverCallsAreEnemyAlignments()
    {
        // AreEnemyAlignments treats Neutral as everyone's enemy, the opposite of what a pairing
        // question needs (MarriageAlignmentService records the same refusal).
        SetSides(FactionSide.Free, FactionSide.Evil);

        Evaluate();

        _alignment.DidNotReceiveWithAnyArgs().AreEnemyAlignments(default(FactionSide), default(FactionSide));
        _alignment.DidNotReceiveWithAnyArgs().AreEnemyAlignments(default(string), default(string));
    }

    [TestMethod]
    public void Evaluate_NullWandererCulture_Allowed()
    {
        // A hero with no culture is nobody's enemy: fail open to vanilla rather than guess a side.
        _alignment.ResolveSide(PlayerKingdom, PlayerCulture).Returns(FactionSide.Evil);

        var verdict = _sut.Evaluate(GenericWanderer, null, PlayerKingdom, PlayerCulture);

        Assert.AreEqual(WandererHireVerdict.Allowed, verdict);
    }

    [TestMethod]
    public void Evaluate_Disabled_Allowed()
    {
        _settings.IsEnabled.Returns(false);
        SetSides(FactionSide.Free, FactionSide.Evil);

        Assert.AreEqual(WandererHireVerdict.Allowed, Evaluate());
    }

    // ---- named companions only --------------------------------------------------------------

    [TestMethod]
    public void Evaluate_NamedOnly_UnlistedWanderer_Allowed()
    {
        _settings.Scope.Returns(WandererAllegianceScope.NamedCompanionsOnly);
        SetSides(FactionSide.Free, FactionSide.Evil);

        Assert.AreEqual(WandererHireVerdict.Allowed, Evaluate(GenericWanderer));
    }

    [TestMethod]
    public void Evaluate_NamedOnly_ListedCompanion_Refused()
    {
        _settings.Scope.Returns(WandererAllegianceScope.NamedCompanionsOnly);
        SetSides(FactionSide.Free, FactionSide.Evil);

        Assert.AreEqual(WandererHireVerdict.RefusedByFreeWanderer, Evaluate(Aragorn));
    }

    [TestMethod]
    public void Evaluate_NamedOnly_DisabledEntryStillCounts()
    {
        // A disabled named companion never spawns; if one is somehow in a tavern anyway, "disabled"
        // must not mean "unprotected".
        _settings.Scope.Returns(WandererAllegianceScope.NamedCompanionsOnly);
        SetSides(FactionSide.Free, FactionSide.Evil);

        Assert.AreEqual(WandererHireVerdict.RefusedByFreeWanderer, Evaluate("named_companion_igor"));
    }

    [TestMethod]
    public void Evaluate_NamedOnly_IdMatchIsCaseInsensitive()
    {
        _settings.Scope.Returns(WandererAllegianceScope.NamedCompanionsOnly);
        SetSides(FactionSide.Free, FactionSide.Evil);

        Assert.AreEqual(WandererHireVerdict.RefusedByFreeWanderer, Evaluate("NAMED_COMPANION_ARAGORN"));
    }

    [TestMethod]
    public void Evaluate_NamedOnly_NullHeroId_Allowed()
    {
        _settings.Scope.Returns(WandererAllegianceScope.NamedCompanionsOnly);
        SetSides(FactionSide.Free, FactionSide.Evil);

        Assert.AreEqual(WandererHireVerdict.Allowed, Evaluate(null));
    }

    [TestMethod]
    public void Evaluate_NamedOnly_ReadsTheCompanionListOnce()
    {
        // The list is process-lifetime config, read lazily and cached; a per-conversation re-read
        // would re-parse nothing but is the kind of allocation a dialogue condition should not do.
        _settings.Scope.Returns(WandererAllegianceScope.NamedCompanionsOnly);
        SetSides(FactionSide.Free, FactionSide.Evil);

        Evaluate(Aragorn);
        Evaluate(GenericWanderer);
        Evaluate(Aragorn);

        _named.Received(1).GetCompanions();
    }
}
