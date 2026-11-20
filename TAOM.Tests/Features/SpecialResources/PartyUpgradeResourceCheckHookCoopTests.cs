using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CoopInterop;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Hooks;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// #563, the maintainer's decision A (2026-11-19): in a live co-op session Coop's prisoner path charges no
/// recruit_cost and a guest reads a stale client balance (#967), so the party-screen prisoner gate lets every
/// recruit through. Singleplayer keeps the clamp. The session is read live on each call.
/// </summary>
[TestClass]
public class PartyUpgradeResourceCheckHookCoopTests
{
    private ISpecialResourceService _service = null!;
    private ICoopSessionProvider _coop = null!;
    private PartyUpgradeResourceCheckHook _hook = null!;
    private readonly IReadOnlyList<RecruitCartEntry> _none = new List<RecruitCartEntry>();

    [TestInitialize]
    public void SetUp()
    {
        _service = Substitute.For<ISpecialResourceService>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _hook = new PartyUpgradeResourceCheckHook(_service, Substitute.For<ISpecialResourceConfigProvider>(), _coop);
        _service.ClampRecruitCount("h", "k", "c", "cave_troll", 3, _none).Returns(1);
    }

    [TestMethod]
    public void ClampRecruitCount_CoopSessionActive_LetsTheWholeRequestThrough()
    {
        _coop.IsSessionActive.Returns(true);

        Assert.AreEqual(3, _hook.ClampRecruitCount("h", "k", "c", "cave_troll", 3, _none));
        _service.DidNotReceiveWithAnyArgs().ClampRecruitCount(default!, default, default, default!, default, default!);
    }

    [TestMethod]
    public void ClampRecruitCount_NoCoopSession_ClampsThroughTheService()
    {
        _coop.IsSessionActive.Returns(false);

        Assert.AreEqual(1, _hook.ClampRecruitCount("h", "k", "c", "cave_troll", 3, _none));
    }

    [TestMethod]
    public void ClampRecruitCount_ReadsTheSessionOnEveryCall()
    {
        _coop.IsSessionActive.Returns(true, false);

        Assert.AreEqual(3, _hook.ClampRecruitCount("h", "k", "c", "cave_troll", 3, _none));
        Assert.AreEqual(1, _hook.ClampRecruitCount("h", "k", "c", "cave_troll", 3, _none));
    }
}
