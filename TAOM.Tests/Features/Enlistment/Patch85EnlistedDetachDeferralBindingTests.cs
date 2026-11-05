using System.IO;
using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Drift-guards for Patch85, the deferred enlisted detach (#557).
///
/// This patch exists because of an ORDERING fact, and its target was chosen for a structural reason
/// that no compiler check can protect: `PlayerEncounter.Finish` runs `FinalizeBattle()` (inside which
/// `MapEvent.FinalizeEvent` dispatches `MapEventEnded`) and `FinishEncounterInternal()` (which grants
/// the post-defeat escape only when `MainParty.AttachedTo == null`) as consecutive statements. A
/// postfix on the first lands in the one-statement window where both constraints hold.
///
/// If TaleWorlds renames or restructures either method, the postfix silently stops covering that
/// window: either it never attaches, or it attaches somewhere that no longer sits between the
/// dispatch and the escape read. Both failure modes are invisible at runtime, so they are pinned
/// here. The behaviour itself is covered by `ServiceBattleServiceTests`.
/// </summary>
[TestClass]
public class Patch85EnlistedDetachDeferralBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static System.Type PlayerEncounterType()
    {
        var type = AccessTools.TypeByName("TaleWorlds.CampaignSystem.Encounters.PlayerEncounter");
        Assert.IsNotNull(type, "PlayerEncounter did not resolve — Patch85 has no target at all.");
        return type;
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void FinalizeBattle_ResolvesAsThePatchTarget()
    {
        RequireGame();

        var method = AccessTools.Method(PlayerEncounterType(), "FinalizeBattle");
        Assert.IsNotNull(method, "PlayerEncounter.FinalizeBattle did not resolve — the postfix would never apply.");
        Assert.IsTrue(method.IsPublic, "FinalizeBattle stopped being public; nameof() in the attribute would not compile.");

        // Parameterless, so the postfix takes no arguments. An added parameter would not break the
        // by-name match, but it would mean the method's contract changed and the placement argument
        // below needs re-deriving.
        Assert.AreEqual(0, method.GetParameters().Length, "FinalizeBattle gained parameters — re-derive Patch85's placement.");

        var overloads = PlayerEncounterType().GetMethods(AccessTools.all).Count(m => m.Name == "FinalizeBattle");
        Assert.AreEqual(1, overloads,
            "FinalizeBattle gained an overload — [HarmonyPatch] by name is now ambiguous and must name the signature.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void FinishEncounterInternal_StillExists_AsTheEscapeGrantThisOrderingProtects()
    {
        RequireGame();

        // The whole reason the detach is deferred to FinalizeBattle rather than dropped entirely.
        // FinishEncounterInternal grants TeleportPartyToOutSideOfEncounterRadius +
        // SetDoNotAttackMainParty(2) only when MainParty.AttachedTo == null. If it is gone, the
        // constraint that fixed the placement is gone too, and Patch85 needs re-deriving rather
        // than quietly continuing to pass.
        var method = AccessTools.Method(PlayerEncounterType(), "FinishEncounterInternal");
        Assert.IsNotNull(method,
            "PlayerEncounter.FinishEncounterInternal is gone — the post-defeat escape this ordering preserves may have moved. Re-derive Patch85.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MapEventEndedListenerRegistration_IsStillLastInFirstOut()
    {
        RequireGame();

        // THE fact the whole fix rests on (#557). MbEvent<T>.AddNonSerializedListener head-inserts
        // into a singly-linked list and Invoke walks from the head, so the LAST listener registered
        // is the FIRST invoked. TAOM registers after SandBox, so TAOM's MapEventEnded handler runs
        // BEFORE vanilla's — which is why the detach cannot happen there.
        //
        // If TaleWorlds ever changes this to append, TAOM's handler would run LAST, the original
        // in-dispatch detach would have been harmless, and this patch becomes unnecessary rather
        // than merely redundant. Pinning the shape at least forces someone to look.
        var mbEvent = AccessTools.TypeByName("TaleWorlds.CampaignSystem.MbEvent`1");
        Assert.IsNotNull(mbEvent, "MbEvent<T> did not resolve — re-derive Patch85's ordering premise.");

        var add = AccessTools.Method(mbEvent, "AddNonSerializedListener");
        Assert.IsNotNull(add, "MbEvent<T>.AddNonSerializedListener is gone — the LIFO premise cannot be checked.");

        // The linked-list field the head-insert writes. Its presence is the observable proxy for the
        // list shape; a switch to List<T>/array would mean append semantics and must be re-read.
        var listField = AccessTools.Field(mbEvent, "_nonSerializedListenerList");
        Assert.IsNotNull(listField,
            "MbEvent<T>._nonSerializedListenerList is gone — the listener list is no longer a head-inserted linked list, so re-verify whether TAOM still runs before vanilla (#557).");
        StringAssert.Contains(listField.FieldType.Name, "EventHandlerRec",
            "MbEvent<T>'s listener list changed type — re-verify the LIFO dispatch order Patch85 depends on.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PatchClass_IsRegisteredInAllThreePlaces()
    {
        // A patch needs all three or it is dead code with no error, warning or log line.
        // Patch39_BanditPartySize shipped missing the category attribute and all five deep-review
        // agents missed it (lessons/harmony-il.md), so all three are asserted rather than assumed.
        var patch = typeof(TAOM.Features.Enlistment.Hooks.Patch85_EnlistedDetachDeferral);

        var target = patch.GetCustomAttributes(typeof(HarmonyPatch), inherit: false);
        Assert.AreEqual(1, target.Length, "Patch85 lost its [HarmonyPatch] target attribute.");

        var categories = patch.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
            .Cast<HarmonyPatchCategory>()
            .Select(c => c.info.category)
            .ToList();
        CollectionAssert.Contains(categories, "Patch85_EnlistedDetachDeferral",
            "Patch85 lost its [HarmonyPatchCategory] — SubModule's PatchCategory call would apply nothing.");

        var subModule = Path.Combine(FindRepoRoot(), "Main", "SubModule.cs");
        Assert.IsTrue(File.Exists(subModule), $"SubModule.cs not found at {subModule}");

        var source = File.ReadAllText(subModule);
        StringAssert.Contains(source, "TryPatchCategory(\"Patch85_EnlistedDetachDeferral\")",
            "SubModule.cs no longer applies Patch85_EnlistedDetachDeferral — the patch is dead code.");

        // Without Initialize the resolver delegates are null, the postfix no-ops, and the detach
        // silently never happens on any path but the reconciler's sweep.
        StringAssert.Contains(source, "Patch85_EnlistedDetachDeferral.Initialize(",
            "SubModule.cs no longer initialises Patch85 — the postfix would have no service to call.");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }
}
