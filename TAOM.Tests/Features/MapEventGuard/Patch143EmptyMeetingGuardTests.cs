using System;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TaleWorlds.CampaignSystem.Encounters;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.MapEventGuard;
using TAOM.Features.MapEventGuard.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MapEventGuard;

/// <summary>Patch143 (#1099 follow-up): what the prefix does with the service's answer, and what it binds to.</summary>
[TestClass]
public class Patch143EmptyMeetingGuardTests
{
    private const string HarmonyId = "taom.tests.patch143";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private IEmptyMeetingAdapter _meeting = null!;
    private IGameMenuAdapter _menus = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _meeting = Substitute.For<IEmptyMeetingAdapter>();
        _menus = Substitute.For<IGameMenuAdapter>();
        _menus.EnsureMenuOpen(Arg.Any<string>()).Returns(true);
        _logger = Substitute.For<IModLogger>();
        Patch143_EmptyMeetingGuard.Initialize(
            new EmptyMeetingGuardService(_meeting, _menus, Substitute.For<IOutcomeLog>(), _logger), _logger);
    }

    [TestCleanup]
    public void Cleanup() => Patch143_EmptyMeetingGuard.Initialize(null, null);

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestMethod]
    public void Prefix_PartnerCanSpeak_RunsVanilla()
    {
        _meeting.FindPartnerWithNoSpeaker().Returns((string?)null);

        Assert.IsTrue(Patch143_EmptyMeetingGuard.Prefix());
    }

    [TestMethod]
    public void Prefix_NobodyToSpeak_StopsVanilla()
    {
        _meeting.FindPartnerWithNoSpeaker().Returns("party_B");

        Assert.IsFalse(Patch143_EmptyMeetingGuard.Prefix());
    }

    [TestMethod]
    public void Prefix_NotInitialized_RunsVanilla()
    {
        Patch143_EmptyMeetingGuard.Initialize(null, null);

        Assert.IsTrue(Patch143_EmptyMeetingGuard.Prefix());
    }

    [TestMethod]
    public void Prefix_ServiceThrows_RunsVanillaAndLogsTheManifestFailLine()
    {
        _meeting.FindPartnerWithNoSpeaker().Throws(new InvalidOperationException("boom"));

        Assert.IsTrue(Patch143_EmptyMeetingGuard.Prefix());

        var manifest = RepoPaths.ReadSource("tools/ingame_checks/1099.toml");
        string pattern = Regex.Match(manifest, @"^fail = \['(.+empty-meeting.+)'\]", RegexOptions.Multiline).Groups[1].Value;
        Assert.AreNotEqual(string.Empty, pattern, "1099.toml has no empty-meeting fail pattern.");
        var failPattern = new Regex(pattern);
        _logger.Received(1).LogError(Arg.Is<string>(m => failPattern.IsMatch(m)));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Prefix_PatchesTheRealTargetLastThroughHarmony()
    {
        RequireGame();

        var harmony = new Harmony(HarmonyId);
        try
        {
            harmony.CreateClassProcessor(typeof(Patch143_EmptyMeetingGuard)).Patch();

            var target = AccessTools.Method(typeof(PlayerEncounter), "DoMeeting");
            var mine = Harmony.GetPatchInfo(target)?.Prefixes.SingleOrDefault(p => p.owner == HarmonyId);
            Assert.IsNotNull(mine, "Harmony did not patch DoMeeting.");
            // Supply Lines and Refuge own their parties' meetings, so this guard must run after them.
            Assert.AreEqual(Priority.Last, mine.priority);
        }
        finally
        {
            harmony.UnpatchAll(HarmonyId);
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EngineReads_ResolveWithTheShapeTheAdapterUses()
    {
        RequireGame();

        var doMeeting = AccessTools.Method(typeof(PlayerEncounter), "DoMeeting");
        Assert.IsNotNull(doMeeting, "PlayerEncounter.DoMeeting is gone: re-derive the guard from the new meeting code.");
        Assert.IsTrue(doMeeting.IsStatic && doMeeting.GetParameters().Length == 0, "DoMeeting changed shape.");

        var helper = AccessTools.TypeByName("TaleWorlds.CampaignSystem.Conversation.ConversationHelper");
        var lookup = AccessTools.Method(helper, "GetConversationCharacterPartyLeader");
        Assert.IsNotNull(lookup, "GetConversationCharacterPartyLeader is gone: re-derive the guard from the new meeting code.");
        Assert.IsTrue(lookup.IsPublic && lookup.IsStatic, "GetConversationCharacterPartyLeader stopped being public static.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PatchClass_IsRegisteredInAllThreePlaces()
    {
        var patch = typeof(Patch143_EmptyMeetingGuard);

        Assert.AreEqual(1, patch.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length,
            "Patch143 lost its [HarmonyPatch] target attribute.");

        var categories = patch.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
            .Cast<HarmonyPatchCategory>()
            .Select(c => c.info.category)
            .ToList();
        CollectionAssert.Contains(categories, Patch143_EmptyMeetingGuard.Category);

        var subModule = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        StringAssert.Matches(subModule, new Regex(@"TryPatchCategory\([^)]*Patch143_EmptyMeetingGuard"));
        StringAssert.Contains(subModule, "IoC.Resolve<TAOM.Features.MapEventGuard.EmptyMeetingGuardService>()");
    }
}
