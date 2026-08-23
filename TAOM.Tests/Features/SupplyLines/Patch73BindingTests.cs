using System;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SupplyLines.Hooks;
using TAOM.Tests.Migration;
using TaleWorlds.CampaignSystem.Encounters;

namespace TAOM.Tests.Features.SupplyLines;

/// <summary>
/// Drift-guards for the caravan click-through guard (Patch73): the prefix binds
/// <c>PlayerEncounter.DoMeeting</c> by string through the Harmony attribute, and the category
/// string must stay what SubModule applies. Either drifting is a silent no-op in game (the
/// caravan opens vanilla's stranger conversation again); it must fail here first
/// (Patch75BindingTests shape).
/// </summary>
[TestClass]
public class Patch73BindingTests
{
    private const string ExpectedCategory = "Patch73_SupplyLines";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PlayerEncounter_DoMeeting_IsStaticParameterless()
    {
        RequireGame();
        var method = AccessTools.Method(typeof(PlayerEncounter), "DoMeeting", Type.EmptyTypes);
        Assert.IsNotNull(method,
            "PlayerEncounter.DoMeeting() did not resolve; Patch73's prefix would fail to apply and "
            + "clicking a supply caravan would strike a stranger conversation with a roster troop.");
        Assert.IsTrue(method.IsStatic, "DoMeeting is no longer static; the parameterless prefix "
            + "shape Patch73 relies on has changed.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PlayerEncounter_Finish_ResolvesWithBoolParameter()
    {
        RequireGame();
        var method = AccessTools.Method(typeof(PlayerEncounter), "Finish", new[] { typeof(bool) });
        Assert.IsNotNull(method,
            "PlayerEncounter.Finish(bool) did not resolve; the guard could suppress the meeting "
            + "but leave the encounter dangling.");
    }

    [TestMethod]
    public void Patch_CarriesThePatch73Category()
    {
        var patchType = typeof(SupplyCaravanEncounterPatch);
        var attributes = patchType.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false);
        Assert.AreEqual(1, attributes.Length, patchType.Name + " lost its HarmonyPatchCategory attribute.");
        Assert.AreEqual(ExpectedCategory, ((HarmonyPatchCategory)attributes[0]).info.category,
            patchType.Name + " drifted from the category SubModule applies; Harmony would apply "
            + "nothing and report nothing for it.");
    }
}
