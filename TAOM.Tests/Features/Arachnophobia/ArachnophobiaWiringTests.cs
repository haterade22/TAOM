using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Arachnophobia;

/// <summary>
/// The seams that run only in the game, pinned as source text. A pin proves that the exact text is present and in order.
/// It cannot prove control flow: a new guard around a pinned call, or a changed branch exit, can still pass. The pure
/// decisions have their own tests, and tools/ingame_checks/1110.toml proves the behaviour in game.
/// </summary>
[TestClass]
public class ArachnophobiaWiringTests
{
    [TestMethod]
    public void Setting_IsOffByDefault()
    {
        var src = RepoPaths.ReadSource("Main/Features/TaomSettings.cs", stripComments: true);
        StringAssert.Contains(src, "public bool ArachnophobiaMode { get; set; } = false;");
    }

    [TestMethod]
    public void SubModule_AppliesTheModeInEveryGame_AfterTheMonsterSizes()
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        int sizes = src.IndexOf("ApplyMonsterSizes()", System.StringComparison.Ordinal);
        int mode = src.IndexOf("IArachnophobiaService>().ApplyForGame(Features.TaomSettings.Instance?.ArachnophobiaMode ?? false, game?.GameType is Campaign)",
            System.StringComparison.Ordinal);
        int oncePerProcess = src.IndexOf("if (_gameInitPatchesApplied) return;", System.StringComparison.Ordinal);

        Assert.IsTrue(sizes >= 0, "the Monster size call is gone");
        Assert.IsTrue(mode > sizes, "the call is gone or changed, or it runs before the Monster size pass");
        Assert.IsTrue(oncePerProcess > mode, "each game reloads its troops from XML, so the call runs before the once-per-process guard");
    }

    [TestMethod]
    public void IoC_RegistersTheFeature()
    {
        var src = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);
        StringAssert.Contains(src, "ArachnophobiaIoC.RegisterArachnophobiaFeature(container)");
    }

    [TestMethod]
    public void BroodTick_WithTheModeOn_DisbandsThenReturnsBeforeThePatrolOrdersAndTheSpawn()
    {
        var src = RepoPaths.ReadSource("Main/Features/CreatureBandits/CreatureBroodSpawnBehavior.cs", stripComments: true);
        int gate = src.IndexOf("if (TaomSettings.Instance?.ArachnophobiaMode ?? false)", System.StringComparison.Ordinal);
        int disband = src.IndexOf("CreatureBandParties.DisbandAll(clan)", System.StringComparison.Ordinal);
        int exit = disband < 0 ? -1 : src.IndexOf("return;", disband, System.StringComparison.Ordinal);
        int strays = src.IndexOf("CreatureBandParties.ReturnStraysToPatrol(clan)", System.StringComparison.Ordinal);

        Assert.IsTrue(gate >= 0, "the daily tick does not read the mode");
        Assert.IsTrue(disband > gate, "the daily tick does not disband broods under the mode");
        Assert.IsTrue(exit > disband && exit < strays, "the mode branch does not return before the patrol orders and the spawn");
    }

    [TestMethod]
    public void Disband_HoldsBackOnlyABroodWithAHeroPrisoner()
    {
        // RemoveParty frees every hero but the player and clears the rest (MobileParty.cs:3641-3644). A brood holding
        // regular prisoners has no exit, so a regular prisoner must not hold it back.
        var src = RepoPaths.ReadSource("Main/Features/CreatureBandits/CreatureBandParties.cs", stripComments: true);
        StringAssert.Contains(src, "|| !CreatureBanditRules.CanDisbandBrood(party.IsActive, party.MapEvent != null, party.PrisonRoster.TotalHeroes))");
    }

    [TestMethod]
    public void BroodSpawn_AsksForTheSpiderMonster()
    {
        var src = RepoPaths.ReadSource("Main/Features/CreatureBandits/Hooks/Patch93_CreatureBandits.cs", stripComments: true);
        StringAssert.Contains(src, "bool hasCreatureItem = CreatureBanditRules.IsBroodMountMonster(creature.Item?.HorseComponent?.Monster?.StringId);");
    }
}
