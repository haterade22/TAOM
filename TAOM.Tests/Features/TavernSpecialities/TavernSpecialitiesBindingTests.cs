using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Composition;
using TAOM.Features.TavernSpecialities;
using TAOM.Features.TavernSpecialities.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.TavernSpecialities;

/// <summary>
/// Proves the prefix still fits the installed engine (#937): the method resolves, the patch class is a prefix and
/// nothing else, and the category applies and JIT-compiles.
/// </summary>
[TestClass]
public class TavernSpecialitiesBindingTests
{
    private static readonly System.Type PatchType = typeof(TavernEmployeesCampaignBehavior_OffersFood_TavernSpecialities_Patch);

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static MethodBase Target() => TavernEmployeesCampaignBehavior_OffersFood_TavernSpecialities_Patch.TargetMethod();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheTarget_ResolvesInTheInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        Assert.IsNotNull(Target(), "conversation_tavernmaid_offers_food_on_condition did not resolve.");
    }

    [TestMethod]
    public void ThePatchClass_IsAPrefixAndHasNoPostfix()
    {
        var prefix = PatchType.GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
        var postfix = PatchType.GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static);

        Assert.IsNotNull(prefix, "the dishes are pre-set by a prefix, so vanilla's seven branches overwrite them");
        Assert.IsTrue(prefix!.GetCustomAttributes<HarmonyPrefix>().Any(), "Prefix carries [HarmonyPrefix]");
        Assert.AreEqual(typeof(void), prefix.ReturnType, "a void prefix never skips the original");
        Assert.IsNull(postfix, "a postfix would overwrite the dishes vanilla just set");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PatchCategory_AppliedToTheInstalledEngine_BuildsAReplacementTheJitAccepts()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        const string id = "taom.tests.tavernspecialities.jit";
        var target = Target();
        var harmony = new Harmony(id);
        try
        {
            PatchCategoryIndex.Build(typeof(TavernSpecialitiesModule).Assembly).Apply(harmony, TavernSpecialitiesModule.PatchCategory);

            var info = Harmony.GetPatchInfo(target);
            Assert.IsNotNull(info, "applying the category patched nothing: the class is not in it");
            Assert.IsTrue(info!.Prefixes.Any(p => p.owner == id), "the prefix is attached");
            Assert.AreEqual(0, info.Postfixes.Count(p => p.owner == id), "no postfix is attached");
            System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(target.MethodHandle);
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PatchClass_IsInTheLiteralCategory()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var categories = PatchType.GetCustomAttributes<HarmonyPatchCategory>().Select(c => c.info.category).ToList();

        CollectionAssert.AreEqual(new[] { "Patch111_TavernSpecialities" }, categories);
    }
}
