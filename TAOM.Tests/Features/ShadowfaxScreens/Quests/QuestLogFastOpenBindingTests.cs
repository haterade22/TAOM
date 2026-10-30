using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.ShadowfaxScreens.Quests;

/// <summary>
/// Pins what the Quest Log Fast Open patches depend on in the installed engine (v1.5.5 when written): the private
/// PopulateQuestLog and its callers, the parameter names Harmony injects by (<c>questItem</c>, <c>logIndex</c>,
/// <c>quest</c>), the private enum the sort patch reads as an int, and the constructor and sort bodies the deferral reasons
/// about. A drift fails here, and the adapter's Bind reports an unattached patch at game start.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class QuestLogFastOpenBindingTests
{
    private const BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static Type ComparerBase()
    {
        var type = AccessTools.Inner(typeof(QuestItemSortControllerVM), "QuestItemComparerBase");
        Assert.IsNotNull(type, "QuestItemSortControllerVM.QuestItemComparerBase did not resolve.");
        return type;
    }

    private static MethodBase JournalCtor()
    {
        var ctor = typeof(QuestItemVM).GetConstructor(new[]
        {
            typeof(JournalLogEntry), typeof(Action<QuestItemVM>), typeof(QuestsVM.QuestCompletionType),
        });
        Assert.IsNotNull(ctor, "the QuestItemVM journal constructor did not resolve.");
        return ctor;
    }

    private static MethodBase PopulateQuestLog()
    {
        var method = typeof(QuestItemVM).GetMethod("PopulateQuestLog", AllInstance, null, new[] { typeof(JournalLog), typeof(bool) }, null);
        Assert.IsNotNull(method, "QuestItemVM.PopulateQuestLog(JournalLog, bool) did not resolve.");
        return method;
    }

    private static List<MethodBase> Callees(MethodBase method, params OpCode[] opcodes) =>
        PatchProcessor.GetOriginalInstructions(method)
            .Where(ci => opcodes.Contains(ci.opcode) && ci.operand is MethodBase)
            .Select(ci => (MethodBase)ci.operand)
            .ToList();

    private static bool CallsAny(MethodBase caller, Func<MethodBase, bool> match, params OpCode[] opcodes) =>
        Callees(caller, opcodes).Any(match);

    private static readonly OpCode[] CallOps = { OpCodes.Call, OpCodes.Callvirt, OpCodes.Newobj };

    // ---- patch targets ----

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PopulateQuestLog_IsAPrivateNonVirtualVoidInstanceMethod()
    {
        RequireGame();

        var method = (MethodInfo)PopulateQuestLog();

        Assert.IsTrue(method.IsPrivate);
        Assert.IsFalse(method.IsStatic);
        Assert.IsFalse(method.IsVirtual, "a patch on a virtual never reaches an override");
        Assert.AreEqual(typeof(void), method.ReturnType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void PopulateQuestLog_IsCalledOnlyFromTheThreeQuestItemConstructors()
    {
        RequireGame();
        var populate = PopulateQuestLog();

        var callers = typeof(QuestItemVM).GetMethods(AllInstance | BindingFlags.DeclaredOnly).Cast<MethodBase>()
            .Concat(typeof(QuestItemVM).GetConstructors(AllInstance))
            .Where(m => m.GetMethodBody() != null && CallsAny(m, c => c == populate, CallOps))
            .ToList();

        Assert.AreEqual(3, callers.Count, "callers: " + string.Join(", ", callers.Select(c => c.Name)));
        Assert.IsTrue(callers.All(c => c.IsConstructor), "a non-constructor caller would build stages outside the open's scope");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PopulateQuestLog_TakesTheLogAsItsFirstParameterNamedLog()
    {
        RequireGame();

        var parameters = PopulateQuestLog().GetParameters();

        Assert.AreEqual("log", parameters[0].Name, "the prefix injects its parameter by this name");
        Assert.AreEqual(typeof(JournalLog), parameters[0].ParameterType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SetSelectedItem_IsAPrivateInstanceMethodWhoseParameterIsNamedQuest()
    {
        RequireGame();

        var method = typeof(QuestsVM).GetMethod("SetSelectedItem", AllInstance, null, new[] { typeof(QuestItemVM) }, null);

        Assert.IsNotNull(method, "QuestsVM.SetSelectedItem(QuestItemVM) did not resolve.");
        Assert.IsTrue(method.IsPrivate);
        Assert.AreEqual("quest", method.GetParameters().Single().Name, "the prefix injects its parameter by this name");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void GetJournalLogAt_IsAProtectedInstanceMethodWithTheNamedParameters()
    {
        RequireGame();

        var method = AccessTools.Method(ComparerBase(), "GetJournalLogAt");

        Assert.IsNotNull(method);
        Assert.IsTrue(method.IsFamily, "protected");
        Assert.IsFalse(method.IsStatic);
        Assert.AreEqual(typeof(JournalLog), method.ReturnType);
        var parameters = method.GetParameters();
        Assert.AreEqual(2, parameters.Length);
        Assert.AreEqual(typeof(QuestItemVM), parameters[0].ParameterType);
        Assert.AreEqual("questItem", parameters[0].Name);
        Assert.IsTrue(parameters[1].ParameterType.IsEnum);
        Assert.AreEqual("logIndex", parameters[1].Name);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void JournalLogIndex_FirstIsZeroAndLastIsOne()
    {
        RequireGame();
        var indexType = ComparerBase().GetNestedType("JournalLogIndex", BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(indexType, "QuestItemComparerBase.JournalLogIndex did not resolve.");

        // the sort patch reads the parameter as an int and treats 1 as Last
        Assert.AreEqual(typeof(int), Enum.GetUnderlyingType(indexType), "the postfix declares logIndex as int, which Harmony passes without boxing");
        Assert.IsTrue(indexType.IsNestedFamily, "protected in a private type: the postfix cannot name it");
        Assert.AreEqual(0, Convert.ToInt32(Enum.Parse(indexType, "First")));
        Assert.AreEqual(1, Convert.ToInt32(Enum.Parse(indexType, "Last")));
        Assert.AreEqual(2, Enum.GetNames(indexType).Length);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ConstructorsAndPublicMembers_ThePatchesAndAdapterUse_Exist()
    {
        RequireGame();

        Assert.IsNotNull(typeof(QuestsVM).GetConstructor(new[] { typeof(Action) }), "QuestsVM(Action)");
        JournalCtor();
        Assert.IsTrue(typeof(QuestsVM.QuestCompletionType).IsNestedPublic);
        Assert.AreEqual(typeof(JournalLogEntry), typeof(QuestItemVM).GetProperty("QuestLogEntry")!.PropertyType);
        Assert.IsNotNull(typeof(QuestItemVM).GetMethod("UpdateIsUpdated", Type.EmptyTypes));
        Assert.IsNotNull(typeof(QuestItemVM).GetProperty("IsUpdated")!.GetSetMethod(), "the postfix writes IsUpdated through the public setter");
        var entries = typeof(JournalLogEntry).GetMethod("GetEntries", Type.EmptyTypes);
        Assert.IsNotNull(entries);
        Assert.IsTrue(typeof(IEnumerable<JournalLog>).IsAssignableFrom(entries.ReturnType));
        Assert.AreEqual(typeof(IReadOnlyList<JournalLog>), typeof(IViewDataTracker).GetProperty("UnExaminedQuestLogs")!.PropertyType);
    }

    // ---- the IL the deferral's reasoning rests on ----

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void JournalCtor_CallsPopulateQuestLogUpdateIsUpdatedAndBuildsTheGiverHeroVM()
    {
        RequireGame();
        var ctor = JournalCtor();

        Assert.IsTrue(CallsAny(ctor, c => c == PopulateQuestLog(), CallOps), "PopulateQuestLog");
        Assert.IsTrue(CallsAny(ctor, c => c.Name == "UpdateIsUpdated" && c.DeclaringType == typeof(QuestItemVM), CallOps), "UpdateIsUpdated");
        Assert.IsTrue(CallsAny(ctor, c => c.IsConstructor && c.DeclaringType!.Name == "HeroVM", OpCodes.Newobj), "new HeroVM(RelatedHero)");
        Assert.IsTrue(CallsAny(ctor, c => c.Name == "GetEntries" && c.DeclaringType == typeof(JournalLogEntry), CallOps), "GetEntries loop");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void QuestsVMCtor_BuildsTheJournalItemsAndTheSortSelector()
    {
        RequireGame();
        var ctor = typeof(QuestsVM).GetConstructor(new[] { typeof(Action) })!;

        Assert.IsTrue(CallsAny(ctor, c => c == JournalCtor(), OpCodes.Newobj), "new QuestItemVM(JournalLogEntry, ...)");
        Assert.IsTrue(CallsAny(ctor, c => c.IsConstructor && c.DeclaringType!.IsGenericType && c.DeclaringType.Name.StartsWith("SelectorVM"),
            OpCodes.Newobj), "new SelectorVM<SelectorItemVM>(...), whose Refresh sorts through OnSortOptionChanged");
        var ldftn = PatchProcessor.GetOriginalInstructions(ctor).Any(ci => ci.opcode == OpCodes.Ldftn
            && ci.operand is MethodBase m && m.Name == "SetSelectedItem");
        Assert.IsTrue(ldftn, "the item callback is SetSelectedItem");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void GetJournalLogAt_ReadsStagesForAnItemWithoutAQuest()
    {
        RequireGame();
        var method = AccessTools.Method(ComparerBase(), "GetJournalLogAt");

        Assert.IsTrue(CallsAny(method, c => c.Name == "get_Stages" && c.DeclaringType == typeof(QuestItemVM), CallOps),
            "the quest-less branch reads Stages, which a deferred item has not built");
        Assert.IsTrue(CallsAny(method, c => c.Name == "get_Quest" && c.DeclaringType == typeof(QuestItemVM), CallOps));
    }
}
