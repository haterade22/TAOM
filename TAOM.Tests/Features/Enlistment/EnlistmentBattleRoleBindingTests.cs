using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Drift-guards for the #424 correction's engine bindings.
/// <c>EnlistmentBattleRoleMissionBehavior</c> calls <c>Team.SetPlayerRole(bool, bool)</c> and
/// reads <c>MapEvent.GetLeaderParty</c>/<c>PlayerSide</c>; a signature change would make the
/// role correction silently no-op and re-expose the private-commands-the-army defect. These
/// tests redden offline instead.
/// </summary>
[TestClass]
public class EnlistmentBattleRoleBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Team_SetPlayerRole_SignatureMatchesInstalledEngine()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var team = AccessTools.TypeByName("TaleWorlds.MountAndBlade.Team");
        Assert.IsNotNull(team, "TaleWorlds.MountAndBlade.Team not found in the installed engine.");

        var method = AccessTools.Method(team, "SetPlayerRole", new[] { typeof(bool), typeof(bool) });
        Assert.IsNotNull(method,
            "Team.SetPlayerRole(bool, bool) missing — the #424 role correction would silently no-op.");
        Assert.IsFalse(method.IsStatic, "Team.SetPlayerRole is expected to be an instance method.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MapEvent_LeaderPartyAndPlayerSide_ResolveAgainstInstalledEngine()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var mapEvent = AccessTools.TypeByName("TaleWorlds.CampaignSystem.MapEvents.MapEvent");
        Assert.IsNotNull(mapEvent, "MapEvent not found in the installed engine.");

        Assert.IsNotNull(AccessTools.Method(mapEvent, "GetLeaderParty"),
            "MapEvent.GetLeaderParty missing — the leads-the-side check would not compile against this engine.");
        Assert.IsNotNull(AccessTools.Property(mapEvent, "PlayerSide"),
            "MapEvent.PlayerSide missing — the leads-the-side check would not compile against this engine.");
    }
}
