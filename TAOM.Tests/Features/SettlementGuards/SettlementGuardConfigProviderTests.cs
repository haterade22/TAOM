using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.SettlementGuards;

namespace TAOM.Tests.Features.SettlementGuards;

[TestClass]
public class SettlementGuardConfigProviderTests
{
    private string _tempDir;
    private IPathService _pathService;
    private IModLogger _logger;
    private SettlementGuardConfigProvider _sut;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_Tests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_tempDir, "settlement_guards"));
        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
        _sut = new SettlementGuardConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // --- Missing config file ---

    [TestMethod]
    public void GetBySettlementId_MissingFile_ReturnsNull()
    {
        var result = _sut.GetBySettlementId("town_EW1");
        Assert.IsNull(result);
    }

    // --- Settlement loading ---

    [TestMethod]
    public void GetBySettlementId_ValidConfig_ReturnsGuardPool()
    {
        WriteConfig(@"<?xml version=""1.0""?>
<SettlementGuards>
  <Settlement id=""town_EW1"">
    <Guards>
      <Guard troop=""gondor_citadel_guard"" weight=""3"" spawn_points=""sp_guard_castle,sp_guard_with_spear"" />
      <Guard troop=""gondor_fountain_guard"" weight=""2"" spawn_points=""sp_guard,sp_guard_patrol"" />
    </Guards>
    <PrisonGuard troop=""gondor_dungeon_keeper"" />
  </Settlement>
</SettlementGuards>");

        var result = _sut.GetBySettlementId("town_EW1");

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Guards.Count);
        Assert.AreEqual("gondor_citadel_guard", result.Guards[0].TroopId);
        Assert.AreEqual(3, result.Guards[0].Weight);
        Assert.AreEqual(2, result.Guards[0].SpawnPoints.Count);
        Assert.AreEqual("sp_guard_castle", result.Guards[0].SpawnPoints[0]);
        Assert.AreEqual("sp_guard_with_spear", result.Guards[0].SpawnPoints[1]);
        Assert.AreEqual("gondor_fountain_guard", result.Guards[1].TroopId);
        Assert.AreEqual(2, result.Guards[1].Weight);
        Assert.AreEqual("gondor_dungeon_keeper", result.PrisonGuardTroopId);
    }

    [TestMethod]
    public void GetBySettlementId_UnknownId_ReturnsNull()
    {
        WriteConfig(@"<?xml version=""1.0""?>
<SettlementGuards>
  <Settlement id=""town_EW1"">
    <Guards><Guard troop=""gondor_citadel_guard"" weight=""1"" /></Guards>
  </Settlement>
</SettlementGuards>");

        var result = _sut.GetBySettlementId("town_EN1");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetBySettlementId_NullId_ReturnsNull()
    {
        WriteConfig(MinimalConfig());
        Assert.IsNull(_sut.GetBySettlementId(null));
    }

    // --- Clan loading ---

    [TestMethod]
    public void GetByClanId_ValidConfig_ReturnsGuardPool()
    {
        WriteConfig(@"<?xml version=""1.0""?>
<SettlementGuards>
  <Clan id=""clan_empire_west_1"">
    <Guards>
      <Guard troop=""gondor_tower_guard"" weight=""2"" />
    </Guards>
  </Clan>
</SettlementGuards>");

        var result = _sut.GetByClanId("clan_empire_west_1");

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Guards.Count);
        Assert.AreEqual("gondor_tower_guard", result.Guards[0].TroopId);
        Assert.IsNull(result.PrisonGuardTroopId);
    }

    // --- Culture loading ---

    [TestMethod]
    public void GetByCultureId_ValidConfig_ReturnsGuardPool()
    {
        WriteConfig(@"<?xml version=""1.0""?>
<SettlementGuards>
  <Culture id=""gondor"">
    <Guards>
      <Guard troop=""guard_gondor"" weight=""1"" />
    </Guards>
  </Culture>
</SettlementGuards>");

        var result = _sut.GetByCultureId("gondor");

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Guards.Count);
        Assert.AreEqual("guard_gondor", result.Guards[0].TroopId);
    }

    // --- Spear mappings ---

    [TestMethod]
    public void GetSpearItemId_ValidMapping_ReturnsItemId()
    {
        WriteConfig(@"<?xml version=""1.0""?>
<SettlementGuards>
  <Spears>
    <Spear culture=""gondor"" item=""gondor_guard_spear"" />
    <Spear culture=""erebor"" item=""erebor_guard_pike"" />
  </Spears>
</SettlementGuards>");

        Assert.AreEqual("gondor_guard_spear", _sut.GetSpearItemId("gondor"));
        Assert.AreEqual("erebor_guard_pike", _sut.GetSpearItemId("erebor"));
    }

    [TestMethod]
    public void GetSpearItemId_UnknownCulture_ReturnsNull()
    {
        WriteConfig(@"<?xml version=""1.0""?>
<SettlementGuards>
  <Spears>
    <Spear culture=""gondor"" item=""gondor_guard_spear"" />
  </Spears>
</SettlementGuards>");

        Assert.IsNull(_sut.GetSpearItemId("mordor"));
    }

    [TestMethod]
    public void GetSpearItemId_NullCulture_ReturnsNull()
    {
        WriteConfig(MinimalConfig());
        Assert.IsNull(_sut.GetSpearItemId(null));
    }

    // --- Default weight ---

    [TestMethod]
    public void GetBySettlementId_NoWeightAttribute_DefaultsTo1()
    {
        WriteConfig(@"<?xml version=""1.0""?>
<SettlementGuards>
  <Settlement id=""town_EW1"">
    <Guards>
      <Guard troop=""gondor_citadel_guard"" />
    </Guards>
  </Settlement>
</SettlementGuards>");

        var result = _sut.GetBySettlementId("town_EW1");
        Assert.AreEqual(1, result.Guards[0].Weight);
    }

    // --- No spawn_points defaults to empty list ---

    [TestMethod]
    public void GetBySettlementId_NoSpawnPoints_DefaultsToEmptyList()
    {
        WriteConfig(@"<?xml version=""1.0""?>
<SettlementGuards>
  <Settlement id=""town_EW1"">
    <Guards>
      <Guard troop=""gondor_citadel_guard"" weight=""1"" />
    </Guards>
  </Settlement>
</SettlementGuards>");

        var result = _sut.GetBySettlementId("town_EW1");
        Assert.AreEqual(0, result.Guards[0].SpawnPoints.Count);
    }

    // --- Lazy loading (only loads once) ---

    [TestMethod]
    public void GetBySettlementId_CalledTwice_LoadsOnlyOnce()
    {
        WriteConfig(@"<?xml version=""1.0""?>
<SettlementGuards>
  <Settlement id=""town_EW1"">
    <Guards><Guard troop=""gondor_citadel_guard"" weight=""1"" /></Guards>
  </Settlement>
</SettlementGuards>");

        _sut.GetBySettlementId("town_EW1");
        _sut.GetBySettlementId("town_EW1");

        _logger.Received(1).LogInfo(Arg.Any<string>());
    }

    // --- Multiple settlement/clan/culture entries ---

    [TestMethod]
    public void LoadConfig_MultipleEntries_ParsesAll()
    {
        WriteConfig(@"<?xml version=""1.0""?>
<SettlementGuards>
  <Settlement id=""town_EW1"">
    <Guards><Guard troop=""gondor_citadel_guard"" weight=""3"" /></Guards>
  </Settlement>
  <Settlement id=""town_EN1"">
    <Guards><Guard troop=""rohan_door_warden"" weight=""2"" /></Guards>
  </Settlement>
  <Clan id=""clan_empire_west_1"">
    <Guards><Guard troop=""gondor_tower_guard"" weight=""1"" /></Guards>
  </Clan>
  <Culture id=""gondor"">
    <Guards><Guard troop=""guard_gondor"" weight=""1"" /></Guards>
  </Culture>
</SettlementGuards>");

        Assert.IsNotNull(_sut.GetBySettlementId("town_EW1"));
        Assert.IsNotNull(_sut.GetBySettlementId("town_EN1"));
        Assert.IsNotNull(_sut.GetByClanId("clan_empire_west_1"));
        Assert.IsNotNull(_sut.GetByCultureId("gondor"));
    }

    // --- GetSpearMappings ---

    [TestMethod]
    public void GetSpearMappings_ReturnsAllMappings()
    {
        WriteConfig(@"<?xml version=""1.0""?>
<SettlementGuards>
  <Spears>
    <Spear culture=""gondor"" item=""gondor_guard_spear"" />
    <Spear culture=""erebor"" item=""erebor_guard_pike"" />
  </Spears>
</SettlementGuards>");

        var mappings = _sut.GetSpearMappings();
        Assert.AreEqual(2, mappings.Count);
    }

    // --- Helpers ---

    private void WriteConfig(string content)
    {
        File.WriteAllText(
            Path.Combine(_tempDir, "settlement_guards", "settlement_guards_config.xml"),
            content);
    }

    private static string MinimalConfig()
    {
        return @"<?xml version=""1.0""?><SettlementGuards></SettlementGuards>";
    }
}
