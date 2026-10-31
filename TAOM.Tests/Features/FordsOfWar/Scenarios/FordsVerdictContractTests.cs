using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.FordsOfWar.Scenarios;

/// <summary>
/// One row per (check, part) that the rules emit: the scenario that makes a working producer PASS it, and the fault that makes a
/// broken producer FAIL it, or <c>PassOnly</c> for a part with no FAIL branch (it can pass or stay unseen, never fail).
/// A row names scenarios from <see cref="FordsScenarios"/>, and each is run: a claim that no scenario backs fails the test.
/// Check 11 "hits" is <c>NotDrivable</c>: <c>FordsVolleyHits.Read</c> takes the engine's blow structs, so no scenario runs it, and the
/// scenario towers hand the report hits whose ratio is 1.0 by construction. That row claims no scenario backing.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FordsVerdictContractTests
{
    private const string PassOnly = "PassOnly";

    /// <summary>No scenario drives this part: <c>FordsVolleyHits.Read</c> takes engine structs, and the scenario towers synthesize hits whose ratio is 1.0 by construction.</summary>
    private const string NotDrivable = "NotDrivable";

    private sealed record Contract(int Check, string Part, string PassScenario, string FailFault);

    private static readonly Contract[] Table =
    {
        new(1, "installed", "S1", "menu-no-content"),
        new(1, "polled", "S1", "menu-no-content"),
        new(1, "flow", "S1", "flow-fault"),
        new(1, "cancel", "S1", PassOnly),
        new(2, "named", "S5-console", "route-c-console"),
        new(2, "unknown", "S5-console", PassOnly),
        new(2, "defaults", "S5-console", "route-c-console"),
        new(3, "started", "S1", "route-c"),
        new(3, "ids", "S1", "missing-id"),
        new(3, "spawn", "S1", "spawn-null"),
        new(4, "countdown", "S1", PassOnly),
        new(4, "towers", "S1", "tower-dropped"),
        new(4, "waves", "S1", "spawn-null"),
        new(4, "march", "S1", "frozen"),
        new(4, "fight", "S1", PassOnly),
        new(4, "killGold", "S1", "killer-unknown"),
        new(4, "pan", "S1", "camera-dead"),
        new(4, "rotate", "S1", "camera-dead"),
        new(4, "zoom", "S1", "camera-dead"),
        new(4, "inside", "S4", "camera-unclamped"),
        new(4, "ownership", "S1", "camera-failed"),
        new(5, "photo", "S1", "photo-unasked"),
        new(5, "options", "S1", "options-missing"),
        new(6, "grant", "S1", "gold-refused"),
        new(6, "buy", "S1", PassOnly),
        new(6, "feed", "S1", "feed-silent"),
        new(7, "field", "S1", "hero-spawn-null"),
        new(7, "died", "S1", PassOnly),
        new(7, "respawn", "S1", "hero-respawn-lost"),
        new(8, "orders", "S1", PassOnly),
        new(8, "hold", "S1", "hold-in-fire"),
        new(9, "lock", "S1", "hp-drift"),
        new(9, "melee", "S1", "towers-dead"),
        new(10, "ownSide", "S1", "hp-drift"),
        new(11, "item", "S1", "item-missing"),
        new(11, "hits", NotDrivable, NotDrivable),
        new(12, "started", "S1", PassOnly),
        new(12, "restored", "S1", "warg-stranded"),
        new(12, "returned", "S1", "warg-not-returned"),
        new(13, "capture", "S1", PassOnly),
        new(13, "contested", "S1", PassOnly),
        new(14, "keep", "S1", PassOnly),
        new(14, "result", "S1", "result-wrong"),
        new(14, "exit", "S1", "exit-slow"),
        new(14, "menu", "S1", PassOnly),
        new(15, "failure", "S3-exit", PassOnly),
        new(15, "exit", "S3-exit", "route-a-slow-close"),
        new(15, "notice", "S3-exit", "notice-unacknowledged"),
        new(15, "menu", "S3-exit", PassOnly),
        new(16, "cap", "S1", "over-cap"),
        new(16, "agents", "S1", "agents-over-limit"),
        new(16, "duration", "S1", PassOnly),
    };

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void ResetThreadGuard() => MissionThreadGuard.ResetForTests();

    [TestMethod]
    public void TheTablesKeys_AreExactlyThePartsTheRulesEmit()
    {
        var emitted = FordsVerdicts.Judge(new FordsSessionEvidence(), new FordsRunEvidence())
            .SelectMany(r => r.Parts.Select(p => r.Check + "." + p.Name)).ToList();
        var keys = Table.Select(c => c.Check + "." + c.Part).ToList();

        CollectionAssert.AreEquivalent(emitted, keys, "a part with no row, or a row for a part the rules no longer emit");
        Assert.AreEqual(keys.Count, keys.Distinct().Count(), "a part listed twice");
    }

    [TestMethod]
    public void EveryPassScenario_MakesItsPartPass()
    {
        var wrong = new List<string>();
        foreach (var c in Table.Where(c => c.PassScenario != NotDrivable))
        {
            var sc = FordsScenarios.Get(c.PassScenario);
            var result = sc.Result(c.Check, c.Part);
            if (result != FordsResult.Pass)
                wrong.Add(c.Check + "." + c.Part + " in " + c.PassScenario + " is " + result + " (" + sc.Numbers(c.Check, c.Part) + ")");
        }

        Assert.AreEqual(0, wrong.Count, string.Join("\n", wrong));
    }

    [TestMethod]
    public void EveryFault_MakesItsPartFail()
    {
        var wrong = new List<string>();
        foreach (var c in Table.Where(c => c.FailFault != PassOnly && c.FailFault != NotDrivable))
        {
            var sc = FordsScenarios.Get(c.FailFault);
            var result = sc.Result(c.Check, c.Part);
            if (result != FordsResult.Fail)
                wrong.Add(c.Check + "." + c.Part + " under " + c.FailFault + " is " + result + " (" + sc.Numbers(c.Check, c.Part) + ")");
        }

        Assert.AreEqual(0, wrong.Count, string.Join("\n", wrong));
    }

    [TestMethod]
    public void APassOnlyPart_IsNeverFailedByAnyScenarioOrFault()
    {
        var names = Table.SelectMany(c => new[] { c.PassScenario, c.FailFault }).Where(n => n != PassOnly && n != NotDrivable).Distinct().ToList();
        var failed = new List<string>();
        foreach (var c in Table.Where(c => c.FailFault == PassOnly))
        {
            foreach (var name in names)
            {
                if (FordsScenarios.Get(name).Result(c.Check, c.Part) == FordsResult.Fail)
                    failed.Add(c.Check + "." + c.Part + " failed in " + name);
            }
        }

        Assert.AreEqual(0, failed.Count, "a part marked PassOnly has a FAIL branch: " + string.Join("; ", failed));
    }
}
