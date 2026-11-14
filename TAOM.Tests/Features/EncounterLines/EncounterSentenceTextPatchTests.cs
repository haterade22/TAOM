using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.EncounterLines;
using TAOM.Features.EncounterLines.Hooks;
using TAOM.Features.EncounterLines.Models;
using TAOM.Features.Execution;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.Messengers;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.EncounterLines;

/// <summary>
/// Runs the body of <see cref="EncounterSentenceTextPatch.Postfix"/>, the one Patch149 body that runs on every spoken sentence
/// of every conversation. A real <see cref="ConversationManager"/> shell (no campaign) carries the sentence list, the index and
/// the stored text; the service is real and its adapter is substituted. The text must change only for the three patched
/// sentence ids when the service answers true, and stay the same object in every other case.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class EncounterSentenceTextPatchTests
{
    private static bool _gameLoaded;

    private IEncounterLinesSettingsProvider _settings = null!;
    private IEncounterLinesAdapter _adapter = null!;
    private IModLogger _logger = null!;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void Setup()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        _settings = Substitute.For<IEncounterLinesSettingsProvider>();
        _settings.Enabled.Returns(true);
        _adapter = Substitute.For<IEncounterLinesAdapter>();
        _adapter.Player().Returns(new PlayerFacts("gondor", "human", new SideIds("player_faction", "gondor")));
        _adapter.ConversationParty().Returns(new PartyFacts("gondor", false, false, false));
        _adapter.TrySetVariable(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var groups = Substitute.For<IEncounterGroupsConfigProvider>();
        groups.GetMap().Returns(EncounterLinesServiceTests.Map());
        var random = Substitute.For<IRandomSource>();
        random.NextDouble().Returns(0.0);
        _logger = Substitute.For<IModLogger>();
        var service = new EncounterLinesService(_settings, _adapter, groups, Substitute.For<IAlignmentService>(), random,
            Substitute.For<IDedicatedServerProvider>(), Substitute.For<IMessengerConversationState>());
        EncounterSentenceTextPatch.Initialize(service, _logger);
    }

    private static ConversationSentence Sentence(string id)
    {
        var sentence = (ConversationSentence)FormatterServices.GetUninitializedObject(typeof(ConversationSentence));
        AccessTools.PropertySetter(typeof(ConversationSentence), "Id").Invoke(sentence, new object[] { id });
        return sentence;
    }

    private static ConversationManager Manager(int current, params string[] sentenceIds)
    {
        var manager = (ConversationManager)FormatterServices.GetUninitializedObject(typeof(ConversationManager));
        AccessTools.FieldRefAccess<ConversationManager, object>("_sentences")(manager) = sentenceIds.Select(Sentence).ToList();
        AccessTools.FieldRefAccess<ConversationManager, int>("_currentSentence")(manager) = current;
        AccessTools.FieldRefAccess<ConversationManager, TextObject>("_currentSentenceText")(manager) = new TextObject("vanilla");
        return manager;
    }

    private static TextObject StoredText(ConversationManager manager) =>
        AccessTools.FieldRefAccess<ConversationManager, TextObject>("_currentSentenceText")(manager);

    private static void AssertVanillaTextStands(ConversationManager manager)
    {
        Assert.AreEqual("vanilla", StoredText(manager).Value);
    }

    [DataTestMethod]
    [DataRow(EncounterLinesService.CaravanDefySentence)]
    [DataRow(EncounterLinesService.VillagerDefySentence)]
    public void Postfix_ForASurrenderSentenceWithALine_ReplacesTheStoredText(string sentenceId)
    {
        var manager = Manager(1, "other_sentence", sentenceId);

        EncounterSentenceTextPatch.Postfix(manager);

        Assert.AreEqual(EncounterSentenceTextPatch.SeamText, StoredText(manager).Value);
        _adapter.Received(1).TrySetVariable(EncounterLinesService.SentenceLineVariable, Arg.Any<string>(), "west_kin");
    }

    [TestMethod]
    public void Postfix_ForTheHideoutBossSentenceWithALine_ReplacesTheStoredText()
    {
        _adapter.HideoutBossClanId.Returns("harad_raiders");
        var manager = Manager(0, EncounterLinesService.HideoutBossSentence);

        EncounterSentenceTextPatch.Postfix(manager);

        Assert.AreEqual(EncounterSentenceTextPatch.SeamText, StoredText(manager).Value);
        _adapter.Received(1).TrySetVariable(EncounterLinesService.SentenceLineVariable, EncounterLinesService.HideoutBossText, Arg.Any<string>());
    }

    [TestMethod]
    public void Postfix_ForASentenceThatIsNotPatched_KeepsVanillasText_AndSetsNothing()
    {
        var manager = Manager(1, EncounterLinesService.CaravanDefySentence, "lord_start");

        EncounterSentenceTextPatch.Postfix(manager);

        AssertVanillaTextStands(manager);
        _adapter.DidNotReceiveWithAnyArgs().TrySetVariable(default!, default!, default!);
    }

    [TestMethod]
    public void Postfix_WithTheSwitchOff_KeepsVanillasText()
    {
        _settings.Enabled.Returns(false);
        var manager = Manager(0, EncounterLinesService.CaravanDefySentence);

        EncounterSentenceTextPatch.Postfix(manager);

        AssertVanillaTextStands(manager);
        _adapter.DidNotReceiveWithAnyArgs().TrySetVariable(default!, default!, default!);
    }

    [TestMethod]
    public void Postfix_WithNoConversationParty_KeepsVanillasText()
    {
        _adapter.ConversationParty().Returns((PartyFacts?)null);
        var manager = Manager(0, EncounterLinesService.CaravanDefySentence);

        EncounterSentenceTextPatch.Postfix(manager);

        AssertVanillaTextStands(manager);
        _adapter.DidNotReceiveWithAnyArgs().TrySetVariable(default!, default!, default!);
    }

    [TestMethod]
    public void Postfix_WhenTheRowIsMissing_KeepsVanillasText()
    {
        _adapter.TrySetVariable(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var manager = Manager(0, EncounterLinesService.VillagerDefySentence);

        EncounterSentenceTextPatch.Postfix(manager);

        AssertVanillaTextStands(manager);
    }

    [DataTestMethod]
    [DataRow(-1)]
    [DataRow(2)]
    [DataRow(3)]
    public void Postfix_WithTheIndexOutsideTheList_KeepsVanillasText_AndDoesNotWarn(int current)
    {
        var manager = Manager(current, EncounterLinesService.CaravanDefySentence, EncounterLinesService.VillagerDefySentence);

        EncounterSentenceTextPatch.Postfix(manager);

        AssertVanillaTextStands(manager);
        _adapter.DidNotReceiveWithAnyArgs().TrySetVariable(default!, default!, default!);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Postfix_WithANullInstance_DoesNothing()
    {
        EncounterSentenceTextPatch.Postfix(null!);

        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void Postfix_WhenTheServiceThrows_KeepsVanillasText_AndWarnsOnce()
    {
        _adapter.ConversationParty().Throws(new InvalidOperationException("boom"));
        var manager = Manager(0, EncounterLinesService.CaravanDefySentence);

        EncounterSentenceTextPatch.Postfix(manager);
        EncounterSentenceTextPatch.Postfix(manager);

        AssertVanillaTextStands(manager);
        _logger.ReceivedWithAnyArgs(1).LogWarning(default!);
    }

    [TestMethod]
    public void Postfix_CallsTheServiceSeam_InItsCompiledBody()
    {
        var postfix = typeof(EncounterSentenceTextPatch).GetMethod("Postfix")!;
        var seam = typeof(EncounterLinesService).GetMethod(nameof(EncounterLinesService.TrySetSentenceLine), BindingFlags.Instance | BindingFlags.NonPublic)!;

        var calls = PatchProcessor.GetOriginalInstructions(postfix)
            .Where(ci => (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt) && ci.operand is MethodBase called && called.MetadataToken == seam.MetadataToken && called.Module == seam.Module);

        Assert.AreEqual(1, calls.Count(), "Postfix no longer calls EncounterLinesService.TrySetSentenceLine");
    }
}
