using System;
using System.Linq;
using System.Reflection;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.AlignmentDesertion;
using TAOM.Features.AlignmentRecruitment;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.FieldCamp;
using TAOM.Features.SupplyLines;
using TAOM.Features.CaravanTrade;
using TAOM.Features.CastleRecruitment;
using TAOM.Features.EyeOfSauron;
using TAOM.Features.FieldCommission;
using TAOM.Features.FireAndTribute;
using TAOM.Features.FiefManagement;
using TAOM.Features.HarvestRoad;
using TAOM.Features.HiddenHosts;
using TAOM.Features.KeepWardens;
using TAOM.Features.KingsMuster;
using TAOM.Features.FromTheAshes;
using TAOM.Features.GoodDeeds;
using TAOM.Features.BondOfHire;
using TAOM.Features.SmithsMeasure;
using TAOM.Features.LongMemory;
using TAOM.Features.TokensOfEsteem;
using TAOM.Features.Provender;
using TAOM.Features.FieldCommission.Domain;
using TAOM.Features.NameplateCull;
using TAOM.Features.Namegiving;
using TAOM.Features.PartyIconScale;
using TAOM.Features.PosternGate;
using TAOM.Features.QuickActions;
using TAOM.Features.RedArrow;
using TAOM.Features.Relations;
using TAOM.Features.RealmBorders;
using TAOM.Features.LoremastersLine;
using TAOM.Features.HearthRoll;
using TAOM.Features.PrisonerBanners;
using TAOM.Features.WardensTally;
using TAOM.Features.ReevesLedger;
using TAOM.Features.SeasonedHosts;
using TAOM.Features.SwornAnew;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.TimeAcceleration;
using TAOM.Features.UnderTheEaves;
using TAOM.Features.UnlookedForAid;
using TAOM.Features.WarChronicle;
using TAOM.Features.WatchfulHolds;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features;

/// <summary>
/// Campaign settings providers are read per party per hour, per caravan score, per party per day and
/// every map frame. Each takes the MCM settings reference once, in a private lazy <c>Settings</c>
/// accessor, and reads through it; no other member may resolve MCM's <c>Instance</c>, which walks MCM's
/// settings containers on every call. The accessor caches only a non-null instance, so a resolve before
/// MCM is up cannot pin the defaults. Pattern: BattleBalanceSettingsProvider (02157b18).
/// </summary>
[TestClass]
public class CampaignHotPathSettingsProvidersTests
{
    private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic
                                          | BindingFlags.Instance | BindingFlags.Static;

    // MCM declares Instance on a generic base (GlobalSettings<T>), so match any declaring type the settings class derives from.
    private static bool IsInstanceGetter(MethodBase m) =>
        m.Name == "get_Instance" && m.DeclaringType != null
        && (m.DeclaringType.IsAssignableFrom(typeof(TaomSettings))
            || m.DeclaringType.IsAssignableFrom(typeof(BattleLoadDiagnosticsSettings)));

    private static bool CallsInstance(MethodBase m)
    {
        var il = m.GetMethodBody()?.GetILAsByteArray();
        return il != null && IlCallScanner.ExtractCalledMethods(m, il).Any(IsInstanceGetter);
    }

    [DataTestMethod]
    [DataRow(typeof(CaravanTradeSettingsProvider))]
    [DataRow(typeof(CastleRecruitmentSettingsProvider))]
    [DataRow(typeof(AlignmentDesertionSettingsProvider))]
    [DataRow(typeof(RealmBordersSettingsProvider))]
    [DataRow(typeof(FieldCommissionSettingsProvider))]
    [DataRow(typeof(QuickActionsSettingsProvider))]
    [DataRow(typeof(PartyIconScaleConfig))]
    [DataRow(typeof(TimeAccelerationSettingsProvider))]
    // #746: read every campaign frame (supply lines always, the field camp while it stands).
    [DataRow(typeof(SupplyLinesSettingsProvider))]
    [DataRow(typeof(CampSettingsProvider))]
    // #765: the war-effect strength is re-read by the registry's daily re-bake, the chronicle switch and army focus by each chronicle step.
    [DataRow(typeof(WarChronicleSettingsProvider))]
    // Read every campaign-map frame, from the Battle Load Diagnostics page.
    [DataRow(typeof(NameplateCullSettingsProvider))]
    // Shadowfax Screens: read on every screen open and by each screen's timer points, from the same page.
    [DataRow(typeof(ShadowfaxScreensSettingsProvider))]
    // #789: the Fiefs map-bar button polls the fief hub opener every frame, which reads these.
    [DataRow(typeof(FiefManagementSettingsProvider))]
    // #765 M8: the hunter's speed multiplier is read every campaign frame while a hunt runs.
    [DataRow(typeof(EyeOfSauronSettingsProvider))]
    // #822: both switches are read for every party on every hourly tick.
    [DataRow(typeof(WatchfulHoldsSettingsProvider))]
    // #846: the nameplate toggle is read for every visible party nameplate every map frame.
    [DataRow(typeof(HiddenHostsSettingsProvider))]
    // #849: the forest toggle is read for every spotting query (the visibility pass reads the main party's sight every frame).
    [DataRow(typeof(UnderTheEavesSettingsProvider))]
    // #862: the master switch is read on every relation read (main thread).
    [DataRow(typeof(PersonalRelationSettingsProvider))]
    // The defender's siege wait menu re-checks the break-out options every frame, and each check asks the Postern Gate model.
    [DataRow(typeof(PosternGateSettingsProvider))]
    // The host menus are plain menus, so the start conditions run when a menu opens or refreshes; the accessor still takes the settings once.
    [DataRow(typeof(FromTheAshesSettingsProvider))]
    // Tokens of Esteem: the gate and the list read the group at every gift line a conversation shows.
    [DataRow(typeof(GiftSettingsProvider))]
    // Not hot paths: Good Deeds reads its group at each release, donation and tournament, Long Memory at each raid, forced taking
    // and at the two no-resist options (the option id is tested before the read). The accessor still takes the settings once.
    [DataRow(typeof(GoodDeedsSettingsProvider))]
    [DataRow(typeof(LongMemorySettingsProvider))]
    // Read on a main-party settlement visit or a daily tick while an order is on, the Quartermaster's listing and each morale line.
    [DataRow(typeof(ProvenderSettingsProvider))]
    // Not a hot path: both switches are read once, when the town management window opens. The accessor still takes the settings once.
    [DataRow(typeof(ReevesLedgerSettingsProvider))]
    // Not a hot path: read once per skill view model, when the screen builds it, never per click or frame. The accessor still takes the settings once.
    [DataRow(typeof(LoremastersLineSettingsProvider))]
    // Not a hot path: read on each row refresh, a few times per row when the clan screen opens or comes back to the front, never per frame. The accessor still takes the settings once.
    [DataRow(typeof(HearthRollSettingsProvider))]
    // Not a hot path: read once per party screen row, when the row is built, never per frame. The accessor still takes the settings once.
    [DataRow(typeof(PrisonerBannersSettingsProvider))]
    // Not a hot path: read once when a town or castle dungeon menu opens, never per frame. The accessor still takes the settings once.
    [DataRow(typeof(WardensTallySettingsProvider))]
    // #870: the four switches are read on every hourly tick while a raiding host rides.
    [DataRow(typeof(FireAndTributeSettingsProvider))]
    // #887: read per foreign candidate per AI lord think (Patch114) and per notable per party-hour (TaomVolunteerModel).
    [DataRow(typeof(RecruitmentAlignmentSettingsProvider))]
    // #872: read on the AI worker thread for every avoid question; the constructor takes the settings once, on the main thread.
    [DataRow(typeof(UnlookedForAidSettingsProvider))]
    // #869: read at each conversation question and by the ruler's prisoner line's condition; the accessor takes the settings once.
    [DataRow(typeof(SwornAnewSettingsProvider))]
    // #888: the patrol queue asks the castle patrol model for every settlement it visits.
    [DataRow(typeof(CastlePatrolSettingsProvider))]
    // #912: read at each villager delivery into a town and at each hourly food check of an AI party standing in one.
    [DataRow(typeof(HarvestRoadSettingsProvider))]
    // Watch-Silver and Tidings of the Lists: read when a conversation or an arena menu is built; the accessor takes the settings once.
    [DataRow(typeof(TAOM.Features.SettlementServices.SettlementServicesSettingsProvider))]
    // Notable Services: read by each of three dialog conditions when a conversation is built; the accessor takes the settings once.
    [DataRow(typeof(TAOM.Features.NotableServices.NotableServicesSettingsProvider))]
    // Inn Rounds: read by each of three dialog conditions when a tavern keeper's conversation is built; the accessor takes the settings once.
    [DataRow(typeof(TAOM.Features.InnRounds.InnRoundsSettingsProvider))]
    // #873: not a hot path (read a few times per party row when the Army Management screen opens); it still takes the settings once.
    [DataRow(typeof(RedArrowSettingsProvider))]
    // #867: not a hot path; read once when a clan signs on as a mercenary. The accessor still takes the settings once.
    [DataRow(typeof(BondOfHireSettingsProvider))]

    // #890: the skill XP multiplier is read at every skill XP grant, the training bonus for every troop of every AI lord party each day.
    [DataRow(typeof(SeasonedHostsSettingsProvider))]
    // #874: the call cost runs on the AI's hourly army think, once per candidate (vanilla's CanLordCreateArmy prices each); the constructor takes the settings once.
    [DataRow(typeof(KingsMusterSettingsProvider))]

    // #910: the smithing model asks for the cap once per smelting-list item and per smelt; the accessor takes the settings once.
    [DataRow(typeof(SmithsMeasureSettingsProvider))]
    public void OnlyTheLazySettingsAccessor_ReadsTheMcmInstance(Type provider)
    {
        var accessor = provider.GetProperty("Settings", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            ?.GetGetMethod(true);
        Assert.IsNotNull(accessor, provider.Name + ": the private lazy Settings accessor");
        Assert.IsTrue(CallsInstance(accessor), provider.Name + ": the lazy accessor takes the settings reference");

        var offenders = provider.GetMethods(Declared).Cast<MethodBase>()
            .Concat(provider.GetConstructors(Declared))
            .Where(m => m.Name != "get_Settings" && CallsInstance(m))
            .Select(m => m.Name)
            .ToList();
        Assert.AreEqual(0, offenders.Count,
            provider.Name + " resolves the MCM instance outside the lazy accessor: " + string.Join(", ", offenders));
    }

    // A second (internal, test-only) constructor must leave exactly one public constructor, or DryIoc
    // cannot select one at registration. FieldCommission registers through a delegate and
    // PartyIconScaleConfig is static, so neither is a row here.
    [DataTestMethod]
    [DataRow(typeof(ICaravanTradeSettingsProvider), typeof(CaravanTradeSettingsProvider))]
    [DataRow(typeof(ICastleRecruitmentSettingsProvider), typeof(CastleRecruitmentSettingsProvider))]
    [DataRow(typeof(IAlignmentDesertionSettingsProvider), typeof(AlignmentDesertionSettingsProvider))]
    [DataRow(typeof(IRealmBordersSettings), typeof(RealmBordersSettingsProvider))]
    [DataRow(typeof(IQuickActionsSettingsProvider), typeof(QuickActionsSettingsProvider))]
    [DataRow(typeof(ITimeAccelerationSettingsProvider), typeof(TimeAccelerationSettingsProvider))]
    [DataRow(typeof(ISupplyLinesSettingsProvider), typeof(SupplyLinesSettingsProvider))]
    [DataRow(typeof(ICampSettingsProvider), typeof(CampSettingsProvider))]
    [DataRow(typeof(IWarChronicleSettingsProvider), typeof(WarChronicleSettingsProvider))]
    [DataRow(typeof(INameplateCullSettingsProvider), typeof(NameplateCullSettingsProvider))]
    [DataRow(typeof(IShadowfaxScreensSettingsProvider), typeof(ShadowfaxScreensSettingsProvider))]
    [DataRow(typeof(IFiefManagementSettingsProvider), typeof(FiefManagementSettingsProvider))]
    [DataRow(typeof(IEyeOfSauronSettings), typeof(EyeOfSauronSettingsProvider))]
    [DataRow(typeof(IWatchfulHoldsSettings), typeof(WatchfulHoldsSettingsProvider))]
    [DataRow(typeof(IHiddenHostsSettings), typeof(HiddenHostsSettingsProvider))]
    [DataRow(typeof(IUnderTheEavesSettings), typeof(UnderTheEavesSettingsProvider))]
    [DataRow(typeof(IPersonalRelationSettingsProvider), typeof(PersonalRelationSettingsProvider))]
    [DataRow(typeof(IPosternGateSettingsProvider), typeof(PosternGateSettingsProvider))]
    [DataRow(typeof(ICastlePatrolSettingsProvider), typeof(CastlePatrolSettingsProvider))]
    [DataRow(typeof(IFromTheAshesSettingsProvider), typeof(FromTheAshesSettingsProvider))]
    [DataRow(typeof(IGiftSettingsProvider), typeof(GiftSettingsProvider))]
    [DataRow(typeof(IGoodDeedsSettingsProvider), typeof(GoodDeedsSettingsProvider))]
    [DataRow(typeof(ILongMemorySettingsProvider), typeof(LongMemorySettingsProvider))]
    [DataRow(typeof(IProvenderSettingsProvider), typeof(ProvenderSettingsProvider))]
    [DataRow(typeof(ReevesLedgerSettingsProvider), typeof(ReevesLedgerSettingsProvider))]
    [DataRow(typeof(LoremastersLineSettingsProvider), typeof(LoremastersLineSettingsProvider))]
    [DataRow(typeof(HearthRollSettingsProvider), typeof(HearthRollSettingsProvider))]
    [DataRow(typeof(PrisonerBannersSettingsProvider), typeof(PrisonerBannersSettingsProvider))]
    [DataRow(typeof(WardensTallySettingsProvider), typeof(WardensTallySettingsProvider))]
    [DataRow(typeof(IFireAndTributeSettings), typeof(FireAndTributeSettingsProvider))]
    [DataRow(typeof(IUnlookedForAidSettingsProvider), typeof(UnlookedForAidSettingsProvider))]
    [DataRow(typeof(ISwornAnewSettingsProvider), typeof(SwornAnewSettingsProvider))]
    [DataRow(typeof(IBondOfHireSettingsProvider), typeof(BondOfHireSettingsProvider))]

    [DataRow(typeof(ISeasonedHostsSettingsProvider), typeof(SeasonedHostsSettingsProvider))]
    [DataRow(typeof(IKingsMusterSettingsProvider), typeof(KingsMusterSettingsProvider))]
    [DataRow(typeof(TAOM.Features.NotableServices.INotableServicesSettingsProvider), typeof(TAOM.Features.NotableServices.NotableServicesSettingsProvider))]
    [DataRow(typeof(TAOM.Features.InnRounds.IInnRoundsSettingsProvider), typeof(TAOM.Features.InnRounds.InnRoundsSettingsProvider))]
    [DataRow(typeof(ISmithsMeasureSettingsProvider), typeof(SmithsMeasureSettingsProvider))]
    public void Provider_ResolvesFromARealContainer(Type service, Type implementation)
    {
        using var container = new Container();
        foreach (var parameter in implementation.GetConstructors().Single().GetParameters())
            container.RegisterInstance(parameter.ParameterType,
                Substitute.For(new[] { parameter.ParameterType }, new object[0]));
        container.Register(service, implementation, Reuse.Singleton);

        Assert.IsInstanceOfType(container.Resolve(service), implementation);
    }

    // Read THROUGH the cached object, never snapshotted: MCM edits its one registered TaomSettings in
    // place, so an edit after the provider is built and read must reach the getter. Each "before" value
    // is the compiled MCM default, so a getter wired to the wrong setting fails too.
    [TestMethod]
    public void Gifts_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new GiftSettingsProvider(mcm);
        Assert.IsTrue(sut.Get().Enabled);
        Assert.AreEqual(7, sut.Get().CooldownDays);

        mcm.GiftsEnabled = false;
        mcm.GiftCooldownDays = 21;

        Assert.IsFalse(sut.Get().Enabled);
        Assert.AreEqual(21, sut.Get().CooldownDays);
    }

    [TestMethod]
    public void GoodDeeds_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new GoodDeedsSettingsProvider(mcm);
        Assert.IsTrue(sut.Get().Enabled);
        Assert.AreEqual(GoodDeedsSettingsProvider.DefaultReleaseLordAmount, sut.Get().ReleaseLordAmount);

        mcm.GoodDeedsEnabled = false;
        mcm.GoodDeedsReleaseLordAmount = 11;

        Assert.IsFalse(sut.Get().Enabled);
        Assert.AreEqual(11, sut.Get().ReleaseLordAmount);
    }

    [TestMethod]
    public void LongMemory_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new LongMemorySettingsProvider(mcm);
        Assert.IsTrue(sut.Get().Enabled);
        Assert.AreEqual(LongMemorySettingsProvider.DefaultRaidOwnerAmount, sut.Get().RaidOwnerAmount);

        mcm.LongMemoryEnabled = false;
        mcm.LongMemoryRaidOwnerAmount = 70;

        Assert.IsFalse(sut.Get().Enabled);
        Assert.AreEqual(70, sut.Get().RaidOwnerAmount);
    }

    [TestMethod]
    public void Provender_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new ProvenderSettingsProvider(mcm);
        Assert.IsTrue(sut.Get().Enabled);
        Assert.AreEqual(3, sut.Get().WageDaysKept);

        mcm.ProvenderEnabled = false;
        mcm.ProvenderWageDaysKept = 12;

        Assert.IsFalse(sut.Get().Enabled);
        Assert.AreEqual(12, sut.Get().WageDaysKept);
    }

    [TestMethod]
    public void BondOfHire_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new BondOfHireSettingsProvider(mcm);
        Assert.AreEqual(BondOfHireSettingsProvider.DefaultMinimumDays, sut.MinimumDays);

        mcm.BondOfHireMinimumDays = 60;

        Assert.AreEqual(60, sut.MinimumDays);
    }

    [TestMethod]
    public void SeasonedHosts_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new SeasonedHostsSettingsProvider(mcm);
        Assert.AreEqual(SeasonedHostsSettingsProvider.DefaultTrainingBonus, sut.TrainingBonus);
        Assert.IsTrue(sut.LearningNeverStops);

        mcm.SeasonedHostsTrainingBonus = 0.5f;
        mcm.SeasonedHostsSkillXpMultiplier = 1.5f;
        mcm.SeasonedHostsLearningNeverStops = false;
        mcm.SeasonedHostsNoStewardXp = true;

        Assert.AreEqual(0.5f, sut.TrainingBonus);
        Assert.AreEqual(1.5f, sut.SkillXpMultiplier);
        Assert.IsFalse(sut.LearningNeverStops);
        Assert.IsTrue(sut.NoStewardXp);
    }

    [TestMethod]
    public void SmithsMeasure_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new SmithsMeasureSettingsProvider(mcm, Substitute.For<TAOM.Core.Logging.IModLogger>());
        Assert.IsTrue(sut.CapSmelting);
        Assert.AreEqual(SmithsMeasureSettingsProvider.DefaultYieldCap, sut.YieldCap);
        Assert.IsTrue(sut.MakeToolsEnabled);

        mcm.SmithsMeasureCapSmelting = false;
        mcm.SmithsMeasureYieldCap = 4;
        mcm.SmithsMeasureMakeTools = false;

        Assert.IsFalse(sut.CapSmelting);
        Assert.AreEqual(4, sut.YieldCap);
        Assert.IsFalse(sut.MakeToolsEnabled);
    }

    [TestMethod]
    public void PersonalRelations_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new PersonalRelationSettingsProvider(mcm);
        Assert.IsTrue(sut.Enabled);

        mcm.PersonalRelationsEnabled = false;

        Assert.IsFalse(sut.Enabled);
    }

    [TestMethod]
    public void CaravanTrade_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var cfg = Substitute.For<ICaravanTradeConfigProvider>();
        cfg.GetConfig().Returns(new CaravanTradeConfig());
        var sut = new CaravanTradeSettingsProvider(cfg, mcm);
        Assert.IsTrue(sut.Enabled);
        Assert.AreEqual(1.6f, sut.RangeMultiplier, 0.0001f);

        mcm.EnableCaravanTrade = false;
        mcm.CaravanRangeMultiplier = 2.5f;

        Assert.IsFalse(sut.Enabled);
        Assert.AreEqual(2.5f, sut.RangeMultiplier, 0.0001f);
    }

    [TestMethod]
    public void CastleRecruitment_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var cfg = Substitute.For<ICastleRecruitmentConfigProvider>();
        cfg.GetConfig().Returns(new CastleRecruitmentConfig());
        var sut = new CastleRecruitmentSettingsProvider(cfg, mcm);
        Assert.IsTrue(sut.IsAiEnabled);
        Assert.AreEqual(3, sut.NotablesPerCastle);

        mcm.EnableCastleRecruitmentAi = false;
        mcm.CastleNotablesPerCastle = 5;

        Assert.IsFalse(sut.IsAiEnabled);
        Assert.AreEqual(5, sut.NotablesPerCastle);
    }

    [TestMethod]
    public void AlignmentDesertion_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var cfg = Substitute.For<IAlignmentDesertionConfigProvider>();
        cfg.GetConfig().Returns(new AlignmentDesertionConfig());
        var sut = new AlignmentDesertionSettingsProvider(cfg, mcm);
        Assert.IsTrue(sut.IsEnabled);
        Assert.AreEqual(0.5f, sut.Rate, 0.0001f);

        mcm.EnableAlignmentDesertion = false;
        mcm.AlignmentDesertionRate = 0.25f;

        Assert.IsFalse(sut.IsEnabled);
        Assert.AreEqual(0.25f, sut.Rate, 0.0001f);
    }

    [TestMethod]
    public void WatchfulHolds_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new WatchfulHoldsSettingsProvider(mcm);
        Assert.IsTrue(sut.Enabled);
        Assert.IsTrue(sut.HarassAiLords);

        mcm.EnableWatchfulHolds = false;
        mcm.WatchfulHoldsHarassAiLords = false;

        Assert.IsFalse(sut.Enabled);
        Assert.IsFalse(sut.HarassAiLords);
    }

    [TestMethod]
    public void RealmBorders_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new RealmBordersSettingsProvider(Substitute.For<IModLogger>(), mcm);
        Assert.IsTrue(sut.Enabled);
        Assert.IsFalse(sut.HeraldicBands);

        mcm.EnableRealmBorders = false;
        mcm.RealmBordersHeraldicBands = true;

        Assert.IsFalse(sut.Enabled);
        Assert.IsTrue(sut.HeraldicBands);
    }

    [TestMethod]
    public void FieldCommission_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var inner = Substitute.For<IFieldCommissionConfigProvider>();
        inner.GetConfig().Returns(new FieldCommissionConfig());
        var sut = new FieldCommissionSettingsProvider(inner, mcm);
        Assert.IsTrue(sut.GetConfig().Enabled);

        mcm.EnableFieldCommission = false;

        Assert.IsFalse(sut.GetConfig().Enabled);
    }

    [TestMethod]
    public void QuickActions_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new QuickActionsSettingsProvider(mcm);
        Assert.IsTrue(sut.EnableInventorySearch);

        mcm.EnableInventorySearch = false;

        Assert.IsFalse(sut.EnableInventorySearch);
    }

    [TestMethod]
    public void SupplyLines_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new SupplyLinesSettingsProvider(mcm);
        Assert.IsTrue(sut.Enabled);
        Assert.IsTrue(sut.ShowRouteVisual);

        mcm.EnableSupplyLines = false;
        mcm.SupplyShowRouteVisual = false;
        mcm.SupplyGoodsMarkupFactor = 2f;

        Assert.IsFalse(sut.Enabled);
        Assert.IsFalse(sut.ShowRouteVisual);
        Assert.AreEqual(2f, sut.GoodsMarkupFactor);
    }

    [TestMethod]
    public void FieldCamp_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new CampSettingsProvider(mcm);
        Assert.IsTrue(sut.Enabled);
        Assert.AreEqual(4f, sut.CampSetupHours);

        mcm.EnableFieldCamps = false;
        mcm.CampSetupHours = 8f;

        Assert.IsFalse(sut.Enabled);
        Assert.AreEqual(8f, sut.CampSetupHours);
    }

    [TestMethod]
    public void FiefManagement_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new FiefManagementSettingsProvider(mcm);
        Assert.IsTrue(sut.EnableFiefManagement);
        Assert.IsTrue(sut.AllowRemoteBuildingQueue);
        Assert.IsFalse(sut.IsDebugMode);

        mcm.EnableFiefManagement = false;
        mcm.AllowRemoteBuildingQueue = false;
        mcm.FiefManagementDebug = true;

        Assert.IsFalse(sut.EnableFiefManagement);
        Assert.IsFalse(sut.AllowRemoteBuildingQueue);
        Assert.IsTrue(sut.IsDebugMode);
    }

    [TestMethod]
    public void FiefManagement_NoMcm_FallsBackToTheDefaults()
    {
        var sut = new FiefManagementSettingsProvider();

        Assert.IsTrue(sut.EnableFiefManagement);
        Assert.IsTrue(sut.AllowRemoteBuildingQueue);
        Assert.IsFalse(sut.IsDebugMode);
    }

    [TestMethod]
    public void TimeAcceleration_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new TimeAccelerationSettingsProvider(mcm);
        Assert.AreEqual(4, sut.FastForwardMultiplier);
        Assert.AreEqual(8, sut.ExtraFastForwardMultiplier);
        Assert.AreEqual(16, sut.CtrlSpaceMultiplier);

        mcm.FastForwardMultiplier = 10;
        mcm.CtrlSpaceMultiplier = 32;

        Assert.AreEqual(10, sut.FastForwardMultiplier);
        Assert.AreEqual(10, sut.ExtraFastForwardMultiplier, "extra is floored at fast");
        Assert.AreEqual(32, sut.CtrlSpaceMultiplier);
    }

    [TestMethod]
    public void NameplateCull_ReadsThroughTheCachedSettings()
    {
        var settings = new BattleLoadDiagnosticsSettings();
        var sut = new NameplateCullSettingsProvider(settings);
        Assert.IsTrue(sut.CullEnabled);

        settings.CullHiddenNameplates = false;

        Assert.IsFalse(sut.CullEnabled);
    }

    [TestMethod]
    public void HiddenHosts_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new HiddenHostsSettingsProvider(mcm);
        Assert.IsTrue(sut.Enabled);

        mcm.HiddenHostsEnabled = false;

        Assert.IsFalse(sut.Enabled);
    }

    [TestMethod]
    public void UnderTheEaves_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new UnderTheEavesSettingsProvider(mcm);
        Assert.IsTrue(sut.Enabled);

        mcm.ForestSightPenaltyEnabled = false;

        Assert.IsFalse(sut.Enabled);
    }

    [TestMethod]
    public void PosternGate_ReadsThroughTheCachedSettings()
    {
        var settings = new TaomSettings();
        var sut = new PosternGateSettingsProvider(settings);
        Assert.IsTrue(sut.CutEnabled);

        settings.EnablePosternGate = false;

        Assert.IsFalse(sut.CutEnabled);
    }

    [TestMethod]
    public void CastlePatrols_ReadsThroughTheCachedSettings()
    {
        var settings = new TaomSettings();
        var sut = new CastlePatrolSettingsProvider(settings);
        Assert.IsTrue(sut.Enabled);

        settings.EnableCastlePatrols = false;

        Assert.IsFalse(sut.Enabled);
    }

    [TestMethod]
    public void GuardHouseBuildPriority_ReadsThroughTheCachedSettings()
    {
        var settings = new TaomSettings();
        var sut = new CastlePatrolSettingsProvider(settings);
        Assert.IsTrue(sut.BuildPriorityEnabled);

        settings.EnableGuardHouseBuildPriority = false;

        Assert.IsFalse(sut.BuildPriorityEnabled);
    }

    [TestMethod]
    public void FromTheAshes_ReadsThroughTheCachedSettings()
    {
        var settings = new TaomSettings();
        var sut = new FromTheAshesSettingsProvider(settings);
        Assert.IsTrue(sut.Enabled);

        settings.EnableFromTheAshes = false;

        Assert.IsFalse(sut.Enabled);
    }

    [TestMethod]
    public void ReevesLedger_ReadsThroughTheCachedSettings()
    {
        var settings = new TaomSettings();
        var sut = new ReevesLedgerSettingsProvider(settings);
        Assert.IsTrue(sut.LedgerEnabled);
        Assert.IsTrue(sut.StepsEnabled);

        settings.EnableReevesLedger = false;
        settings.EnableReserveSteps = false;

        Assert.IsFalse(sut.LedgerEnabled);
        Assert.IsFalse(sut.StepsEnabled);
    }

    [TestMethod]
    public void LoremastersLine_ReadsThroughTheCachedSettings()
    {
        var settings = new TaomSettings();
        var sut = new LoremastersLineSettingsProvider(settings);
        Assert.IsTrue(sut.Enabled);

        settings.EnableLoremastersLine = false;

        Assert.IsFalse(sut.Enabled);
    }

    [TestMethod]
    public void HearthRoll_ReadsThroughTheCachedSettings()
    {
        var settings = new TaomSettings();
        var sut = new HearthRollSettingsProvider(settings);
        Assert.IsTrue(sut.Enabled);

        settings.EnableUnhappyCompanionTint = false;

        Assert.IsFalse(sut.Enabled);
    }

    [TestMethod]
    public void FireAndTribute_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new FireAndTributeSettingsProvider(mcm);
        Assert.IsTrue(sut.Enabled);
        Assert.IsTrue(sut.FreeHostsEnabled);
        Assert.IsTrue(sut.NeutralHostsEnabled);
        Assert.IsTrue(sut.EvilHostsEnabled);
        Assert.IsTrue(sut.EnvoysEnabled);

        mcm.EnableFireAndTribute = false;
        mcm.FireAndTributeFreeHosts = false;
        mcm.FireAndTributeNeutralHosts = false;
        mcm.FireAndTributeEvilHosts = false;
        mcm.FireAndTributeEnvoys = false;

        Assert.IsFalse(sut.Enabled);
        Assert.IsFalse(sut.FreeHostsEnabled);
        Assert.IsFalse(sut.NeutralHostsEnabled);
        Assert.IsFalse(sut.EvilHostsEnabled);
        Assert.IsFalse(sut.EnvoysEnabled);
    }

    [TestMethod]
    public void FireAndTribute_NoMcm_EverySwitchDefaultsOn()
    {
        // TaomSettings.Instance is null in the test host (MCM is never initialised), so these hold the fallbacks.
        var sut = new FireAndTributeSettingsProvider();

        Assert.IsTrue(sut.Enabled);
        Assert.IsTrue(sut.FreeHostsEnabled);
        Assert.IsTrue(sut.NeutralHostsEnabled);
        Assert.IsTrue(sut.EvilHostsEnabled);
        Assert.IsTrue(sut.EnvoysEnabled);
    }

    [TestMethod]
    public void UnlookedForAid_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new UnlookedForAidSettingsProvider(mcm);
        Assert.IsTrue(sut.Enabled);

        mcm.EnableUnlookedForAid = false;

        Assert.IsFalse(sut.Enabled);
    }

    [TestMethod]
    public void UnlookedForAid_NoMcm_DefaultsOn()
        => Assert.IsTrue(new UnlookedForAidSettingsProvider().Enabled);

    [TestMethod]
    public void Namegiving_ReadsThroughTheCachedSettings()
    {
        var settings = new TaomSettings();
        var sut = new NamegivingSettingsProvider(settings);
        Assert.IsTrue(sut.Enabled);

        settings.EnableNamegiving = false;

        Assert.IsFalse(sut.Enabled);
    }

    [TestMethod]
    public void SettlementServices_ReadsThroughTheCachedSettings()
    {
        var settings = new TaomSettings();
        var sut = new TAOM.Features.SettlementServices.SettlementServicesSettingsProvider(settings);
        Assert.IsTrue(sut.WatchSilverEnabled);
        Assert.IsTrue(sut.TidingsEnabled);

        settings.EnableWatchSilver = false;
        settings.EnableTidingsOfTheLists = false;

        Assert.IsFalse(sut.WatchSilverEnabled);
        Assert.IsFalse(sut.TidingsEnabled);
    }

    // No-MCM pins: TaomSettings.Instance is null in the test host (MCM is never initialised), so these
    // hold today's fallbacks through the caching change.
    [TestMethod]
    public void QuickActions_NoMcm_InventorySearchDefaultsOn()
        => Assert.IsTrue(new QuickActionsSettingsProvider().EnableInventorySearch);

    [TestMethod]
    public void PartyIconScale_NoMcm_GetScaleIsTheDefault()
        => Assert.AreEqual(PartyIconScaleConfig.Default, PartyIconScaleConfig.GetScale(), 0.0001f);

    [TestMethod]
    public void NameplateCull_NoMcm_CullDefaultsOn()
        => Assert.IsTrue(new NameplateCullSettingsProvider().CullEnabled);

    [TestMethod]
    public void CastleRecruitment_NoMcm_TogglesFallBackToJson()
    {
        var cfg = Substitute.For<ICastleRecruitmentConfigProvider>();
        cfg.GetConfig().Returns(new CastleRecruitmentConfig { Enabled = false, AiEnabled = false });
        var sut = new CastleRecruitmentSettingsProvider(cfg);

        Assert.IsFalse(sut.IsEnabled);
        Assert.IsFalse(sut.IsAiEnabled);
    }
}
