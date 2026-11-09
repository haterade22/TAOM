using System.Collections.Generic;
using System.IO;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Hooks;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// #1108: the gold-only option, through the real config provider, service and storage over real XML. The option
/// lives in one seam (the provider's row). The toggle-off cases prove the fixture reaches the gates the toggle removes.
/// </summary>
[TestClass]
public class SpecialResourceGoldOnlyTests
{
    private const string Hero = "hero1";
    private const string Kingdom = "gondor_k";
    private const string Culture = "gondor";
    private const string Elite = "elite_full";
    private const string NoMerchant = "elite_no_merchant";

    private string _tempDir;
    private IPathService _pathService;
    private IModLogger _logger;
    private ISpecialResourceSettingsProvider _settings;
    private SpecialResourceConfigProvider _provider;
    private SpecialResourceStorageService _storage;
    private SpecialResourceService _service;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "taom_gold_only_tests_" + System.Guid.NewGuid().ToString("N"));
        var dir = Path.Combine(_tempDir, "special_resources");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "special_resources_config.xml"), @"<?xml version=""1.0"" encoding=""utf-8""?>
<SpecialResources>
  <Resource id=""caster"" display_name=""Castar"" icon_sprite=""taom_caster_icon""
    cap=""10000"" starting_amount=""0"" daily_per_town=""0.5""
    per_battle_victory_base=""8"" per_raid=""4"" per_siege_victory=""18""
    per_prisoner=""1"" per_tournament_win=""6"" per_hideout_clear=""5"">
    <Kingdom id=""gondor_k"" />
    <Culture id=""gondor"" />
  </Resource>
</SpecialResources>");
        File.WriteAllText(Path.Combine(dir, "troop_resource_costs.xml"), @"<?xml version=""1.0"" encoding=""utf-8""?>
<TroopResourceCosts>
  <Troop id=""elite_full"" resource_id=""caster"" upgrade_cost=""5"" recruit_cost=""7"" daily_upkeep=""0.4"" merchant_cost=""18"" />
  <Troop id=""elite_no_merchant"" resource_id=""caster"" upgrade_cost=""3"" daily_upkeep=""0.2"" />
</TroopResourceCosts>");

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
        _settings = Substitute.For<ISpecialResourceSettingsProvider>();
        _provider = new SpecialResourceConfigProvider(_pathService, _logger, _settings);
        _storage = new SpecialResourceStorageService();
        _service = new SpecialResourceService(_provider, _storage, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private void GoldOn() => _settings.EliteTroopsCostGoldOnly.Returns(true);

    private void SetBalance(float amount) => _storage.Set(Hero, "caster", amount);

    private float Balance => _storage.Get(Hero, "caster");

    private static IReadOnlyList<RecruitCartEntry> Cart(int count) => new[] { new RecruitCartEntry(Elite, count) };

    private static IReadOnlyList<TroopUpkeepInfo> Party(int count) => new[] { new TroopUpkeepInfo(Elite, count) };

    // ── The row ──

    [TestMethod]
    public void GetTroopCost_GoldOn_ZeroesEveryTroopCost_AndKeepsTheMerchantPrice()
    {
        GoldOn();

        var row = _provider.GetTroopCost(Elite);

        Assert.AreEqual(0, row.UpgradeCost);
        Assert.AreEqual(0, row.RecruitCost);
        Assert.AreEqual(0f, row.DailyUpkeep);
        Assert.AreEqual(18, row.MerchantCost);
        Assert.AreEqual(Elite, row.TroopId);
        Assert.AreEqual("caster", row.ResourceId);
    }

    [TestMethod]
    public void GetTroopCost_GoldOff_ReturnsTheAuthoredRow()
    {
        var row = _provider.GetTroopCost(Elite);

        Assert.AreEqual(5, row.UpgradeCost);
        Assert.AreEqual(7, row.RecruitCost);
        Assert.AreEqual(0.4f, row.DailyUpkeep);
        Assert.AreEqual(18, row.MerchantCost);
    }

    [TestMethod]
    public void GetTroopCost_NoSettingsProvider_ReturnsTheAuthoredRow() =>
        Assert.AreEqual(5, new SpecialResourceConfigProvider(_pathService, _logger).GetTroopCost(Elite).UpgradeCost);

    [TestMethod]
    public void GetTroopCost_UnknownTroop_ReturnsNull_WithTheToggleOffAndOn()
    {
        Assert.IsNull(_provider.GetTroopCost("nobody"));
        GoldOn();
        Assert.IsNull(_provider.GetTroopCost("nobody"));
    }

    [TestMethod]
    public void GetTroopCost_ToggleFlips_FollowsItLive()
    {
        Assert.AreEqual(5, _provider.GetTroopCost(Elite).UpgradeCost);

        GoldOn();
        Assert.AreEqual(0, _provider.GetTroopCost(Elite).UpgradeCost);

        _settings.EliteTroopsCostGoldOnly.Returns(false);
        Assert.AreEqual(5, _provider.GetTroopCost(Elite).UpgradeCost);
    }

    [TestMethod]
    public void GetTroopCost_GoldOn_RepeatedReads_ReturnTheSameRow()
    {
        GoldOn();
        Assert.AreSame(_provider.GetTroopCost(Elite), _provider.GetTroopCost(Elite));
    }

    [TestMethod]
    public void Registration_GivesTheProviderTheSettings()
    {
        var container = new Container();
        SpecialResourcesIoC.RegisterSpecialResourcesFeature(container);
        Assert.IsTrue(container.IsRegistered<ISpecialResourceSettingsProvider>(),
            "SpecialResourcesIoC no longer registers the settings provider, so the optional parameter resolves to null.");
        container.RegisterInstance(_pathService);
        container.RegisterInstance(_logger);
        container.RegisterInstance(_settings, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        GoldOn();

        Assert.AreEqual(0, container.Resolve<ISpecialResourceConfigProvider>().GetTroopCost(Elite).UpgradeCost);
    }

    // ── Recruit ──

    [TestMethod]
    public void CanAffordRecruit_GoldOff_ZeroBalance_IsBlocked() =>
        Assert.IsTrue(_service.CanAffordRecruit(Hero, Kingdom, Culture, Cart(1)).Blocked);

    [TestMethod]
    public void CanAffordRecruit_GoldOn_ZeroBalance_IsAllowed()
    {
        GoldOn();
        Assert.IsFalse(_service.CanAffordRecruit(Hero, Kingdom, Culture, Cart(1)).Blocked);
    }

    [TestMethod]
    public void ChargeRecruitCost_GoldOff_Charges()
    {
        SetBalance(100f);
        Assert.AreEqual(7f, _service.ChargeRecruitCost(Hero, Kingdom, Culture, Elite, 1));
        Assert.AreEqual(93f, Balance);
    }

    [TestMethod]
    public void ChargeRecruitCost_GoldOn_ChargesNothing()
    {
        GoldOn();
        SetBalance(100f);

        Assert.AreEqual(0f, _service.ChargeRecruitCost(Hero, Kingdom, Culture, Elite, 1));
        Assert.AreEqual(100f, Balance);
    }

    // ── Upgrade ──

    [TestMethod]
    public void ClampUpgradeCount_GoldOff_ZeroBalance_ClampsToZero() =>
        Assert.AreEqual(0, _service.ClampUpgradeCount(Hero, Kingdom, Culture, Elite, 4));

    [TestMethod]
    public void ClampUpgradeCount_GoldOn_ZeroBalance_ReturnsTheRequest()
    {
        GoldOn();
        Assert.AreEqual(4, _service.ClampUpgradeCount(Hero, Kingdom, Culture, Elite, 4));
    }

    [TestMethod]
    public void CanAffordUpgrade_GoldOn_ZeroBalance_IsTrue()
    {
        GoldOn();
        Assert.IsTrue(_service.CanAffordUpgrade(Hero, Kingdom, Culture, Elite, 4));
    }

    [TestMethod]
    public void CommitSession_GoldOn_DebitsNothing()
    {
        GoldOn();
        SetBalance(100f);
        _service.BeginPartyScreenSession();
        _service.QueueUpgradeSpend(Hero, Elite, 4);

        Assert.AreEqual(0f, _service.CommitSession(Hero, Kingdom, Culture));
        Assert.AreEqual(100f, Balance);
    }

    [TestMethod]
    public void GetUpgradeCost_GoldOn_ReturnsZero()
    {
        var hook = new PartyUpgradeResourceCheckHook(_service, _provider);
        Assert.AreEqual(5, hook.GetUpgradeCost(Elite));

        GoldOn();
        Assert.AreEqual(0, hook.GetUpgradeCost(Elite));
    }

    // ── Upkeep and desertion ──

    [TestMethod]
    public void GetDailyBreakdown_GoldOff_HasUpkeepDue() =>
        Assert.IsTrue(_service.GetDailyBreakdown(Hero, Kingdom, Culture, 2, Party(10)).HasUpkeepDue);

    [TestMethod]
    public void GetDailyBreakdown_GoldOn_HasNoUpkeep_AndKeepsTheEarning()
    {
        GoldOn();

        var breakdown = _service.GetDailyBreakdown(Hero, Kingdom, Culture, 2, Party(10));

        Assert.IsFalse(breakdown.HasUpkeepDue);
        Assert.AreEqual(0, breakdown.UpkeepLines.Count);
        Assert.AreEqual(0f, breakdown.Upkeep);
        Assert.AreEqual(1f, breakdown.Earning);
    }

    [TestMethod]
    public void ApplyDailyTick_GoldOn_DoesNotDrain_AndStillEarns()
    {
        GoldOn();
        SetBalance(10f);

        _service.ApplyDailyTick(Hero, Kingdom, Culture, 2, Party(10));

        Assert.AreEqual(11f, Balance);
    }

    [TestMethod]
    public void CalculateDesertion_GoldOff_ZeroBalance_DesertsTheUpkeepTroop() =>
        Assert.AreEqual(1, _service.CalculateDesertion(Hero, Kingdom, Culture, Party(10)).Count);

    [TestMethod]
    public void CalculateDesertion_GoldOn_ZeroBalance_NobodyDeserts()
    {
        GoldOn();
        Assert.AreEqual(0, _service.CalculateDesertion(Hero, Kingdom, Culture, Party(10)).Count);
    }

    // ── What the option leaves alone ──

    [TestMethod]
    public void EarnFromBattle_GoldOn_StillEarns()
    {
        GoldOn();

        _service.EarnFromBattle(Hero, Kingdom, Culture, 1f);

        Assert.AreEqual(8f, Balance);
    }

    // ── The encyclopedia badge ──

    [TestMethod]
    public void IsShown_GoldOff_ReturnsTrue() =>
        Assert.IsTrue(SpecialResourceTroopBadge.IsShown(_provider.GetTroopCost(NoMerchant)));

    [TestMethod]
    public void IsShown_GoldOn_ReturnsFalse()
    {
        GoldOn();
        Assert.IsFalse(SpecialResourceTroopBadge.IsShown(_provider.GetTroopCost(NoMerchant)));
    }
}
