using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CultureConversion;
using TAOM.Features.CultureConversion.GarrisonSwap;
using TAOM.Features.CultureConversion.GarrisonSwap.Domain;

namespace TAOM.Tests.Features.CultureConversion;

[TestClass]
public class GarrisonCultureSwapServiceTests
{
    private const string Town = "town_ES1";
    private const string From = "gondor";
    private const string To = "mordor";

    private IGarrisonCultureSwapAdapter _adapter = null!;
    private ITroopCultureMapper _mapper = null!;
    private ICultureConversionSettingsProvider _settings = null!;
    private IModLogger _logger = null!;
    private GarrisonCultureSwapService _sut = null!;

    private static readonly TroopSwapPlan OneSwap =
        new(new List<TroopSwap> { new("gondor_inf", "mordor_inf", 12, 0) }, new List<string>());

    [TestInitialize]
    public void Setup()
    {
        _adapter = Substitute.For<IGarrisonCultureSwapAdapter>();
        _mapper = Substitute.For<ITroopCultureMapper>();
        _settings = Substitute.For<ICultureConversionSettingsProvider>();
        _logger = Substitute.For<IModLogger>();
        _sut = new GarrisonCultureSwapService(_adapter, _mapper, _settings, _logger);

        _settings.ReplaceGarrisonOnConversion.Returns(true);
        _settings.ReplaceMilitiaOnConversion.Returns(true);
        _settings.ReplaceGarrisonInPlayerFiefs.Returns(true);

        // A target culture that has both regular troops and militia slots.
        _adapter.GetCultureTroopIndex(To).Returns(
            new CultureTroopIndex(To, new[] { new CultureTroopCandidate("mordor_inf", 3, TroopRole.Infantry) }));
        _adapter.GetCultureMilitiaTroops(Arg.Any<string>())
            .Returns(ci => new CultureMilitiaTroops(ci.Arg<string>(), "m", "me", "r", "re"));

        _adapter.GetGarrisonRoster(Arg.Any<string>()).Returns(new List<GarrisonTroopInfo>());
        _adapter.GetMilitiaRoster(Arg.Any<string>()).Returns(new List<GarrisonTroopInfo>());
        _mapper.MapGarrison(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<GarrisonTroopInfo>>(), Arg.Any<CultureTroopIndex>())
            .Returns(TroopSwapPlan.Empty);
        _mapper.MapMilitia(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<GarrisonTroopInfo>>(),
                Arg.Any<CultureMilitiaTroops?>(), Arg.Any<CultureMilitiaTroops?>(), Arg.Any<CultureTroopIndex?>())
            .Returns(TroopSwapPlan.Empty);
        _adapter.ApplyGarrisonSwaps(Arg.Any<string>(), Arg.Any<IReadOnlyList<TroopSwap>>()).Returns(12);
        _adapter.ApplyMilitiaSwaps(Arg.Any<string>(), Arg.Any<IReadOnlyList<TroopSwap>>()).Returns(7);
    }

    private void GivenGarrisonHasSwaps() =>
        _mapper.MapGarrison(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<GarrisonTroopInfo>>(), Arg.Any<CultureTroopIndex>())
            .Returns(OneSwap);

    private void GivenMilitiaHasSwaps() =>
        _mapper.MapMilitia(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<GarrisonTroopInfo>>(),
                Arg.Any<CultureMilitiaTroops?>(), Arg.Any<CultureMilitiaTroops?>(), Arg.Any<CultureTroopIndex?>())
            .Returns(OneSwap);

    private void Swap(bool isPlayerOwned = false) => _sut.SwapSettlementTroops(Town, From, To, isPlayerOwned);

    // --- Toggles ---

    [TestMethod]
    public void SwapSettlementTroops_BothTogglesOff_ReadsNothingAndWritesNothing()
    {
        _settings.ReplaceGarrisonOnConversion.Returns(false);
        _settings.ReplaceMilitiaOnConversion.Returns(false);

        Swap();

        _adapter.DidNotReceive().GetCultureTroopIndex(Arg.Any<string>());
        _adapter.DidNotReceive().ApplyGarrisonSwaps(Arg.Any<string>(), Arg.Any<IReadOnlyList<TroopSwap>>());
        _adapter.DidNotReceive().ApplyMilitiaSwaps(Arg.Any<string>(), Arg.Any<IReadOnlyList<TroopSwap>>());
    }

    [TestMethod]
    public void SwapSettlementTroops_GarrisonToggleOff_StillSwapsMilitia()
    {
        _settings.ReplaceGarrisonOnConversion.Returns(false);
        GivenMilitiaHasSwaps();

        Swap();

        _adapter.DidNotReceive().ApplyGarrisonSwaps(Arg.Any<string>(), Arg.Any<IReadOnlyList<TroopSwap>>());
        _adapter.Received(1).ApplyMilitiaSwaps(Town, Arg.Any<IReadOnlyList<TroopSwap>>());
    }

    [TestMethod]
    public void SwapSettlementTroops_MilitiaToggleOff_StillSwapsGarrison()
    {
        _settings.ReplaceMilitiaOnConversion.Returns(false);
        GivenGarrisonHasSwaps();

        Swap();

        _adapter.Received(1).ApplyGarrisonSwaps(Town, Arg.Any<IReadOnlyList<TroopSwap>>());
        _adapter.DidNotReceive().ApplyMilitiaSwaps(Arg.Any<string>(), Arg.Any<IReadOnlyList<TroopSwap>>());
    }

    // --- The dedicated player-fief toggle ---

    [TestMethod]
    public void SwapSettlementTroops_PlayerFiefWithDedicatedToggleOff_IsLeftAlone()
    {
        _settings.ReplaceGarrisonInPlayerFiefs.Returns(false);
        GivenGarrisonHasSwaps();
        GivenMilitiaHasSwaps();

        Swap(isPlayerOwned: true);

        _adapter.DidNotReceive().ApplyGarrisonSwaps(Arg.Any<string>(), Arg.Any<IReadOnlyList<TroopSwap>>());
        _adapter.DidNotReceive().ApplyMilitiaSwaps(Arg.Any<string>(), Arg.Any<IReadOnlyList<TroopSwap>>());
    }

    [TestMethod]
    public void SwapSettlementTroops_AiFiefWithPlayerToggleOff_StillSwaps()
    {
        _settings.ReplaceGarrisonInPlayerFiefs.Returns(false);
        GivenGarrisonHasSwaps();

        Swap(isPlayerOwned: false);

        _adapter.Received(1).ApplyGarrisonSwaps(Town, Arg.Any<IReadOnlyList<TroopSwap>>());
    }

    // --- The player notice ---

    [TestMethod]
    public void SwapSettlementTroops_PlayerFief_NotifiesWithTheTotalReplaced()
    {
        GivenGarrisonHasSwaps();
        GivenMilitiaHasSwaps();

        Swap(isPlayerOwned: true);

        _adapter.Received(1).NotifyPlayerTroopsSwapped(Town, 19); // 12 garrison + 7 militia
    }

    [TestMethod]
    public void SwapSettlementTroops_AiFief_DoesNotNotify()
    {
        GivenGarrisonHasSwaps();

        Swap(isPlayerOwned: false);

        _adapter.DidNotReceive().NotifyPlayerTroopsSwapped(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void SwapSettlementTroops_NothingReplaced_DoesNotNotify()
    {
        GivenGarrisonHasSwaps();
        _adapter.ApplyGarrisonSwaps(Arg.Any<string>(), Arg.Any<IReadOnlyList<TroopSwap>>()).Returns(0);
        _adapter.ApplyMilitiaSwaps(Arg.Any<string>(), Arg.Any<IReadOnlyList<TroopSwap>>()).Returns(0);

        Swap(isPlayerOwned: true);

        _adapter.DidNotReceive().NotifyPlayerTroopsSwapped(Arg.Any<string>(), Arg.Any<int>());
    }

    // --- Degraded data ---

    [TestMethod]
    public void SwapSettlementTroops_TargetCultureWithNoTroopsOrMilitia_WarnsAndChangesNothing()
    {
        _adapter.GetCultureTroopIndex(To).Returns((CultureTroopIndex?)null);
        _adapter.GetCultureMilitiaTroops(To).Returns((CultureMilitiaTroops?)null);

        Swap();

        _adapter.DidNotReceive().ApplyGarrisonSwaps(Arg.Any<string>(), Arg.Any<IReadOnlyList<TroopSwap>>());
        _adapter.DidNotReceive().ApplyMilitiaSwaps(Arg.Any<string>(), Arg.Any<IReadOnlyList<TroopSwap>>());
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains(To)));
    }

    [TestMethod]
    public void SwapSettlementTroops_TargetCultureWithEmptyMilitiaSlots_WarnsAndChangesNothing()
    {
        // The case CultureMilitiaTroops.IsEmpty exists for, and the one a null test misses: the
        // eight minor factions resolve to a real instance with four empty slots. Without IsEmpty
        // this reached the militia path and dropped every stack onto the garrison ladder, which
        // would put regular line troops in the militia party.
        _adapter.GetCultureTroopIndex(To).Returns((CultureTroopIndex?)null);
        _adapter.GetCultureMilitiaTroops(To)
            .Returns(new CultureMilitiaTroops(To, null, null, null, null));

        Swap();

        _adapter.DidNotReceive().ApplyMilitiaSwaps(Arg.Any<string>(), Arg.Any<IReadOnlyList<TroopSwap>>());
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains(To)));
    }

    [TestMethod]
    public void SwapSettlementTroops_TargetCultureWithMilitiaButNoRegulars_StillSwapsMilitia()
    {
        _adapter.GetCultureTroopIndex(To).Returns(new CultureTroopIndex(To, new CultureTroopCandidate[0]));
        GivenMilitiaHasSwaps();

        Swap();

        _adapter.DidNotReceive().ApplyGarrisonSwaps(Arg.Any<string>(), Arg.Any<IReadOnlyList<TroopSwap>>());
        _adapter.Received(1).ApplyMilitiaSwaps(Town, Arg.Any<IReadOnlyList<TroopSwap>>());
    }

    [TestMethod]
    public void SwapSettlementTroops_UnmappedStacks_AreReportedAndLeftAlone()
    {
        _mapper.MapGarrison(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<GarrisonTroopInfo>>(), Arg.Any<CultureTroopIndex>())
            .Returns(new TroopSwapPlan(new List<TroopSwap>(), new List<string> { "gondor_archer" }));

        Swap();

        _adapter.DidNotReceive().ApplyGarrisonSwaps(Arg.Any<string>(), Arg.Any<IReadOnlyList<TroopSwap>>());
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("gondor_archer")));
    }

    [TestMethod]
    public void SwapSettlementTroops_NoSwapsPlanned_DoesNotCallApply()
    {
        Swap();

        _adapter.DidNotReceive().ApplyGarrisonSwaps(Arg.Any<string>(), Arg.Any<IReadOnlyList<TroopSwap>>());
        _adapter.DidNotReceive().ApplyMilitiaSwaps(Arg.Any<string>(), Arg.Any<IReadOnlyList<TroopSwap>>());
    }

    [TestMethod]
    public void SwapSettlementTroops_BlankSettlementOrTargetCulture_DoesNothing()
    {
        _sut.SwapSettlementTroops("", From, To, false);
        _sut.SwapSettlementTroops(Town, From, "", false);

        _adapter.DidNotReceive().GetCultureTroopIndex(Arg.Any<string>());
    }

    [TestMethod]
    public void SwapSettlementTroops_MatchesMilitiaSlotsAgainstTheOldCulture()
    {
        Swap();

        // The whole reason ApplyConversion captures the culture before flipping it.
        _adapter.Received().GetCultureMilitiaTroops(From);
        _mapper.Received(1).MapMilitia(Town, To, Arg.Any<IReadOnlyList<GarrisonTroopInfo>>(),
            Arg.Is<CultureMilitiaTroops?>(m => m != null && m.CultureId == From),
            Arg.Is<CultureMilitiaTroops?>(m => m != null && m.CultureId == To),
            Arg.Any<CultureTroopIndex?>());
    }

    [TestMethod]
    public void SwapSettlementTroops_BlankSourceCulture_MapsWithNoSourceMilitia()
    {
        // Not an early return: this is the blank input that proceeds. Without a source culture there
        // are no old militia slots to match against, so every militia stack falls through to the
        // garrison ladder rather than mapping slot-for-slot.
        _sut.SwapSettlementTroops(Town, "", To, false);

        _adapter.DidNotReceive().GetCultureMilitiaTroops("");
        _mapper.Received(1).MapMilitia(Town, To, Arg.Any<IReadOnlyList<GarrisonTroopInfo>>(),
            Arg.Is<CultureMilitiaTroops?>(m => m == null),
            Arg.Any<CultureMilitiaTroops?>(),
            Arg.Any<CultureTroopIndex?>());
    }
}
