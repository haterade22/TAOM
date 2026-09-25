using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Diplomacy.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Diplomacy;

/// <summary>
/// Drift-guard for Patch80. The three seams take sealed engine view models and cannot run outside a
/// live campaign, so nothing here proves behaviour — what it proves is that every engine member the
/// patch bodies touch still resolves. That matters more than usual: a member referenced inside a
/// patch body fails at JIT time, BEFORE the body's own try/catch is entered, and PatchShield then
/// swallows it at the target (lessons/harmony-il.md, "A patch's own try/catch cannot survive a
/// JIT-time member-resolution failure").
/// </summary>
[TestClass]
public class Patch80KingdomVoteDeadlockBindingTests
{
    private const string Category = "Patch80_KingdomVoteDeadlock";

    private const string DecisionsVmTypeName =
        "TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.KingdomDecisionsVM";
    private const string ItemVmTypeName =
        "TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.ItemTypes.DecisionItemBaseVM";
    private const string ElectionTypeName = "TaleWorlds.CampaignSystem.Election.KingdomElection";
    private const string DecisionTypeName = "TaleWorlds.CampaignSystem.Election.KingdomDecision";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static Type Resolve(string name)
    {
        var type = AccessTools.TypeByName(name);
        Assert.IsNotNull(type, name + " did not resolve — Patch80 would apply to nothing.");
        return type;
    }

    // ---- Patch targets -------------------------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void RefreshWith_BindingResolves_AgainstInstalledEngine()
    {
        RequireGame();

        var method = AccessTools.Method(Resolve(DecisionsVmTypeName), "RefreshWith");
        Assert.IsNotNull(method, "KingdomDecisionsVM.RefreshWith is gone — seam A would apply to nothing.");

        var parameters = method.GetParameters();
        Assert.AreEqual(1, parameters.Length, "RefreshWith's arity changed.");
        Assert.AreEqual("KingdomDecision", parameters[0].ParameterType.Name);

        // Harmony binds prefix arguments by NAME, and a rename is a legal silent engine change that
        // leaves every type-based assertion passing while the parameter arrives null.
        Assert.AreEqual(
            "decision", parameters[0].Name,
            "RefreshWith's parameter was renamed — seam A's `KingdomDecision decision` binds by name.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void HandleDecision_BindingResolves_AgainstInstalledEngine()
    {
        RequireGame();

        var method = AccessTools.Method(Resolve(DecisionsVmTypeName), "HandleDecision");
        Assert.IsNotNull(method, "KingdomDecisionsVM.HandleDecision is gone — seam C would apply to nothing.");

        var parameters = method.GetParameters();
        Assert.AreEqual(1, parameters.Length, "HandleDecision's arity changed.");
        Assert.AreEqual("KingdomDecision", parameters[0].ParameterType.Name);
        Assert.AreEqual(
            "curDecision", parameters[0].Name,
            "HandleDecision's parameter was renamed — seam C's `KingdomDecision curDecision` binds by name.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ExecuteFinalSelection_BindingResolves_AgainstInstalledEngine()
    {
        RequireGame();

        var method = AccessTools.Method(Resolve(ItemVmTypeName), "ExecuteFinalSelection");
        Assert.IsNotNull(method, "DecisionItemBaseVM.ExecuteFinalSelection is gone — seam B would apply to nothing.");
        Assert.AreEqual(0, method.GetParameters().Length, "ExecuteFinalSelection's arity changed.");
        Assert.IsTrue(method.IsPublic, "ExecuteFinalSelection is no longer public.");
    }

    // ---- Members the patch BODIES reference ------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PublicMembersReadByPatchBodies_Resolve()
    {
        RequireGame();

        var itemVm = Resolve(ItemVmTypeName);
        var election = Resolve(ElectionTypeName);

        var maker = AccessTools.PropertyGetter(itemVm, "KingdomDecisionMaker");
        Assert.IsNotNull(maker, "DecisionItemBaseVM.KingdomDecisionMaker is gone — seam B cannot see the election.");
        Assert.AreEqual(election, maker.ReturnType, "KingdomDecisionMaker no longer returns a KingdomElection.");

        var cancelled = AccessTools.PropertyGetter(election, "IsCancelled");
        Assert.IsNotNull(cancelled, "KingdomElection.IsCancelled is gone — seam B's whole condition.");
        Assert.AreEqual(typeof(bool), cancelled.ReturnType, "IsCancelled is no longer a bool.");

        var isActive = AccessTools.PropertySetter(itemVm, "IsActive");
        Assert.IsNotNull(isActive, "DecisionItemBaseVM.IsActive has no setter — seam B cannot hide the popup.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void KingdomDecisionMembersUsedByTheAdapter_Resolve()
    {
        RequireGame();

        var decision = Resolve(DecisionTypeName);

        var shouldBeCancelled = AccessTools.Method(decision, "ShouldBeCancelled");
        Assert.IsNotNull(shouldBeCancelled, "KingdomDecision.ShouldBeCancelled is gone — nothing can judge staleness.");
        Assert.AreEqual(typeof(bool), shouldBeCancelled.ReturnType);
        Assert.AreEqual(0, shouldBeCancelled.GetParameters().Length);

        var title = AccessTools.Method(decision, "GetGeneralTitle");
        Assert.IsNotNull(title, "KingdomDecision.GetGeneralTitle is gone — the lapse notice cannot name the vote.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PrivateMembersReachedByReflection_Resolve()
    {
        RequireGame();

        var decisionsVm = Resolve(DecisionsVmTypeName);
        var itemVm = Resolve(ItemVmTypeName);

        var examined = AccessTools.Field(decisionsVm, "_examinedDecisionsSinceInit");
        Assert.IsNotNull(examined, "_examinedDecisionsSinceInit is gone — the queue repair cannot record a ballot.");
        Assert.AreEqual(
            typeof(List<>), examined.FieldType.GetGenericTypeDefinition(),
            "_examinedDecisionsSinceInit is no longer a List<>.");

        var shouldCheck = AccessTools.PropertySetter(decisionsVm, "_shouldCheckForDecision");
        Assert.IsNotNull(shouldCheck, "_shouldCheckForDecision has no setter — the queue can no longer be re-armed.");

        var decisionField = AccessTools.Field(itemVm, "_decision");
        Assert.IsNotNull(decisionField, "DecisionItemBaseVM._decision is gone — seam B cannot name the lapsed ballot.");
        Assert.AreEqual("KingdomDecision", decisionField.FieldType.Name);

        var onDecisionOver = AccessTools.Field(itemVm, "_onDecisionOver");
        Assert.IsNotNull(onDecisionOver, "DecisionItemBaseVM._onDecisionOver is gone — seam B cannot close the window.");
        Assert.AreEqual(
            typeof(Action), onDecisionOver.FieldType,
            "_onDecisionOver is no longer a parameterless Action — seam B invokes it directly.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MembersSeamDReads_Resolve()
    {
        RequireGame();

        var decisionsVm = Resolve(DecisionsVmTypeName);
        var itemVm = Resolve(ItemVmTypeName);

        var current = AccessTools.PropertyGetter(decisionsVm, "CurrentDecision");
        Assert.IsNotNull(current, "KingdomDecisionsVM.CurrentDecision is gone — seam D cannot find the window RefreshWith built.");

        var over = AccessTools.PropertyGetter(itemVm, "IsKingsDecisionOver");
        Assert.IsNotNull(over, "DecisionItemBaseVM.IsKingsDecisionOver is gone — seam D cannot tell a pre-concluded election.");
        Assert.AreEqual(typeof(bool), over.ReturnType);

        var active = AccessTools.PropertyGetter(itemVm, "IsActive");
        Assert.IsNotNull(active, "DecisionItemBaseVM.IsActive has no getter.");

        // Protected instance, no parameters, no return. Seam D invokes it through the cached
        // MethodInfo; a signature change would surface as a TargetParameterCountException at
        // runtime, inside the try, which is survivable but leaves the window stuck.
        var executeDone = AccessTools.Method(itemVm, "ExecuteDone");
        Assert.IsNotNull(executeDone, "DecisionItemBaseVM.ExecuteDone is gone — seam D has nothing to close the window with.");
        Assert.IsFalse(executeDone.IsStatic, "ExecuteDone became static; the binding invokes it on the item view model.");
        Assert.AreEqual(0, executeDone.GetParameters().Length, "ExecuteDone grew parameters; the binding passes none.");
        Assert.AreEqual(typeof(void), executeDone.ReturnType);
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void ReadyToAiChoose_StillRunsInsideStartElection_WhichIsWhatSeamDCatches()
    {
        RequireGame();

        // The premise of seam D: when the player is not a supporter, StartElection() resolves the
        // election synchronously (ReadyToAiChoose -> ApplyChosenOutcome -> KingdomDecisionConcluded)
        // while DecisionItemBaseVM's constructor is still running, so the window renders already
        // decided. If StartElection stops calling ReadyToAiChoose, re-read before keeping seam D.
        var method = AccessTools.Method(Resolve(ElectionTypeName), "StartElection");
        Assert.IsNotNull(method, "KingdomElection.StartElection is gone.");

        Assert.IsTrue(
            CalledNames(method).Contains("ReadyToAiChoose"),
            "KingdomElection.StartElection no longer calls ReadyToAiChoose — the pre-concluded window " +
            "seam D closes may no longer be reachable this way.");
    }

    // ---- The engine premises the fix rests on ----------------------------------------------

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void ApplySelection_StillGatesOnIsCancelled()
    {
        RequireGame();

        // The premise of the whole feature: a cancelled election makes ApplySelection do nothing, so
        // KingdomDecisionConcluded never fires and the popup can never close itself. If this call
        // disappears, vanilla has been fixed and Patch80 should be re-evaluated rather than kept.
        //
        // Limitation, stated deliberately: call presence is not control flow. This proves the read
        // has not been deleted, not that it still guards the body.
        var method = AccessTools.Method(Resolve(ElectionTypeName), "ApplySelection");
        Assert.IsNotNull(method, "KingdomElection.ApplySelection is gone.");

        Assert.IsTrue(
            CalledNames(method).Contains("get_IsCancelled"),
            "KingdomElection.ApplySelection no longer reads IsCancelled — the deadlock Patch80 guards " +
            "may be fixed upstream. Re-read the method before assuming this patch is still needed.");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void ExecuteDone_StillReadsChosenOutcomeText_WhichIsWhySeamBDoesNotCallIt()
    {
        RequireGame();

        // Seam B closes the window through _onDecisionOver rather than ExecuteDone because
        // ExecuteDone opens with GetChosenOutcomeText(), which dereferences the election's null
        // _chosenOutcome on a cancelled election. If that stops being true, seam B could be
        // simplified to call ExecuteDone directly.
        var method = AccessTools.Method(Resolve(ItemVmTypeName), "ExecuteDone");
        Assert.IsNotNull(method, "DecisionItemBaseVM.ExecuteDone is gone.");

        Assert.IsTrue(
            CalledNames(method).Contains("GetChosenOutcomeText"),
            "ExecuteDone no longer calls GetChosenOutcomeText — re-check whether seam B can just call it.");
    }

    // ---- TAOM-side wiring ------------------------------------------------------------------

    [TestMethod]
    public void EverySeamDeclaresTheSamePatchCategory()
    {
        // A patch with no category is silent dead code: TAOM never calls Harmony.PatchAll.
        foreach (var type in new[]
                 {
                     typeof(KingdomDecisionsVM_RefreshWith_Patch),
                     typeof(KingdomDecisionsVM_HandleDecision_Patch),
                     typeof(DecisionItemBaseVM_ExecuteFinalSelection_Patch),
                     typeof(KingdomDecisionsVM_RefreshWith_AutoResolved_Patch),
                     typeof(DecisionItemBaseVM_ExecuteDone_Patch),
                 })
        {
            var attributes = type.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false);
            Assert.AreEqual(1, attributes.Length, type.Name + " has no [HarmonyPatchCategory] — it would never apply.");
            Assert.AreEqual(
                Category, ((HarmonyPatchCategory)attributes[0]).info.category,
                type.Name + " declares the wrong patch category.");
        }
    }

    [TestMethod]
    public void SubModule_AppliesThePatchCategory()
    {
        // The third of the three places a TAOM patch must be registered. The attribute above and the
        // string here are matched by text only, so a rename on either side silently orphans all three
        // seams — Harmony applies nothing for an empty category and reports nothing.
        var repoRoot = FindRepoRoot();
        var subModule = Path.Combine(repoRoot, "Main", "SubModule.cs");
        Assert.IsTrue(File.Exists(subModule), "Main/SubModule.cs not found at " + subModule);

        StringAssert.Contains(
            File.ReadAllText(subModule),
            "TryPatchCategory(\"" + Category + "\")",
            "SubModule.cs never applies " + Category + " — all three seams would be dead code.");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }

    [TestMethod]
    public void EverySeamStillConsultsTheDeadlockService()
    {
        // The one-character mutation these patches are most exposed to is losing the service call and
        // becoming a no-op, which no behavioural test can see because the bodies need a live campaign.
        AssertCalls(typeof(KingdomDecisionsVM_RefreshWith_Patch), "Prefix", "ShouldSuppressBallot");
        AssertCalls(typeof(KingdomDecisionsVM_RefreshWith_Patch), "Prefix", "WithdrawBallotFromQueue");
        AssertCalls(typeof(KingdomDecisionsVM_HandleDecision_Patch), "Postfix", "ShouldSuppressBallot");
        AssertCalls(typeof(KingdomDecisionsVM_HandleDecision_Patch), "Postfix", "WithdrawBallotFromQueue");
        AssertCalls(typeof(DecisionItemBaseVM_ExecuteFinalSelection_Patch), "Postfix", "get_IsCancelled");
        AssertCalls(typeof(DecisionItemBaseVM_ExecuteFinalSelection_Patch), "Postfix", "set_IsActive");
        AssertCalls(typeof(DecisionItemBaseVM_ExecuteFinalSelection_Patch), "Postfix", "GetOnDecisionOver");
        AssertCalls(typeof(KingdomDecisionsVM_RefreshWith_AutoResolved_Patch), "Postfix", "get_CurrentDecision");
        AssertCalls(typeof(KingdomDecisionsVM_RefreshWith_AutoResolved_Patch), "Postfix", "GetDecisionOf");
        AssertCalls(typeof(KingdomDecisionsVM_RefreshWith_AutoResolved_Patch), "Postfix", "get_IsKingsDecisionOver");
        AssertCalls(typeof(KingdomDecisionsVM_RefreshWith_AutoResolved_Patch), "Postfix", "get_IsActive");
        AssertCalls(typeof(KingdomDecisionsVM_RefreshWith_AutoResolved_Patch), "Postfix", "CloseViaExecuteDone");
        AssertCalls(typeof(DecisionItemBaseVM_ExecuteDone_Patch), "Prefix", "get_IsActive");
        AssertCalls(typeof(DecisionItemBaseVM_ExecuteDone_Patch), "Prefix", "get_IsKingsDecisionOver");
    }

    [TestMethod]
    public void SeamE_TargetsTheOnlyCloseVanillaHas()
    {
        // Seam E makes ExecuteDone single-shot and only-when-concluded, because seam D closes a
        // window BEFORE the popup widget's five-second timer fires and nothing but that timer's own
        // ExecuteFinalDone ever disarms it. A stray FinalDone then reaches whatever item is bound at
        // the time: the same closed item (a duplicate inquiry, asserted away) or the NEXT live item
        // (GetChosenOutcomeText on a null _chosenOutcome, an NRE inside OnLateUpdate). Codex review
        // 113, F1. The attribute must name the method by string: it is protected.
        var attributes = typeof(DecisionItemBaseVM_ExecuteDone_Patch)
            .GetCustomAttributes(typeof(HarmonyPatch), inherit: false)
            .Cast<HarmonyPatch>()
            .ToArray();
        Assert.AreEqual(1, attributes.Length, "seam E carries no [HarmonyPatch].");
        Assert.AreEqual("ExecuteDone", attributes[0].info.methodName, "seam E no longer targets ExecuteDone.");
        Assert.AreEqual("DecisionItemBaseVM", attributes[0].info.declaringType?.Name);
    }

    [TestMethod]
    public void SeamD_ClosesThroughVanillaExecuteDone_NotAHandRolledCopy()
    {
        // Seam D closes a window whose election concluded inside the view model's own constructor.
        // Unlike seam B's cancelled election, _chosenOutcome IS set here (ReadyToAiChoose assigned
        // it before ApplyChosenOutcome fired the concluded event), so vanilla's ExecuteDone is safe
        // and is the right thing to call: it hides the popup, shows the outcome, clears the
        // concluded listener and runs OnDecisionOver. Re-implementing any of that is how seam B
        // leaked the item view model (lessons/harmony-il.md, "Substituting for a vanilla method
        // inherits every one of its responsibilities").
        var close = typeof(KingdomVoteDeadlockBinding).GetMethod(
            "CloseViaExecuteDone", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(close, "KingdomVoteDeadlockBinding.CloseViaExecuteDone is gone.");

        Assert.IsTrue(
            CalledNames(close).Contains("Invoke"),
            "CloseViaExecuteDone no longer invokes the cached ExecuteDone — seam D has been gutted.");
    }

    [TestMethod]
    public void SubModule_InitializesSeamsDAndE()
    {
        var subModule = File.ReadAllText(Path.Combine(FindRepoRoot(), "Main", "SubModule.cs"));
        StringAssert.Contains(
            subModule,
            "KingdomDecisionsVM_RefreshWith_AutoResolved_Patch.Initialize(",
            "SubModule.cs never initializes seam D — its logger would be null and the category still applies it.");
        StringAssert.Contains(
            subModule,
            "DecisionItemBaseVM_ExecuteDone_Patch.Initialize(",
            "SubModule.cs never initializes seam E — its logger would be null and the category still applies it.");
    }

    [TestMethod]
    public void SeamB_StillClearsTheConcludedListener_OrItLeaksTheItemViewModel()
    {
        // Vanilla closes through ExecuteDone, which calls
        // CampaignEvents.KingdomDecisionConcluded.ClearListeners(this) before OnDecisionOver runs.
        // Seam B deliberately does NOT call ExecuteDone (it would NRE on a cancelled election's null
        // _chosenOutcome), so it has to replicate that clear itself. The listener list holds a strong
        // owner reference and DecisionItemBaseVM.OnFinalize does not clear it, so dropping this line
        // leaks the item view model, its DecisionOptionsList and its KingdomElection for the rest of
        // the session — invisibly, once per window the seam closes.
        //
        // Call presence only: this proves the call has not been deleted, not that it runs on every
        // path through the postfix.
        AssertCalls(typeof(DecisionItemBaseVM_ExecuteFinalSelection_Patch), "Postfix", "ClearListeners");
    }

    [TestMethod]
    public void SeamA_FaultPathWithdrawsRatherThanDeferringToVanilla()
    {
        // Vanilla is not a safe default at this call site, so the prefix's catch must withdraw the
        // ballot instead of returning true. Pinned because the tempting "fall through to vanilla is
        // always safe" convention is exactly what this seam must not do
        // (lessons/harmony-il.md, "Fall through to vanilla on error is only safe when vanilla is a
        // safe default at THAT call site").
        AssertCalls(typeof(KingdomDecisionsVM_RefreshWith_Patch), "Prefix", "WithdrawBallotFromQueue");

        var il = typeof(KingdomDecisionsVM_RefreshWith_Patch)
            .GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static)
            .GetMethodBody().GetILAsByteArray();
        Assert.IsNotNull(il, "Prefix has no readable IL body.");
    }

    private static void AssertCalls(Type patchType, string methodName, string expectedCallee)
    {
        var method = patchType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, patchType.Name + "." + methodName + " is gone.");

        Assert.IsTrue(
            CalledNames(method).Contains(expectedCallee),
            $"{patchType.Name}.{methodName} no longer calls {expectedCallee} — the seam has been gutted.");
    }

    private static HashSet<string> CalledNames(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        Assert.IsNotNull(il, method.Name + " has no readable IL body.");

        var names = new HashSet<string>(
            IlCallScanner.ExtractCalledMethods(method, il).Select(m => m.Name), StringComparer.Ordinal);

        Assert.AreNotEqual(0, names.Count, method.Name + " resolved no calls — the scan failed, not the method.");
        return names;
    }
}
