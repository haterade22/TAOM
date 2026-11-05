using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Messengers;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Messengers;

[TestClass]
public class MessengerConversationStateTests
{
    [TestMethod]
    public void TheFlag_StartsClosed_OpensAndCloses()
    {
        var state = new MessengerConversationState();

        Assert.IsFalse(state.IsOpen);
        state.Open();
        Assert.IsTrue(state.IsOpen);
        state.Close();
        Assert.IsFalse(state.IsOpen);
    }

    [TestMethod]
    public void TheBehavior_OpensTheFlagBeforeTheEncounterStarts()
    {
        var src = RepoPaths.ReadSource("Main/Features/Messengers/MessengerCampaignBehavior.cs", stripComments: true);

        var open = src.IndexOf("_conversation.Open();", System.StringComparison.Ordinal);
        var start = src.IndexOf("PlayerEncounter.Start();", System.StringComparison.Ordinal);
        Assert.IsTrue(open > 0, "the conversation flag is never opened");
        Assert.IsTrue(open < start, "the flag must be open before the encounter starts");
    }

    [TestMethod]
    public void TheBehavior_ClosesTheFlagOnEveryPathThatEndsTheConversation()
    {
        var src = RepoPaths.ReadSource("Main/Features/Messengers/MessengerCampaignBehavior.cs", stripComments: true);

        var onEnd = src.IndexOf("public void OnEndMission()", System.StringComparison.Ordinal);
        Assert.IsTrue(src.IndexOf("_conversation.Close();", onEnd, System.StringComparison.Ordinal) > onEnd, "OnEndMission");
        var recovery = src.IndexOf("OpenConversationMission returned null", System.StringComparison.Ordinal);
        Assert.IsTrue(src.IndexOf("_conversation.Close();", recovery, System.StringComparison.Ordinal) > recovery, "null-mission recovery");
        var launched = src.IndexOf("void OnSessionLaunched", System.StringComparison.Ordinal);
        Assert.IsTrue(src.IndexOf("_conversation.Close();", launched, System.StringComparison.Ordinal) > launched, "session launch");
    }

    [TestMethod]
    public void TheFlag_IsRegisteredInTheFeaturesOwnIoc()
    {
        var ioc = RepoPaths.ReadSource("Main/Features/Messengers/MessengerIoC.cs", stripComments: true);

        Assert.IsTrue(ioc.Contains("IMessengerConversationState, MessengerConversationState"));
    }
}
