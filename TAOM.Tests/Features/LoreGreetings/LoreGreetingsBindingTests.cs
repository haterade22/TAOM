using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Features.LoreGreetings;
using TAOM.Features.LoreGreetings.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.LoreGreetings;

/// <summary>
/// Pins the two vanilla seams Lore Greetings rides (Patch177) against the installed engine, with no campaign: both targets keep
/// their shape, the met greeting still writes <c>VOICED_LINE</c> through <c>conversations_set_voiced_line</c>, the meeting helper is
/// too large to inline and still calls <c>Hero.SetHasMet</c>, <c>SetHasMet</c> still stamps the meeting time the prefix restores,
/// and the category attaches exactly one postfix and one prefix the JIT accepts.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class LoreGreetingsBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static MethodInfo Method(System.Type type, string name)
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var method = AccessTools.Method(type, name);
        Assert.IsNotNull(method, type.Name + "." + name + " did not resolve.");
        return method!;
    }

    private static MethodInfo GreetingTarget() => Method(typeof(LordConversationsCampaignBehavior), Patch177_LoreGreeting.TargetName);
    private static MethodInfo MeetingTarget() => Method(typeof(LordConversationsCampaignBehavior), Patch177_LoreMeeting.TargetName);

    private static bool Calls(MethodBase method, string name) =>
        PatchProcessor.GetOriginalInstructions(method).Any(ci => (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt)
            && ci.operand is MethodInfo m && m.Name == name);

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BothTargets_ArePrivateInstanceBools_WithNoParameters_InInstalledEngine()
    {
        foreach (var target in new[] { GreetingTarget(), MeetingTarget() })
        {
            Assert.IsFalse(target.IsStatic, target.Name);
            Assert.IsTrue(target.IsPrivate, target.Name);
            Assert.AreEqual(typeof(bool), target.ReturnType, target.Name);
            Assert.AreEqual(0, target.GetParameters().Length, target.Name);
        }
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheGreeting_StillSetsVoicedLine_ThroughSetVoicedLine_InInstalledEngine()
    {
        Assert.IsTrue(Calls(GreetingTarget(), "conversations_set_voiced_line"), "the greeting no longer calls conversations_set_voiced_line");
        var setter = Method(typeof(LordConversationsCampaignBehavior), "conversations_set_voiced_line");
        var strings = PatchProcessor.GetOriginalInstructions(setter).Where(ci => ci.opcode == OpCodes.Ldstr).Select(ci => (string)ci.operand).ToList();
        CollectionAssert.Contains(strings, LoreGreetingsAdapter.GreetingVariable);
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheMeetingHelper_StillMarksMet_AndIsTooLargeForTheClrToInline()
    {
        var target = MeetingTarget();
        Assert.IsTrue(Calls(target, nameof(Hero.SetHasMet)), "ConversationUseMeetingDialogs no longer calls Hero.SetHasMet");

        var body = target.GetMethodBody()!;
        var il = body.GetILAsByteArray()!.Length;
        Assert.IsTrue(body.ExceptionHandlingClauses.Count > 0 || il > 100,
            "IL size " + il + " could be inlined into one of its four callers, which would skip the prefix");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void SetHasMet_StillStampsTheMeetingTime_TheAdapterPutsBack()
    {
        var setHasMet = Method(typeof(Hero), nameof(Hero.SetHasMet));

        Assert.IsTrue(Calls(setHasMet, "set_" + nameof(Hero.LastMeetingTimeWithPlayer)), "SetHasMet no longer writes LastMeetingTimeWithPlayer");
        Assert.IsNotNull(typeof(Hero).GetProperty(nameof(Hero.LastMeetingTimeWithPlayer))!.GetSetMethod(), "the meeting time has no public setter");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ThePatchMethods_DeclareTheNamesHarmonyInjectsBy()
    {
        CollectionAssert.AreEqual(new[] { "__result" },
            typeof(Patch177_LoreGreeting).GetMethod("Postfix")!.GetParameters().Select(p => p.Name).ToArray());
        Assert.AreEqual(0, typeof(Patch177_LoreMeeting).GetMethod("Prefix")!.GetParameters().Length);
        Assert.AreEqual(typeof(void), typeof(Patch177_LoreMeeting).GetMethod("Prefix")!.ReturnType, "the prefix must never skip vanilla");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void PatchCategory_AppliedToTheInstalledEngine_AttachesThePatchesItDeclares_AndTheJitAccepts()
    {
        const string id = "taom.tests.loregreetings.jit";
        var harmony = new Harmony(id);
        try
        {
            PatchCategoryIndex.Build(typeof(LoreGreetingsModule).Assembly).Apply(harmony, LoreGreetingsModule.PatchCategory);

            foreach (var (target, prefixes, postfixes) in new[] { (GreetingTarget(), 0, 1), (MeetingTarget(), 1, 0) })
            {
                var info = Harmony.GetPatchInfo(target);
                Assert.IsNotNull(info, target.Name + " was not patched: the class is not in the category");
                Assert.AreEqual(postfixes, info!.Postfixes.Count(p => p.owner == id), target.Name + " postfix count");
                Assert.AreEqual(prefixes, info.Prefixes.Count(p => p.owner == id), target.Name + " prefix count");
                Assert.AreEqual(0, info.Transpilers.Count(p => p.owner == id), target.Name + " transpiler count");
                Assert.AreEqual(0, info.Finalizers.Count(p => p.owner == id), target.Name + " finalizer count");
                RuntimeHelpers.PrepareMethod(target.MethodHandle);
            }
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }
}
