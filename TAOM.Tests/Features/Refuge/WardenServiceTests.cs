using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.Refuge;

namespace TAOM.Tests.Features.Refuge;

/// <summary>
/// Decision-path coverage for <see cref="WardenService"/>: candidate ordering and the
/// companion-slot gate, the promote-exactly-one-troop sequencing, the release matrix under
/// release (companion rejoins by action, promoted warden returns to the ranks, absent no-op), and
/// the never-attached promotion rollback. Campaign statics are
/// overridden on a test subclass; those virtual bodies are the honest untested boundary sliver.
/// </summary>
[TestClass]
public class WardenServiceTests
{
    private sealed class TestableWardenService : WardenService
    {
        public readonly List<WardenCandidate> Companions = new List<WardenCandidate>();
        public readonly List<WardenCandidate> Promotables = new List<WardenCandidate>();
        public bool SlotFree = true;
        public readonly Dictionary<string, int> TroopCounts = new Dictionary<string, int>();
        public string MintResult = "hero_minted";
        public int MintCalls;
        public readonly List<string> RemovedTroops = new List<string>();
        public bool WithRefuge = true;
        public readonly List<string> MovedToMainParty = new List<string>();

        public TestableWardenService(IModLogger logger)
            : base(logger)
        {
        }

        // Existing tests list eligible companions in Companions; the CompanionsInMainParty tests
        // arrange the raw roster heroes in PartyHeroes.
        public List<PartyHeroInfo> PartyHeroes;

        protected override IReadOnlyList<PartyHeroInfo> HeroesInMainParty() =>
            PartyHeroes ?? Companions.ConvertAll(c => c == null ? null : new PartyHeroInfo
            {
                HeroId = c.Id,
                DisplayName = c.DisplayName,
                IsPlayerClanCompanion = true,
            });

        protected override bool HasCompanionSlotFree() => SlotFree;

        protected override IReadOnlyList<WardenCandidate> PromotableTroopsInMainParty() => Promotables;

        protected override int TroopCountInMainParty(string troopId) =>
            TroopCounts.TryGetValue(troopId, out var count) ? count : 0;

        public PromotionSource Source = new PromotionSource
        {
            TroopCultureId = "culture_troop",
            PlayerCultureId = "culture_player",
        };
        public string CultureTemplate = "template_culture"; // returned for any non-null culture id
        public string AnyTemplate = "template_any";         // returned for a null culture id
        public int? ComesOfAge = 18;
        public int RandomIntResult = 7;
        public bool RenameThrows;
        public readonly List<string> MintSteps = new List<string>();

        protected override PromotionSource ReadPromotionSource(string troopId)
        {
            MintCalls++;
            MintSteps.Add("source:" + troopId);
            return Source;
        }

        protected override string RandomCompanionTemplateId(string cultureId)
        {
            MintSteps.Add("template:" + (cultureId ?? "<any>"));
            return cultureId == null ? AnyTemplate : CultureTemplate;
        }

        protected override int? HeroComesOfAge()
        {
            MintSteps.Add("comesOfAge");
            return ComesOfAge;
        }

        protected override int NextRandomInt(int maxExclusive)
        {
            MintSteps.Add("rng:" + maxExclusive);
            return RandomIntResult;
        }

        public MintedHero LastMinted;
        public readonly List<MintedHero> MintedHandedOn = new List<MintedHero>();

        protected override MintedHero CreatePromotedHero(string templateId, int age)
        {
            MintSteps.Add("create:" + templateId + ":" + age);
            LastMinted = MintResult == null ? null : new MintedHero { HeroId = MintResult };
            return LastMinted;
        }

        protected override void RenamePromotedHero(MintedHero minted, string troopId)
        {
            MintedHandedOn.Add(minted);
            MintSteps.Add("rename:" + minted?.HeroId + ":" + troopId);
            if (RenameThrows)
                throw new System.InvalidOperationException("text manager missing");
        }

        protected override void EnrolPromotedHero(MintedHero minted)
        {
            MintedHandedOn.Add(minted);
            MintSteps.Add("enrol:" + minted?.HeroId);
        }

        protected override bool RemoveOneTroopFromMainParty(string troopId)
        {
            RemovedTroops.Add(troopId);
            return true;
        }

        protected override bool IsHeroWithRefugeParty(string heroId) => WithRefuge;

        // One ordered stream for the move, the removal, the refund and the message, so the
        // return-to-ranks sequence can be pinned.
        public readonly List<string> Steps = new List<string>();

        protected override void MoveHeroToMainParty(string heroId)
        {
            MovedToMainParty.Add(heroId);
            Steps.Add("move:" + heroId);
        }

        public bool RemoveMintedResult = true;
        public readonly List<string> RemovedCompanions = new List<string>();
        public readonly List<string> RefundedTroops = new List<string>();

        protected override bool RemoveMintedCompanion(string heroId)
        {
            RemovedCompanions.Add(heroId);
            Steps.Add("remove:" + heroId);
            return RemoveMintedResult;
        }

        protected override void AddOneTroopToMainParty(string troopId)
        {
            RefundedTroops.Add(troopId);
            Steps.Add("troop:" + troopId);
        }

        // A plain promoted warden by default: still the player's companion, no family, no role.
        public PromotedWardenFacts Facts = new PromotedWardenFacts { IsPlayerClanCompanion = true };
        public readonly HashSet<string> KnownTroops = new HashSet<string> { "troop_a", "culture_basic" };
        public string CultureBasicTroop = "culture_basic";
        public readonly List<string> RanksMessages = new List<string>();

        protected override PromotedWardenFacts ReadPromotedWarden(string heroId) => Facts;

        protected override bool IsRegularTroop(string troopId) => troopId != null && KnownTroops.Contains(troopId);

        protected override string CultureBasicTroopId(string heroId) => CultureBasicTroop;

        protected override void ShowReturnedToRanks(string troopId)
        {
            RanksMessages.Add(troopId);
            Steps.Add("message:" + troopId);
        }
    }

    private TestableWardenService _sut;
    private IModLogger _logger;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _sut = new TestableWardenService(_logger);
    }

    private static WardenCandidate Companion(string id) =>
        new WardenCandidate { Id = id, DisplayName = id, IsCompanion = true };

    private static WardenCandidate Troop(string id) =>
        new WardenCandidate { Id = id, DisplayName = id, IsCompanion = false };

    // --- Candidates ordering + gating ---

    [TestMethod]
    public void Candidates_CompanionsListedBeforePromotableTroops()
    {
        _sut.Companions.Add(Companion("companion_1"));
        _sut.Promotables.Add(Troop("troop_1"));
        _sut.Companions.Add(Companion("companion_2"));

        var candidates = _sut.Candidates();

        Assert.AreEqual(3, candidates.Count);
        Assert.AreEqual("companion_1", candidates[0].Id);
        Assert.AreEqual("companion_2", candidates[1].Id);
        Assert.AreEqual("troop_1", candidates[2].Id);
        Assert.IsTrue(candidates[0].IsCompanion);
        Assert.IsFalse(candidates[2].IsCompanion);
    }

    [TestMethod]
    public void Candidates_NoCompanionSlotFree_ExcludesTroopsKeepsCompanions()
    {
        _sut.SlotFree = false;
        _sut.Companions.Add(Companion("companion_1"));
        _sut.Promotables.Add(Troop("troop_1"));

        var candidates = _sut.Candidates();

        Assert.AreEqual(1, candidates.Count);
        Assert.AreEqual("companion_1", candidates[0].Id);
    }

    [TestMethod]
    public void Candidates_NobodyAvailable_ReturnsEmpty()
    {
        Assert.AreEqual(0, _sut.Candidates().Count);
        Assert.IsFalse(_sut.AnyAvailable());
    }

    [TestMethod]
    public void AnyAvailable_TroopOnlyWithSlotFree_True()
    {
        _sut.Promotables.Add(Troop("troop_1"));

        Assert.IsTrue(_sut.AnyAvailable());
    }

    [TestMethod]
    public void AnyAvailable_TroopOnlyWithoutSlot_False()
    {
        _sut.SlotFree = false;
        _sut.Promotables.Add(Troop("troop_1"));

        Assert.IsFalse(_sut.AnyAvailable());
    }

    // --- ResolveWarden ---

    [TestMethod]
    public void ResolveWarden_Companion_ReturnsHisIdWithoutPromotion()
    {
        var heroId = _sut.ResolveWarden(Companion("companion_1"), out bool promoted, out string fromTroop);

        Assert.AreEqual("companion_1", heroId);
        Assert.IsFalse(promoted);
        Assert.IsNull(fromTroop);
        Assert.AreEqual(0, _sut.MintCalls);
        Assert.AreEqual(0, _sut.RemovedTroops.Count);
    }

    [TestMethod]
    public void ResolveWarden_Troop_MintsHeroAndConsumesExactlyOneSoldier()
    {
        _sut.TroopCounts["troop_1"] = 5;

        var heroId = _sut.ResolveWarden(Troop("troop_1"), out bool promoted, out string fromTroop);

        Assert.AreEqual("hero_minted", heroId);
        Assert.IsTrue(promoted);
        Assert.AreEqual("troop_1", fromTroop);
        Assert.AreEqual(1, _sut.MintCalls);
        CollectionAssert.AreEqual(new[] { "troop_1" }, _sut.RemovedTroops,
            "exactly one soldier leaves the ranks; he became the hero");
    }

    [TestMethod]
    public void ResolveWarden_TroopWithoutCompanionSlot_FailsWithoutMinting()
    {
        _sut.SlotFree = false;
        _sut.TroopCounts["troop_1"] = 5;

        var heroId = _sut.ResolveWarden(Troop("troop_1"), out bool promoted, out _);

        Assert.IsNull(heroId);
        Assert.IsFalse(promoted);
        Assert.AreEqual(0, _sut.MintCalls);
        Assert.AreEqual(0, _sut.RemovedTroops.Count);
    }

    [TestMethod]
    public void ResolveWarden_TroopStackEmptiedSincePicking_FailsWithoutMinting()
    {
        _sut.TroopCounts["troop_1"] = 0;

        Assert.IsNull(_sut.ResolveWarden(Troop("troop_1"), out _, out _));
        Assert.AreEqual(0, _sut.MintCalls);
    }

    [TestMethod]
    public void ResolveWarden_MintRefused_FailsWithoutConsumingTheSoldier()
    {
        _sut.TroopCounts["troop_1"] = 5;
        _sut.MintResult = null;

        var heroId = _sut.ResolveWarden(Troop("troop_1"), out bool promoted, out string fromTroop);

        Assert.IsNull(heroId);
        Assert.IsFalse(promoted);
        Assert.IsNull(fromTroop);
        Assert.AreEqual(0, _sut.RemovedTroops.Count, "no hero means the soldier stays a soldier");
    }

    [TestMethod]
    public void ResolveWarden_NullCandidate_ReturnsNull()
    {
        Assert.IsNull(_sut.ResolveWarden(null, out bool promoted, out string fromTroop));
        Assert.IsFalse(promoted);
        Assert.IsNull(fromTroop);
    }

    [TestMethod]
    public void ResolveWarden_EmptyCandidateId_ReturnsNull()
    {
        Assert.IsNull(_sut.ResolveWarden(new WardenCandidate { Id = "", IsCompanion = true }, out _, out _));
    }

    // --- ReleaseWarden (companion rejoins; promoted warden goes back to the ranks) ---

    [TestMethod]
    public void ReleaseWarden_CompanionWithRefuge_RejoinsMainPartyAndIsNeverRemoved()
    {
        _sut.WithRefuge = true;

        _sut.ReleaseWarden("companion_1", promoted: false, promotedFromTroopId: null);

        CollectionAssert.AreEqual(new[] { "companion_1" }, _sut.MovedToMainParty);
        Assert.AreEqual(0, _sut.RemovedCompanions.Count, "a warden who was already a companion stays one");
        Assert.AreEqual(0, _sut.RefundedTroops.Count);
    }

    [TestMethod]
    public void ReleaseWarden_CompanionWithStaleTroopId_IsNeverRemoved()
    {
        // Only the promoted flag decides; a stray troop id on a companion row changes nothing.
        _sut.ReleaseWarden("companion_1", promoted: false, promotedFromTroopId: "troop_a");

        Assert.AreEqual(0, _sut.RemovedCompanions.Count);
        Assert.AreEqual(0, _sut.RefundedTroops.Count);
    }

    [TestMethod]
    public void ReleaseWarden_PromotedWardenWithRefuge_ReturnsToTheRanksAsHisSourceTroop()
    {
        _sut.WithRefuge = true;

        _sut.ReleaseWarden("hero_minted", promoted: true, promotedFromTroopId: "troop_a");

        CollectionAssert.AreEqual(
            new[] { "move:hero_minted", "remove:hero_minted", "troop:troop_a", "message:troop_a" },
            _sut.Steps,
            "he leaves the refuge by action first (a raw roster merge nulls PartyBelongedTo), is "
            + "removed from the main party the way UnwindPromotion removes a minted hero, and "
            + "exactly one of his source troop rejoins");
    }

    [TestMethod]
    public void ReleaseWarden_PromotedWardenElsewhere_NoOp()
    {
        _sut.WithRefuge = false;

        _sut.ReleaseWarden("hero_minted", promoted: true, promotedFromTroopId: "troop_a");

        Assert.AreEqual(0, _sut.Steps.Count,
            "a captured, dead or absent promoted warden is left where fate put him, and no "
            + "soldier comes back for a hero the player does not hand over");
    }

    [DataTestMethod]
    [DataRow("spouse", DisplayName = "married")]
    [DataRow("children", DisplayName = "has children")]
    [DataRow("clanLeader", DisplayName = "leads a clan")]
    [DataRow("notCompanion", DisplayName = "no longer the player's companion")]
    [DataRow("noFacts", DisplayName = "hero record missing")]
    public void ReleaseWarden_PromotedWardenWithTies_StaysACompanion(string tie)
    {
        var facts = new PromotedWardenFacts { IsPlayerClanCompanion = true };
        switch (tie)
        {
            case "spouse": facts.HasSpouse = true; break;
            case "children": facts.HasChildren = true; break;
            case "clanLeader": facts.IsClanLeader = true; break;
            case "notCompanion": facts.IsPlayerClanCompanion = false; break;
            case "noFacts": facts = null; break;
        }
        _sut.Facts = facts;

        _sut.ReleaseWarden("hero_minted", promoted: true, promotedFromTroopId: "troop_a");

        CollectionAssert.AreEqual(new[] { "hero_minted" }, _sut.MovedToMainParty,
            "he still comes home by action");
        Assert.AreEqual(0, _sut.RemovedCompanions.Count,
            "removing a hero with family or a role would leave a widow, orphans or an empty seat");
        Assert.AreEqual(0, _sut.RefundedTroops.Count);
    }

    [DataTestMethod]
    [DataRow(null, DisplayName = "no troop recorded")]
    [DataRow("", DisplayName = "empty troop id")]
    [DataRow("troop_removed_by_update", DisplayName = "troop no longer in the game")]
    public void ReleaseWarden_PromotedWardenSourceTroopUnknown_FallsBackToCultureBasicTroop(string troopId)
    {
        _sut.ReleaseWarden("hero_minted", promoted: true, promotedFromTroopId: troopId);

        CollectionAssert.AreEqual(new[] { "hero_minted" }, _sut.RemovedCompanions);
        CollectionAssert.AreEqual(new[] { "culture_basic" }, _sut.RefundedTroops);
    }

    [TestMethod]
    public void ReleaseWarden_PromotedWardenNoTroopResolvable_StaysACompanion()
    {
        _sut.CultureBasicTroop = null;

        _sut.ReleaseWarden("hero_minted", promoted: true, promotedFromTroopId: "troop_removed_by_update");

        CollectionAssert.AreEqual(new[] { "hero_minted" }, _sut.MovedToMainParty);
        Assert.AreEqual(0, _sut.RemovedCompanions.Count,
            "never take the hero away when no soldier can be given back");
        Assert.AreEqual(0, _sut.RefundedTroops.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("hero_minted")));
    }

    [TestMethod]
    public void ReleaseWarden_PromotedWardenRemovalRefused_NoSoldierAndNoMessage()
    {
        _sut.RemoveMintedResult = false;

        _sut.ReleaseWarden("hero_minted", promoted: true, promotedFromTroopId: "troop_a");

        Assert.AreEqual(0, _sut.RefundedTroops.Count,
            "a soldier on top of a hero who is still there would duplicate him");
        Assert.AreEqual(0, _sut.RanksMessages.Count);
    }

    [TestMethod]
    public void MayReturnToRanks_PlainPromotedCompanion_True()
    {
        Assert.IsTrue(WardenService.MayReturnToRanks(new PromotedWardenFacts { IsPlayerClanCompanion = true }));
    }

    // --- UnwindPromotion (the never-attached rollback window) ---

    [TestMethod]
    public void UnwindPromotion_RemovesMintedCompanionAndRefundsTheSoldier()
    {
        _sut.UnwindPromotion("hero_minted", "troop_a");

        CollectionAssert.AreEqual(new[] { "hero_minted" }, _sut.RemovedCompanions);
        CollectionAssert.AreEqual(new[] { "troop_a" }, _sut.RefundedTroops,
            "exactly the one soldier the promotion consumed comes back");
    }

    [TestMethod]
    public void UnwindPromotion_RemoveRefused_NoRefund()
    {
        _sut.RemoveMintedResult = false;

        _sut.UnwindPromotion("hero_minted", "troop_a");

        Assert.AreEqual(0, _sut.RefundedTroops.Count,
            "a refund without the removal would duplicate the soldier");
    }

    [TestMethod]
    public void UnwindPromotion_NullOrEmptyArgs_NoOp()
    {
        _sut.UnwindPromotion(null, "troop_a");
        _sut.UnwindPromotion("hero_minted", null);
        _sut.UnwindPromotion("", "");

        Assert.AreEqual(0, _sut.RemovedCompanions.Count);
        Assert.AreEqual(0, _sut.RefundedTroops.Count);
    }

    [TestMethod]
    public void ReleaseWarden_CompanionElsewhere_NoOp()
    {
        _sut.WithRefuge = false;

        _sut.ReleaseWarden("companion_1", promoted: false, promotedFromTroopId: null);

        Assert.AreEqual(0, _sut.MovedToMainParty.Count,
            "a captured or hospitalised warden is left where fate put him");
    }

    [TestMethod]
    public void ReleaseWarden_NullOrEmptyHeroId_NoOp()
    {
        _sut.ReleaseWarden(null, promoted: false, promotedFromTroopId: null);
        _sut.ReleaseWarden("", promoted: true, promotedFromTroopId: "troop_a");

        Assert.AreEqual(0, _sut.MovedToMainParty.Count);
    }

    // --- CompanionsInMainParty (the eligibility filter; the roster seam only reads) ---

    private static PartyHeroInfo PartyHero(string id, bool companion = true, bool mainHero = false) =>
        new PartyHeroInfo
        {
            HeroId = id,
            DisplayName = "Name of " + id,
            IsPlayerClanCompanion = companion,
            IsMainHero = mainHero,
        };

    [TestMethod]
    public void CompanionsInMainParty_MainHero_Excluded()
    {
        _sut.PartyHeroes = new List<PartyHeroInfo> { PartyHero("main_hero", companion: true, mainHero: true) };

        Assert.AreEqual(0, _sut.CompanionsInMainParty().Count);
    }

    [TestMethod]
    public void CompanionsInMainParty_HeroNotAPlayerClanCompanion_Excluded()
    {
        _sut.PartyHeroes = new List<PartyHeroInfo> { PartyHero("visiting_lord", companion: false) };

        Assert.AreEqual(0, _sut.CompanionsInMainParty().Count,
            "a visiting noble or quest hero must never be strandable in a refuge");
    }

    [TestMethod]
    public void CompanionsInMainParty_NullEntry_Skipped()
    {
        _sut.PartyHeroes = new List<PartyHeroInfo> { null, PartyHero("companion_1") };

        var companions = _sut.CompanionsInMainParty();

        Assert.AreEqual(1, companions.Count);
        Assert.AreEqual("companion_1", companions[0].Id);
    }

    [TestMethod]
    public void CompanionsInMainParty_MapsEachCompanionInRosterOrder()
    {
        _sut.PartyHeroes = new List<PartyHeroInfo> { PartyHero("companion_2"), PartyHero("companion_1") };

        var companions = _sut.CompanionsInMainParty();

        Assert.AreEqual(2, companions.Count);
        Assert.AreEqual("companion_2", companions[0].Id);
        Assert.AreEqual("Name of companion_2", companions[0].DisplayName);
        Assert.IsTrue(companions[0].IsCompanion);
        Assert.AreEqual(0, companions[0].Tier);
        Assert.AreEqual("companion_1", companions[1].Id);
    }

    [TestMethod]
    public void Candidates_ListsOnlyEligibleCompanionsFromTheRoster()
    {
        _sut.SlotFree = false;
        _sut.PartyHeroes = new List<PartyHeroInfo>
        {
            PartyHero("main_hero", mainHero: true),
            PartyHero("visiting_lord", companion: false),
            PartyHero("companion_1"),
        };

        var candidates = _sut.Candidates();

        Assert.AreEqual(1, candidates.Count);
        Assert.AreEqual("companion_1", candidates[0].Id);
    }

    // --- MintCompanionFromTroop (culture, template, age and rename policy; the seams do one engine step each) ---

    [TestMethod]
    public void MintCompanionFromTroop_Default_RunsTheEngineStepsInSourceOrder()
    {
        var heroId = _sut.MintCompanionFromTroop("troop_1");

        Assert.AreEqual("hero_minted", heroId);
        CollectionAssert.AreEqual(
            new[]
            {
                "source:troop_1",
                "template:culture_troop",
                "comesOfAge",
                "rng:14",
                "create:template_culture:29",
                "rename:hero_minted:troop_1",
                "enrol:hero_minted",
            },
            _sut.MintSteps,
            "the template draw and the age draw use the campaign RNG in the source's order");
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default);
    }

    [TestMethod]
    public void MintCompanionFromTroop_NoPromotionSource_MintsNothing()
    {
        _sut.Source = null;

        Assert.IsNull(_sut.MintCompanionFromTroop("troop_1"));
        CollectionAssert.AreEqual(new[] { "source:troop_1" }, _sut.MintSteps);
    }

    [TestMethod]
    public void MintCompanionFromTroop_TroopIsAHero_MintsNothing()
    {
        _sut.Source.TroopIsHero = true;

        Assert.IsNull(_sut.MintCompanionFromTroop("troop_1"));
        CollectionAssert.AreEqual(new[] { "source:troop_1" }, _sut.MintSteps);
    }

    [TestMethod]
    public void MintCompanionFromTroop_TroopWithoutCulture_UsesThePlayerCulture()
    {
        _sut.Source.TroopCultureId = null;

        _sut.MintCompanionFromTroop("troop_1");

        Assert.AreEqual("template:culture_player", _sut.MintSteps[1]);
        Assert.AreEqual("create:template_culture:29", _sut.MintSteps[4]);
    }

    [TestMethod]
    public void MintCompanionFromTroop_NoCultureAnywhere_AsksForAnyTemplateOnce()
    {
        _sut.Source.TroopCultureId = null;
        _sut.Source.PlayerCultureId = null;

        _sut.MintCompanionFromTroop("troop_1");

        Assert.AreEqual("template:<any>", _sut.MintSteps[1]);
        Assert.AreEqual("comesOfAge", _sut.MintSteps[2]);
        Assert.AreEqual("create:template_any:29", _sut.MintSteps[4]);
    }

    [TestMethod]
    public void MintCompanionFromTroop_NoTemplateForTheCulture_FallsBackToAnyTemplate()
    {
        _sut.CultureTemplate = null;

        _sut.MintCompanionFromTroop("troop_1");

        Assert.AreEqual("template:culture_troop", _sut.MintSteps[1]);
        Assert.AreEqual("template:<any>", _sut.MintSteps[2]);
        Assert.AreEqual("create:template_any:29", _sut.MintSteps[5]);
    }

    [TestMethod]
    public void MintCompanionFromTroop_NoTemplateAtAll_StopsBeforeTheRandomDraw()
    {
        _sut.CultureTemplate = null;
        _sut.AnyTemplate = null;

        Assert.IsNull(_sut.MintCompanionFromTroop("troop_1"));
        CollectionAssert.AreEqual(
            new[] { "source:troop_1", "template:culture_troop", "template:<any>" }, _sut.MintSteps);
    }

    [TestMethod]
    public void MintCompanionFromTroop_Age_IsComesOfAgePlusFourPlusOneDrawBelowFourteen()
    {
        _sut.ComesOfAge = 16;
        _sut.RandomIntResult = 13;

        _sut.MintCompanionFromTroop("troop_1");

        CollectionAssert.Contains(_sut.MintSteps, "create:template_culture:33");
        Assert.AreEqual(1, _sut.MintSteps.FindAll(s => s.StartsWith("rng:", System.StringComparison.Ordinal)).Count,
            "exactly one RandomInt draw per mint");
        CollectionAssert.Contains(_sut.MintSteps, "rng:14");
    }

    [TestMethod]
    public void MintCompanionFromTroop_NoAgeModel_ComesOfAgeDefaultsTo18()
    {
        _sut.ComesOfAge = null;
        _sut.RandomIntResult = 0;

        _sut.MintCompanionFromTroop("troop_1");

        CollectionAssert.Contains(_sut.MintSteps, "create:template_culture:22");
    }

    [TestMethod]
    public void MintCompanionFromTroop_CreationRefused_NoRenameNoEnrol()
    {
        _sut.MintResult = null;

        Assert.IsNull(_sut.MintCompanionFromTroop("troop_1"));
        Assert.AreEqual(5, _sut.MintSteps.Count);
        Assert.AreEqual("create:template_culture:29", _sut.MintSteps[4]);
    }

    [TestMethod]
    public void MintCompanionFromTroop_RenameThrows_WarnsAndStillEnrols()
    {
        _sut.RenameThrows = true;

        var heroId = _sut.MintCompanionFromTroop("troop_1");

        Assert.AreEqual("hero_minted", heroId, "the rename is cosmetic; the promotion goes on");
        Assert.AreEqual("enrol:hero_minted", _sut.MintSteps[_sut.MintSteps.Count - 1]);
        _logger.Received(1).LogWarning("[Refuge] promoted-warden rename failed: text manager missing");
    }

    [TestMethod]
    public void MintCompanionFromTroop_CreatedHero_HandedToRenameAndEnrolAsIs()
    {
        _sut.MintCompanionFromTroop("troop_1");

        Assert.AreEqual(2, _sut.MintedHandedOn.Count);
        Assert.AreSame(_sut.LastMinted, _sut.MintedHandedOn[0], "the rename acts on the created hero, not a lookup");
        Assert.AreSame(_sut.LastMinted, _sut.MintedHandedOn[1], "the enrol acts on the created hero, not a lookup");
    }
}
