using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// #769 E10: where the troop-counter hooks are wired. Source-text pins only (comments stripped, so a
/// commented-out call cannot satisfy them), so they run without the game: the RefAsm unit step skips the
/// <c>RequiresGame</c> classes, and a wiring gate that is skipped guards nothing.
/// </summary>
[TestClass]
public class CareerTroopHooksWiringTests
{
    private static string Squash(string source) => Regex.Replace(source, @"\s+", " ");

    // A morale drain call wrapped in the counter: CareerTroopHooks.ScaleFearDrain(<stats>, <victim>, moraleModel.Calc...(<same victim>, ...
    private static readonly Regex WrappedFearSite = new Regex(
        @"CareerTroopHooks\.ScaleFearDrain\( ?[\w.]+, ?(\w+), moraleModel\.CalculateMoraleChangeToCharacter\( ?\1,",
        RegexOptions.Compiled);

    // A call (not a declaration of the model's own method, not base.) of the morale model.
    private static readonly Regex MoraleCall = new Regex(
        @"(?<!float )(?<!base\.)\bCalculateMoraleChangeToCharacter\(",
        RegexOptions.Compiled);

    private static string[] FeatureSources()
        => Directory.GetFiles(RepoPath("Main", "Features"), "*.cs", SearchOption.AllDirectories);

    [TestMethod]
    public void EveryMoraleCallUnderFeatures_IsWrappedInTheFearCounter()
    {
        // A fifth drain site in a new file would otherwise ship without the counter. The morale model itself is
        // not overridden (kill-morale GAIN uses the same method), so the counter lives at the drain sites.
        var unwrapped = new System.Collections.Generic.List<string>();
        var wrappedFiles = new System.Collections.Generic.List<string>();
        foreach (var path in FeatureSources())
        {
            var source = Squash(StripComments(File.ReadAllText(path)));
            var calls = MoraleCall.Matches(source).Count;
            if (calls == 0) continue;

            var wrapped = WrappedFearSite.Matches(source).Count;
            var name = Path.GetFileName(path);
            if (wrapped != calls) unwrapped.Add($"{name} ({calls} call(s), {wrapped} wrapped)");
            else wrappedFiles.Add(name);
        }

        Assert.AreEqual(0, unwrapped.Count,
            "A CalculateMoraleChangeToCharacter call must sit inside CareerTroopHooks.ScaleFearDrain(<stats>, <the same victim>, ...): "
            + string.Join("; ", unwrapped));
        CollectionAssert.IsSubsetOf(
            new[] { "DreadPulseRunner.cs", "SignatureStrikeRunner.cs", "RaceAbilityDeaths.cs", "RaceAbilityTicker.cs" },
            wrappedFiles,
            "the scan found fewer fear sites than the four known ones: the pattern is stale");
    }

    [TestMethod]
    public void TheMoraleModelItself_IsNotOverridden_SoKillMoraleGainIsUntouched()
    {
        // CalculateMoraleChangeToCharacter also scales the morale a soldier GAINS from kills; the counter
        // belongs at the drain sites only.
        foreach (var path in new[]
        {
            "Main/Features/CultureDoctrine/Models/TaomBattleMoraleModel.cs",
            "Main/Features/CultureDoctrine/Models/TaomCustomBattleMoraleModel.cs",
        })
        {
            Assert.IsFalse(ReadSource(path, stripComments: true).Contains("CareerTroopHooks"), path);
        }
    }

    [TestMethod]
    public void KnockdownModel_WrapsTheWholeExistingExpressionInTheCounter()
    {
        var source = Squash(ReadSource("Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs", stripComments: true));

        StringAssert.Contains(source,
            "=> CareerTroopHooks.KnockDownResistance(_agentStatService, agent, RaceAbilityHooks.KnockDownResistance(agent, base.GetKnockDownResistance(agent, strikeType)));");
    }

    [TestMethod]
    public void CrushModel_AppliesTheHoldAfterTheWholeVerdictChain()
    {
        var source = Squash(ReadSource("Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs", stripComments: true));

        StringAssert.Contains(source, "=> CareerTroopHooks.ApplyCrushHold(AgentStatService, defenderAgent, RaceAbilityHooks.CrushVerdict(attackerAgent, defenderAgent, strikeType, isPassiveUsageHit)");
        StringAssert.Contains(source, "?? _combat.CrushThrough(attackerAgent, defenderAgent, totalAttackEnergy, attackDirection, strikeType, defendItem, isPassiveUsageHit)");
        StringAssert.Contains(source, "?? base.DecideCrushedThrough(attackerAgent, defenderAgent, totalAttackEnergy, attackDirection, strikeType, defendItem, isPassiveUsageHit));");
    }

    [TestMethod]
    public void DamageModel_HandsEachSideItsOwnCharacterToTheServiceAsItsTroopClass()
    {
        // Both class arguments are a TroopClassMapper.FromCharacter(<character helper>) typed the same, so a swapped
        // helper compiles and passes every other test. The pin keeps the argument NAME, the mapper call and the
        // helper in one statement: the class the attacker's pip needs is read off the attacker, the victim's off the victim.
        var source = Squash(ReadSource("Main/Features/CareerSystem/Models/TaomAgentApplyDamageModel.cs", stripComments: true));

        StringAssert.Contains(source, "attackerTroopClass: TroopClassMapper.FromCharacter(AttackerCharacter(in attackInformation)),");
        StringAssert.Contains(source, "victimTroopClass: TroopClassMapper.FromCharacter(VictimCharacter(in attackInformation)),");
    }

    [TestMethod]
    public void DamageModel_CharacterHelpers_ReadTheRiderForAMountedSideAndTheAgentOtherwise()
    {
        // The leader id is taken off the rider for a mount hit; the class must follow, or every horse-body hit of a
        // cavalryman reads the horse's (absent) character and the cavalry pip silently never lands.
        var source = Squash(ReadSource("Main/Features/CareerSystem/Models/TaomAgentApplyDamageModel.cs", stripComments: true));

        StringAssert.Contains(source,
            "BasicCharacterObject AttackerCharacter(in AttackInformation info) => info.IsAttackerAgentMount ? info.AttackerRiderAgentCharacter : info.AttackerAgentCharacter;");
        StringAssert.Contains(source,
            "BasicCharacterObject VictimCharacter(in AttackInformation info) => info.IsVictimAgentMount ? info.VictimRiderAgentCharacter : info.VictimAgentCharacter;");
    }

    [TestMethod]
    public void TheHooks_TakeTheServiceAndNeverLocateIt()
    {
        // The service is handed in by each caller, as RefugeDamageHooks.Reduce and SignatureStrikeVerdicts.Decide
        // are given theirs: a lookup inside the hooks would run per blow and per fear drain.
        var source = ReadSource("Main/Features/CareerSystem/Hooks/CareerTroopHooks.cs", stripComments: true);

        Assert.IsFalse(source.Contains("IoC."), "CareerTroopHooks must not resolve services from the container");
        Assert.IsFalse(source.Contains("ResolveStats"), "the lookup helper is gone");
    }

    [TestMethod]
    public void TheCrushHoldRoll_UsesItsOwnLockedGenerator_NeverTheEngineOne()
    {
        // DecideCrushedThrough is reached from Mission.GetDefendCollisionResults, [MBCallback(null, true)]: it may run
        // on several threads at once. MBRandom.RandomFloat mutates Game.Current.RandomGenerator's XorShift state with
        // no lock, so two concurrent rolls corrupt the shared game stream (Codex, 2026-10-15, reproduced on v1.5.5).
        var source = ReadSource("Main/Features/CareerSystem/Hooks/CareerTroopHooks.cs", stripComments: true);

        Assert.IsFalse(source.Contains("MBRandom"), "the crush hold must not draw the engine's shared generator");
        StringAssert.Contains(Squash(source), "lock (RollGate)", "the hold's own generator is drawn under its lock");
    }

    [DataTestMethod]
    [DataRow("Main/Features/DreadAura/Hooks/DreadPulseRunner.cs", "CareerTroopHooks.ScaleFearDrain(_careerStats, target,")]
    [DataRow("Main/Features/SignatureStrikes/Hooks/SignatureStrikeRunner.cs", "CareerTroopHooks.ScaleFearDrain(_careerStats, victim,")]
    [DataRow("Main/Features/RaceAbilities/Hooks/RaceAbilityDeaths.cs", "CareerTroopHooks.ScaleFearDrain(_runtime.CareerStats, victim,")]
    [DataRow("Main/Features/RaceAbilities/Hooks/RaceAbilityTicker.cs", "CareerTroopHooks.ScaleFearDrain(_runtime.CareerStats, victim,")]
    public void EveryFearSite_PassesItsOwnInjectedService(string path, string expected)
    {
        StringAssert.Contains(Squash(ReadSource(path, stripComments: true)), expected);
    }

    [TestMethod]
    public void TheMissionLogics_ResolveTheServiceAtTheirConstructorAndHandItToTheRunner()
    {
        StringAssert.Contains(Squash(ReadSource("Main/Features/DreadAura/Hooks/DreadAuraMissionLogic.cs", stripComments: true)),
            "new DreadPulseRunner(_service, _settings, IoC.Resolve<ICareerAgentStatService>())");
        StringAssert.Contains(Squash(ReadSource("Main/Features/SignatureStrikes/Hooks/SignatureStrikesMissionLogic.cs", stripComments: true)),
            "new SignatureStrikeRunner(_service, IoC.Resolve<IDreadRegistry>(), _logger, IoC.Resolve<ICareerAgentStatService>())");
    }

    [DataTestMethod]
    [DataRow("Main/Features/CultureDoctrine/Models/TaomCustomBattleAgentStatCalculateModel.cs")]
    [DataRow("Main/Features/CombatMechanics/Models/TaomCustomBattleDamageModel.cs")]
    public void CustomBattleTwins_DoNotCarryTheCounters(string path)
    {
        // Custom Battle agent origins are not parties, so no career hero ever resolves there.
        Assert.IsFalse(ReadSource(path, stripComments: true).Contains("CareerTroopHooks"), path);
    }
}
