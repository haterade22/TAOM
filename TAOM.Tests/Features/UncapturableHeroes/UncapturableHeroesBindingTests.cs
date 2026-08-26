using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.UncapturableHeroes;

/// <summary>
/// Drift-guards for the engine surface this feature reaches.
///
/// The one that matters most is <see cref="MapEvent_StillFallsThroughToTheFugitiveAction"/>. The
/// whole design rests on a premise nothing else in the suite can see: that
/// <c>MapEvent.CaptureDefeatedPartyMembers</c> denies-then-escapes, so suppressing capture IS
/// granting escape. If TaleWorlds ever restructures that method, the veto would still apply and
/// the escape would silently stop happening, leaving a defeated hero neither captured nor free
/// with no exception, no log line, and no failing signature test.
/// </summary>
[TestClass]
public class UncapturableHeroesBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static Type HeroType => AccessTools.TypeByName("TaleWorlds.CampaignSystem.Hero");

    private static Type MapEventType => AccessTools.TypeByName("TaleWorlds.CampaignSystem.MapEvents.MapEvent");

    private static Type DeathMarkEnumType => AccessTools.TypeByName(
        "TaleWorlds.CampaignSystem.Actions.KillCharacterAction+KillCharacterActionDetail");

    // ---- Seam 1 ------------------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Hero_CanBecomePrisoner_ResolvesAsAParameterlessBoolInstanceMethod()
    {
        RequireGame();

        var method = AccessTools.Method(HeroType, "CanBecomePrisoner", Type.EmptyTypes);

        Assert.IsNotNull(method, "Hero.CanBecomePrisoner is gone. The battle capture seam is dead.");
        Assert.AreEqual(typeof(bool), method!.ReturnType);
        Assert.IsFalse(method.IsStatic, "The postfix takes __instance, which requires an instance method.");
        Assert.AreEqual(0, method.GetParameters().Length);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Hero_ExposesDeathMarkAndIsPrisoner()
    {
        RequireGame();

        var deathMark = AccessTools.Property(HeroType, "DeathMark");
        Assert.IsNotNull(deathMark, "Hero.DeathMark is gone; the stranded-hero guard cannot run.");

        // Name equality is not enough. Both guards compare DeathMark against
        // KillCharacterActionDetail.None, so if the property's TYPE changes the comparison stops
        // compiling the way it does today and the postfix fails to JIT, which under PatchShield
        // means every hero becomes uncapturable rather than an error anyone can see.
        Assert.AreSame(DeathMarkEnumType, deathMark!.PropertyType,
            "Hero.DeathMark no longer returns KillCharacterAction.KillCharacterActionDetail.");

        var isPrisoner = AccessTools.Property(HeroType, "IsPrisoner");
        Assert.IsNotNull(isPrisoner, "Hero.IsPrisoner is gone; the PrisonRoster guard cannot run.");
        Assert.AreEqual(typeof(bool), isPrisoner!.PropertyType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MapEvent_ExposesIsPlayerMapEvent()
    {
        // Player relevance for the escape message. Distinct from the static PlayerMapEvent, which
        // only means "the player is in some battle".
        RequireGame();

        var property = AccessTools.Property(MapEventType, "IsPlayerMapEvent");

        Assert.IsNotNull(property, "MapEvent.IsPlayerMapEvent is gone; escape messages lose their filter.");
        Assert.AreEqual(typeof(bool), property!.PropertyType);
    }

    // ---- Seam 2 ------------------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TakePrisonerAction_Apply_ResolvesWithThePartyAndHeroOverload()
    {
        RequireGame();

        var partyBase = AccessTools.TypeByName("TaleWorlds.CampaignSystem.Party.PartyBase");
        var method = AccessTools.Method(
            AccessTools.TypeByName("TaleWorlds.CampaignSystem.Actions.TakePrisonerAction"),
            "Apply",
            new[] { partyBase, HeroType });

        Assert.IsNotNull(method, "TakePrisonerAction.Apply(PartyBase, Hero) is gone. The direct-capture seam is dead.");
        Assert.IsTrue(method!.IsStatic);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TakePrisonerAction_Apply_KeepsTheParameterNamesTheseHooksBindTo()
    {
        // Harmony binds prefix parameters BY NAME, not by position. If TaleWorlds renames these
        // (parameter names are not part of a public API contract, so a rename is a silent,
        // perfectly legal engine change), our prefix stops receiving the party and the hero, the
        // null guard swallows every call, and the direct-capture seam becomes a no-op that no
        // behavioural test can see. Resolving by parameter TYPE, as the test above does, would
        // still pass. This is the only assertion that catches it.
        RequireGame();

        var partyBase = AccessTools.TypeByName("TaleWorlds.CampaignSystem.Party.PartyBase");
        var method = AccessTools.Method(
            AccessTools.TypeByName("TaleWorlds.CampaignSystem.Actions.TakePrisonerAction"),
            "Apply",
            new[] { partyBase, HeroType });

        Assert.IsNotNull(method);

        CollectionAssert.AreEqual(
            new[] { "capturerParty", "prisonerCharacter" },
            method!.GetParameters().Select(p => p.Name).ToArray(),
            "TakePrisonerAction.Apply's parameter names changed. TakePrisonerAction_Apply_Patch.Prefix "
            + "declares (PartyBase capturerParty, Hero prisonerCharacter) and Harmony binds those by "
            + "name; rename the prefix's parameters to match or the seam silently stops working.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void KillCharacterActionDetail_StillDeclaresNone()
    {
        // The one engine reference in the postfix body that no other binding assertion covers.
        // It matters more than it looks: a Missing*/TypeLoad thrown while JITting our own Postfix
        // is raised BEFORE the method's try/catch is entered, so the patch cannot self-guard. It
        // reaches PatchShield's finalizer instead, which swallows it and lets the patched method
        // return default(bool) = false, i.e. EVERY hero in the game becomes uncapturable. Worse,
        // PatchShieldPolicy.CompiledProtectedOwnerPrefixes contains "TAOM", so PatchShield refuses
        // to unpatch us and the state persists for the whole session. This assertion is what turns
        // that into a red test after an engine bump instead of a shipped campaign-wide bug.
        RequireGame();

        var enumType = DeathMarkEnumType;

        Assert.IsNotNull(enumType, "KillCharacterAction.KillCharacterActionDetail is gone.");
        Assert.IsTrue(enumType!.IsEnum);
        CollectionAssert.Contains(
            System.Enum.GetNames(enumType),
            "None",
            "KillCharacterActionDetail.None is gone. Hero_CanBecomePrisoner_Patch's stranded-hero "
            + "guard references it directly and would fail to JIT.");

        // The numeric value matters as much as the name. C# folds an enum comparison to the
        // constant at COMPILE time, so if None stopped being 0 our already-compiled guards would
        // silently be comparing against a stale literal and every death-marked hero would take the
        // wrong branch. Nothing else in the suite can see that.
        Assert.AreEqual(0, Convert.ToInt32(Enum.Parse(enumType, "None")),
            "KillCharacterActionDetail.None is no longer 0. Both DeathMark guards compare against a "
            + "compile-time-folded constant and are now wrong; rebuild and re-verify them.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MakeHeroFugitiveAction_Apply_ResolvesWithTheNotificationFlag()
    {
        RequireGame();

        var method = AccessTools.Method(
            AccessTools.TypeByName("TaleWorlds.CampaignSystem.Actions.MakeHeroFugitiveAction"),
            "Apply",
            new[] { HeroType, typeof(bool) });

        Assert.IsNotNull(method, "MakeHeroFugitiveAction.Apply(Hero, bool) is gone; the adapter cannot free anyone.");
        Assert.IsTrue(method!.IsStatic);
    }

    // ---- The premise the whole design rests on -----------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MapEvent_StillFallsThroughToTheFugitiveAction()
    {
        // Verified by reading MapEvent.cs at v1.4.8: :1983 gates capture on CanBecomePrisoner, and
        // :2004-2008 applies MakeHeroFugitiveAction to any hero still in the roster afterwards.
        // Both calls must remain in that ONE method, or "deny the capture" stops meaning "grant
        // the escape".
        RequireGame();

        var capture = AccessTools.Method(MapEventType, "CaptureDefeatedPartyMembers");
        Assert.IsNotNull(capture,
            "MapEvent.CaptureDefeatedPartyMembers is gone. Re-derive the whole capture pipeline "
            + "before trusting either seam.");

        var il = capture!.GetMethodBody()?.GetILAsByteArray();
        Assert.IsNotNull(il, "Could not read the IL of CaptureDefeatedPartyMembers.");

        var called = IlCallScanner.ExtractCalledMethods(capture, il!).ToList();

        var gateAt = called.FindIndex(m => m.Name == "CanBecomePrisoner" && m.DeclaringType == HeroType);
        var captureAt = called.FindIndex(m => m.Name == "Apply" && m.DeclaringType?.Name == "TakePrisonerAction");
        var escapeAt = called.FindIndex(m => m.Name == "Apply" && m.DeclaringType?.Name == "MakeHeroFugitiveAction");

        Assert.IsTrue(gateAt >= 0,
            "CaptureDefeatedPartyMembers no longer calls Hero.CanBecomePrisoner. The battle seam "
            + "still applies but the engine no longer consults it: protected heroes are capturable again.");

        Assert.IsTrue(captureAt >= 0,
            "CaptureDefeatedPartyMembers no longer calls TakePrisonerAction.Apply, so the capture "
            + "branch this feature vetoes has moved. Re-read the method.");

        Assert.IsTrue(escapeAt >= 0,
            "CaptureDefeatedPartyMembers no longer calls MakeHeroFugitiveAction.Apply. Denying "
            + "capture no longer produces an escape, so a protected hero would end a battle "
            + "neither captured nor free, silently. Re-read MapEvent.cs before shipping.");

        Assert.IsTrue(escapeAt > gateAt && escapeAt > captureAt,
            "The fugitive fall-through no longer comes after the capture gate in IL order. The "
            + "feature depends on escape being downstream of the veto, not inside the capture branch.");
    }

    // WHAT THIS TEST CANNOT SEE, stated plainly so nobody trusts it further than it goes.
    // It proves the three calls still exist in that method in that order. It does NOT prove the
    // control-flow relationship the feature actually depends on: that the false branch of the gate
    // leaves the roster entry intact and reaches the fall-through. An engine refactor that kept all
    // three calls but removed the hero from the roster on the false branch, or moved the fugitive
    // call inside the successful-capture branch, would still pass. Verifying that needs real IL
    // branch analysis or a live-campaign integration test; neither exists here yet. Treat a green
    // result as "the premise has not obviously been deleted", and re-read MapEvent.cs by hand on
    // any engine bump. Recorded as a known limitation in docs/features/uncapturable-heroes.md.

    // ---- Our own hooks ------------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BothHooks_DeclareTheExactPatchCategorySubModuleApplies()
    {
        // Asserting only that the two categories are EQUAL would pass if both were wrong in the
        // same way, while the separate wiring test that greps SubModule.cs for the correct literal
        // would also still pass. Pin the literal here, on the types themselves.
        const string Expected = "Patch76_UncapturableHeroes";

        foreach (var type in new[]
                 {
                     typeof(TAOM.Features.UncapturableHeroes.Hooks.Hero_CanBecomePrisoner_Patch),
                     typeof(TAOM.Features.UncapturableHeroes.Hooks.TakePrisonerAction_Apply_Patch),
                 })
        {
            var attribute = type.GetCustomAttribute<HarmonyPatchCategory>();

            Assert.IsNotNull(attribute,
                $"{type.Name} lost its [HarmonyPatchCategory] and will never be applied.");
            Assert.AreEqual(Expected, attribute!.info.category,
                $"{type.Name} declares a category SubModule.cs does not apply, so it is dead code.");
        }
    }

    // ---- Mutation guards: these hooks and the adapter have no behavioural tests -------------
    // A Hero cannot be constructed without a live Campaign, so the patch bodies and the adapter
    // cannot be exercised directly. That leaves specific mutations invisible to the whole suite:
    // deleting the engine call from the adapter while still returning true, or gutting a hook into
    // a no-op. These IL assertions close that gap at the level it can be closed at.

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void HeroCaptivityAdapter_ActuallyCallsTheFugitiveAction()
    {
        RequireGame();

        AssertCalls(
            typeof(TAOM.Adapters.HeroCaptivityAdapter), "MakeFugitive",
            "MakeHeroFugitiveAction", "Apply",
            "HeroCaptivityAdapter.MakeFugitive no longer calls MakeHeroFugitiveAction.Apply. It "
            + "would still return true, the service would report a prevented capture, the prefix "
            + "would skip vanilla, and the hero would simply stay put. Every existing test passes.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BothHooks_ActuallyDelegateToTheService()
    {
        RequireGame();

        AssertCalls(
            typeof(TAOM.Features.UncapturableHeroes.Hooks.Hero_CanBecomePrisoner_Patch), "Postfix",
            "IUncapturableHeroService", "ShouldDenyCapture",
            "The battle postfix no longer asks the service whether to deny. It is a no-op.");

        AssertCalls(
            typeof(TAOM.Features.UncapturableHeroes.Hooks.TakePrisonerAction_Apply_Patch), "Prefix",
            "IUncapturableHeroService", "TryPreventCapture",
            "The direct-capture prefix no longer asks the service. It is a no-op.");
    }

    private static void AssertCalls(
        Type owner, string method, string calleeType, string calleeMethod, string message)
    {
        var target = AccessTools.Method(owner, method);
        Assert.IsNotNull(target, $"{owner.Name}.{method} is gone.");

        var il = target!.GetMethodBody()?.GetILAsByteArray();
        Assert.IsNotNull(il, $"Could not read the IL of {owner.Name}.{method}.");

        Assert.IsTrue(
            IlCallScanner.ExtractCalledMethods(target, il!)
                .Any(m => m.Name == calleeMethod && m.DeclaringType?.Name == calleeType),
            message);
    }
}
