using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.WandererAllegiance;

/// <summary>
/// Drift guards for the vanilla dialogue the refusal lines hang off. There is no Harmony patch, so
/// nothing auto-enrolls in <c>HarmonyPatchBindingTests</c>; these pin the two facts the design
/// rests on instead. If vanilla renames the <c>companion_hire</c> token or restructures the hire
/// sub-dialogue, the refusal lines are silently never selected and every wanderer hires as in
/// vanilla, so this goes red rather than a log line nobody reads.
/// </summary>
[TestClass]
public class WandererAllegianceBindingTests
{
    private const string BehaviorTypeName = "TaleWorlds.CampaignSystem.CampaignBehaviors.LordConversationsCampaignBehavior";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static System.Type RequireBehavior()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var type = AccessTools.TypeByName(BehaviorTypeName);
        Assert.IsNotNull(type, BehaviorTypeName + " did not resolve against the installed engine.");
        return type!;
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void AddHeroGeneralConversations_StillEmitsTheCompanionHireToken()
    {
        // Installed v1.4.8: the token is the string literal three times in this one method, the
        // player line's output (:797) and the vanilla reply's id and input (:820). Fewer means the
        // sub-dialogue moved or was renamed; re-verify the token before trusting the refusal lines.
        var method = AccessTools.Method(RequireBehavior(), "AddHeroGeneralConversations");
        Assert.IsNotNull(method, "AddHeroGeneralConversations is gone; the hire dialogue is registered somewhere else now.");

        var literals = PatchProcessor.ReadMethodBody(method!)
            .Where(pair => pair.Key == OpCodes.Ldstr)
            .Select(pair => pair.Value as string)
            .ToList();

        Assert.AreEqual(3, literals.Count(s => s == "companion_hire"),
            "companion_hire is no longer emitted three times by AddHeroGeneralConversations.");
        Assert.IsTrue(literals.Contains("lord_pretalk"),
            "lord_pretalk is no longer emitted by AddHeroGeneralConversations; the refusal lines' return path has moved.");
        Assert.IsTrue(literals.Contains("main_option_faction_hire"),
            "the vanilla hire player line (main_option_faction_hire) is gone; nothing reaches companion_hire.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void VanillaHireConditions_StillResolve()
    {
        var behavior = RequireBehavior();

        foreach (var name in new[]
                 {
                     "conversation_hero_hire_on_condition",
                     "conversation_companion_hire_gold_on_condition",
                     "conversation_wanderer_on_condition",
                 })
        {
            var method = AccessTools.Method(behavior, name);
            Assert.IsNotNull(method, name + " is gone; the vanilla gates the refusal lines rely on have changed.");
            Assert.AreEqual(typeof(bool), method!.ReturnType, name + " no longer returns bool.");
            Assert.AreEqual(0, method.GetParameters().Length, name + " grew parameters.");
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void AddDialogLine_StillTakesAPriority()
    {
        // The whole mechanism is "priority 110 beats vanilla's 100". If the overload loses its
        // priority parameter the behavior would not compile, but a renamed or re-typed parameter
        // could still bind to something else.
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var starter = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignGameStarter");
        Assert.IsNotNull(starter, "CampaignGameStarter did not resolve.");

        var addDialogLine = starter!.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "AddDialogLine")
            .Select(m => m.GetParameters())
            .FirstOrDefault(ps => ps.Any(p => p.Name == "priority" && p.ParameterType == typeof(int)));

        Assert.IsNotNull(addDialogLine, "No CampaignGameStarter.AddDialogLine overload takes an int 'priority' parameter.");
        var priority = addDialogLine!.Single(p => p.Name == "priority");
        Assert.IsTrue(priority.HasDefaultValue && (int)priority.DefaultValue! == 100,
            "AddDialogLine's priority default is no longer 100; re-derive the value the refusal lines must beat.");
    }
}
