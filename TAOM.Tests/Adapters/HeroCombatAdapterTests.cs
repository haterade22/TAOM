using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TAOM.Adapters;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Adapters;

/// <summary>
/// The equipment overload of <see cref="HeroCombatAdapter"/>. OOB Auto-Assign classifies a
/// candidate from the equipment its mission agent spawned with, not the hero's campaign
/// <c>BattleEquipment</c>: in a siege assault vanilla spawns every agent without a horse
/// (<c>SandBoxSiegeMissionSpawnHandler.AfterStart</c> sets <c>SetSpawnHorses(false)</c>, and
/// <c>Mission.DecideAgentSpawnEquipment</c> clears the horse slot of a clone), so a companion who
/// owns a horse fights on foot there. Reading campaign gear classified them Cavalry, which scores 0
/// on every class a siege offers (plan 022 review, 2026-09-24). <c>Equipment</c> is constructible
/// standalone and the indexer setter only calls <c>IsItemFitsToSlot</c>, whose result it ignores.
/// </summary>
[TestClass]
public class HeroCombatAdapterTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static Equipment WithHorse()
    {
        var equipment = new Equipment(Equipment.EquipmentType.Battle);
        equipment[EquipmentIndex.Horse] = new EquipmentElement(new ItemObject("test_horse"));
        return equipment;
    }

    [TestMethod]
    public void Ctor_GivenEquipment_ReadsThatEquipmentNotTheHeros()
    {
        RequireGame();

        var adapter = new HeroCombatAdapter(null, WithHorse());

        Assert.IsTrue(adapter.HasMount,
            "The adapter ignored the equipment it was given. Auto-Assign passes the agent's spawn equipment so a siege agent on foot is not classified as cavalry.");
    }

    [TestMethod]
    public void Ctor_GivenSpawnEquipmentWithoutHorse_ReportsNoMount()
    {
        RequireGame();

        var adapter = new HeroCombatAdapter(null, new Equipment(Equipment.EquipmentType.Battle));

        Assert.IsFalse(adapter.HasMount);
    }
}
