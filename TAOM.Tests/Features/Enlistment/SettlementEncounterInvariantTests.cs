using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.Actions;
using TAOM.Adapters;
using TAOM.Features.Enlistment;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// THE INVARIANT (issue #510): TAOM never puts the main party inside a settlement without also
/// establishing the <c>PlayerEncounter</c> that makes a vanilla settlement menu safe.
///
/// <c>IMobilePartyAttachmentAdapter.MoveIntoSettlement</c> is
/// <c>EnterSettlementAction.ApplyForParty</c> and nothing more. It moves the party and creates
/// neither <c>PlayerEncounter.Current</c> nor <c>PlayerEncounter.LocationEncounter</c>. Vanilla
/// dereferences both unguarded: <c>game_menu_settlement_wait_on_init</c> opens on
/// <c>PlayerEncounter.EncounterSettlement.IsVillage</c> (backing <c>town_wait_menus</c> AND
/// <c>village_wait_menus</c>, with the castle menu's <c>town_wait</c> routing into the former),
/// and the tavern / arena / keep / town-centre-walk options all go through
/// <c>PlayerEncounter.LocationEncounter</c>.
///
/// The defect shipped twice from one root. Discharge released the player into the commander's town
/// with the raw vanilla menu (v2.0.20, the crash bundle), then shore leave deliberately handed the
/// same menu to a still-enlisted player (v2.0.21 / v2.0.22) even though
/// <c>ServiceAttachmentService.FollowCommanderIntoSettlement</c>'s own doc comment and
/// <c>docs/features/enlistment.md</c> both named the hazard. A comment did not stop it; this does.
///
/// The allow-list is deliberately tiny. Add to it only for a caller that keeps the player in a
/// TAOM-owned menu for the whole time the party is inside, and say why in the entry.
/// </summary>
[TestClass]
public class SettlementEncounterInvariantTests
{
    private static bool _gameLoaded;

    /// <summary>
    /// The ONLY methods allowed to place the party with the bare <c>EnterSettlementAction</c>.
    /// Format: "Namespace.Type.Method".
    /// </summary>
    private static readonly HashSet<string> Allowed = new HashSet<string>
    {
        // The follow path. It holds the player in the TAOM service wait menu and rolls itself back
        // out of the settlement if that menu fails to open, so it never hands over a vanilla menu
        // ITSELF.
        //
        // It is not true that no vanilla settlement menu is reachable from the state it produces:
        // shore leave is reachable from exactly here, and releasing those menus is the whole point
        // of it. The safety comes from the second chokepoint rather than this one —
        // EnlistmentPlayerActionService.TakeTownLeave calls EnsureSettlementEncounter and refuses
        // the pass when it fails, so the encounter exists before any vanilla menu is opened.
        "TAOM.Features.Enlistment.ServiceAttachmentService.FollowCommanderIntoSettlement",

        // The single sanctioned site for the raw engine call. This row is NOT "the adapter may call
        // its own interface method" — it never does. It exists because EnterSettlementAction
        // .ApplyForParty is banned too, and this is the one method allowed to make it.
        "TAOM.Adapters.MobilePartyAttachmentAdapter.MoveIntoSettlement",

        // NOT listed, deliberately: EncounterAdapter.EnsureSettlementEncounter. It reaches the
        // engine through PlayerEncounter.EnterSettlement, which calls ApplyForParty itself, so the
        // chokepoint never appears as a direct caller. The rotted-exemption check below proved that
        // by failing on a speculative row for it.
    };

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void MoveIntoSettlement_IsOnlyCalledFromPathsThatKeepThePlayerInATaomMenu()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        // THREE targets, because SameMethod compares DeclaringType and one target is evadable by
        // changing a field's declared type. A caller holding the INTERFACE emits a call declared on
        // IMobilePartyAttachmentAdapter; one holding the CONCRETE adapter emits the same call
        // declared on MobilePartyAttachmentAdapter, and a single-target ban misses it entirely.
        // The third closes the floor beneath both: going straight to the engine action.
        // Declaring-type qualification is what keeps LeaveSettlementAction.ApplyForParty out.
        var banned = new MethodBase[]
        {
            typeof(IMobilePartyAttachmentAdapter).GetMethod(nameof(IMobilePartyAttachmentAdapter.MoveIntoSettlement)),
            typeof(MobilePartyAttachmentAdapter).GetMethod(nameof(MobilePartyAttachmentAdapter.MoveIntoSettlement)),
            typeof(EnterSettlementAction).GetMethod(nameof(EnterSettlementAction.ApplyForParty)),
        };
        for (var i = 0; i < banned.Length; i++)
            Assert.IsNotNull(banned[i], $"Banned target {i} no longer resolves — retire or retarget this invariant.");

        var callers = IlCallScanner.FindCallers(
            typeof(TAOM.IoC).Assembly,
            target => banned.Any(b => IlCallScanner.SameMethod(target, b)),
            out var unreadable,
            out var scanned);

        if (scanned < 1000)
            Assert.Inconclusive($"Only {scanned} method bodies scanned (expected thousands) — assembly enumeration problem, not a genuine pass.");

        var observed = callers.Distinct().ToList();
        var violations = observed.Where(c => !Allowed.Contains(c)).ToList();
        // An allow-list row that is never observed is an exemption for something that no longer
        // happens. Left alone it rots into a comment asserting a protection nothing exercises,
        // which is how the original entry here described a call the adapter never makes.
        var unused = Allowed.Where(a => !observed.Contains(a)).ToList();

        if (violations.Count > 0 || unused.Count > 0 || unreadable.Count > 0)
        {
            var sb = new StringBuilder();
            if (violations.Count > 0)
            {
                sb.AppendLine($"{violations.Count} method(s) place the main party inside a settlement without an encounter (issue #510):");
                foreach (var v in violations) sb.AppendLine("  " + v);
                sb.AppendLine("Use IEncounterAdapter.EnsureSettlementEncounter instead, or add an allow-list entry");
                sb.AppendLine("stating how that caller keeps every vanilla settlement menu unreachable.");
            }
            if (unused.Count > 0)
            {
                sb.AppendLine($"{unused.Count} allow-list entr(ies) were never observed as a caller — the exemption has rotted, delete it:");
                foreach (var u in unused) sb.AppendLine("  " + u);
            }
            if (unreadable.Count > 0)
            {
                sb.AppendLine($"{unreadable.Count} method bodies could not be read — the invariant cannot vouch for them:");
                foreach (var u in unreadable.Take(20)) sb.AppendLine("  " + u);
            }
            Assert.Fail(sb.ToString());
        }
    }

    /// <summary>
    /// The allow-list is only meaningful while its entries exist. A rename that silently empties it
    /// would turn this suite green for the wrong reason.
    /// </summary>
    [TestMethod]
    public void AllowList_EntriesStillNameRealMethods()
    {
        var assembly = typeof(EnlistmentService).Assembly;

        foreach (var entry in Allowed)
        {
            var split = entry.LastIndexOf('.');
            var typeName = entry.Substring(0, split);
            var methodName = entry.Substring(split + 1);

            var type = assembly.GetType(typeName);
            Assert.IsNotNull(type, $"Allow-list entry '{entry}' names a type that no longer exists.");
            Assert.IsNotNull(type.GetMethod(methodName),
                $"Allow-list entry '{entry}' names a method that no longer exists.");
        }
    }
}
