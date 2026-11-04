using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Library;
using TAOM.Client;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// Patch140's screen patches (build-spec 6.3 and 6.4): the crosshair, the hit marker, three radial views, the equipment view's two key
/// ticks and the formation focus point. Each needs its target and its category, Harmony injects by NAME, and each is a no-op while the
/// Seat is off. The engine types sit in assemblies the test project does not reference, so they are found by name.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SeatScreenPatchWiringTests
{
    private const string Mission = "TaleWorlds.MountAndBlade.GauntletUI.Mission.";

    private static readonly (Type Patch, string Type, string Method)[] Targets =
    {
        (typeof(Patch140_CrosshairArrows), Mission + "MissionGauntletCrosshair", "GetShouldArrowsBeVisible"),
        (typeof(Patch140_CrosshairHitMarker), "TaleWorlds.MountAndBlade.ViewModelCollection.HUD.CrosshairVM", "ShowHitMarker"),
        (typeof(Patch140_EquipDropViewAvailable), Mission + "MissionGauntletMainAgentEquipDropView", "IsMainAgentAvailable"),
        (typeof(Patch140_CheerViewAvailable), Mission + "MissionGauntletMainAgentCheerControllerView", "IsMainAgentAvailable"),
        (typeof(Patch140_ControlModeViewAvailable), Mission + "MissionGauntletMainAgentControlModeView", "IsMainAgentAvailable"),
        (typeof(Patch140_EquipmentViewDropTick), Mission + "MissionGauntletMainAgentEquipmentControllerView", "DropWeaponTick"),
        (typeof(Patch140_EquipmentViewEquipTick), Mission + "MissionGauntletMainAgentEquipmentControllerView", "EquipWeaponTick"),
        (typeof(Patch140_FormationFocusPoint), "TaleWorlds.MountAndBlade.View.MissionViews.MissionFormationTargetSelectionHandler",
            "TryGetFormationDistanceToCenter"),
    };

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestCleanup]
    public void Cleanup() => SeatPresence.Set(false);

    private static Type Engine(string fullName)
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName, throwOnError: false)).FirstOrDefault(t => t != null);
        Assert.IsNotNull(type, fullName + " is not in the installed engine");
        return type!;
    }

    private static MethodBase Resolve((Type Patch, string Type, string Method) target)
    {
        var info = target.Patch.GetCustomAttributes<HarmonyPatch>(inherit: false).Single().info;
        Assert.AreEqual(Engine(target.Type), info.declaringType, target.Patch.Name);
        Assert.AreEqual(target.Method, info.methodName, target.Patch.Name);
        var method = AccessTools.DeclaredMethod(info.declaringType, info.methodName, info.argumentTypes);
        Assert.IsNotNull(method, target.Type + "." + target.Method + " did not resolve in the installed engine");
        return method!;
    }

    [TestMethod]
    public void EveryScreenPatch_HasOneTargetAndTheCategory_AndLivesInTheClient()
    {
        foreach (var (patch, _, _) in Targets)
        {
            Assert.AreEqual(1, patch.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length, patch.Name);
            var categories = patch.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false).Cast<HarmonyPatchCategory>().Select(c => c.info.category);
            CollectionAssert.Contains(categories.ToList(), SeatOfSeeingModule.PatchCategory, patch.Name);
            Assert.AreEqual(typeof(TaomClientComposition).Assembly, patch.Assembly, patch.Name + " names a client-only type");
            Assert.AreEqual("TAOM.Features.SeatOfSeeing.Hooks", patch.Namespace, "PatchCategoryNumberTests takes the owning feature from the namespace");
        }
    }

    [TestMethod]
    public void EveryTarget_ResolvesInTheInstalledEngine()
    {
        foreach (var target in Targets)
            Resolve(target);
    }

    [TestMethod]
    public void TheCrosshairTarget_IsProtectedVirtual_AndTheCrosshairCallsItFirst()
    {
        var arrows = Resolve(Targets[0]);
        Assert.IsTrue(arrows.IsVirtual && arrows.IsFamily, "protected virtual (MissionGauntletCrosshair.cs:209)");
        Assert.AreEqual(typeof(bool), ((MethodInfo)arrows).ReturnType);

        // GetShouldCrosshairBeVisible begins with a virtual call to it, so one patch hides the crosshair too. A later engine change shows here.
        var crosshair = AccessTools.DeclaredMethod(arrows.DeclaringType, "GetShouldCrosshairBeVisible");
        Assert.IsNotNull(crosshair);
        var calls = IlCallScanner.ExtractCalledMethods(crosshair, crosshair.GetMethodBody()!.GetILAsByteArray()).ToList();
        Assert.IsTrue(calls.Any(c => IlCallScanner.SameMethod(c, arrows)), "GetShouldCrosshairBeVisible no longer calls GetShouldArrowsBeVisible");
    }

    [TestMethod]
    public void NoEngineTypeOverridesTheCrosshairTarget()
    {
        var arrows = Resolve(Targets[0]);
        var overrides = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic)
            .SelectMany(a => { try { return a.GetTypes(); } catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null)!; } })
            .Where(t => t != arrows.DeclaringType && arrows.DeclaringType!.IsAssignableFrom(t))
            .Where(t => t.GetMethod("GetShouldArrowsBeVisible", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly) != null)
            .ToList();

        Assert.AreEqual(0, overrides.Count, "a subclass that overrides it would bypass the patch: " + string.Join(", ", overrides.Select(t => t.FullName)));
    }

    [TestMethod]
    public void TheAvailabilityTargets_ArePrivateParameterlessBoolMethods()
    {
        foreach (var target in Targets.Skip(2).Take(3))
        {
            var method = (MethodInfo)Resolve(target);
            Assert.IsTrue(method.IsPrivate, target.Type);
            Assert.AreEqual(typeof(bool), method.ReturnType, target.Type);
            Assert.AreEqual(0, method.GetParameters().Length, target.Type);
        }
    }

    [TestMethod]
    public void TheEquipmentViewTicks_ArePrivateWithOneFloat_AndItsAvailabilityIsNotPatched()
    {
        foreach (var target in Targets.Skip(5).Take(2))
        {
            var method = (MethodInfo)Resolve(target);
            Assert.IsTrue(method.IsPrivate, target.Method);
            CollectionAssert.AreEqual(new[] { typeof(float) }, method.GetParameters().Select(p => p.ParameterType).ToArray(), target.Method);
        }

        var equipmentView = Engine(Mission + "MissionGauntletMainAgentEquipmentControllerView");
        Assert.IsFalse(Targets.Any(t => t.Type == equipmentView.FullName && t.Method == "IsMainAgentAvailable"),
            "its body is short enough for the JIT to inline, and a patch might never reach it");
    }

    [TestMethod]
    public void TheFocusPrefix_InjectsTheHandlersCentreFieldByRef_WithFourUnderscores()
    {
        var handler = Engine(Targets[7].Type);
        var field = AccessTools.Field(handler, "_centerOfScreen");
        Assert.IsNotNull(field, "MissionFormationTargetSelectionHandler._centerOfScreen (v1.5.6 :36)");
        Assert.AreEqual(typeof(Vec2), field!.FieldType);

        var parameters = typeof(Patch140_FormationFocusPoint).GetMethod("Prefix")!.GetParameters();
        CollectionAssert.AreEqual(new[] { "____centerOfScreen" }, parameters.Select(p => p.Name).ToArray(), "Harmony binds by name");
        Assert.AreEqual(typeof(Vec2).MakeByRefType(), parameters[0].ParameterType);

        var target = (MethodInfo)Resolve(Targets[7]);
        Assert.IsTrue(target.IsPrivate);
        Assert.IsTrue(target.GetParameters().Any(p => p.IsOut), "the original's out parameters are not the prefix's concern");
    }

    // ---- Off the Seat every one is a no-op; seated, each does its one thing ----

    [TestMethod]
    public void OffTheSeat_EveryPostfixLeavesTheAnswer_AndEveryPrefixLetsTheOriginalRun()
    {
        SeatPresence.Set(false);
        var result = true;

        Patch140_CrosshairArrows.Postfix(ref result);
        Patch140_EquipDropViewAvailable.Postfix(ref result);
        Patch140_CheerViewAvailable.Postfix(ref result);
        Patch140_ControlModeViewAvailable.Postfix(ref result);

        Assert.IsTrue(result);
        Assert.IsTrue(Patch140_CrosshairHitMarker.Prefix());
        var held = true;
        var time = 0.3f;
        Assert.IsTrue(Patch140_EquipmentViewDropTick.Prefix(ref held, ref time));
        Assert.IsTrue(Patch140_EquipmentViewEquipTick.Prefix(ref held, ref time));
        Assert.IsTrue(held, "off the Seat the view's own pending press is left alone");
        Assert.AreEqual(0.3f, time);
    }

    [TestMethod]
    public void WhileSeated_EveryPostfixAnswersFalse_AndEveryPrefixSkipsTheOriginal()
    {
        SeatPresence.Set(true);
        foreach (var postfix in new Action<bool>[] { Run(Patch140_CrosshairArrows.Postfix), Run(Patch140_EquipDropViewAvailable.Postfix),
                     Run(Patch140_CheerViewAvailable.Postfix), Run(Patch140_ControlModeViewAvailable.Postfix) })
            postfix(true);

        Assert.IsFalse(Patch140_CrosshairHitMarker.Prefix());
        var held = false;
        var time = 0f;
        Assert.IsFalse(Patch140_EquipmentViewDropTick.Prefix(ref held, ref time));
        Assert.IsFalse(Patch140_EquipmentViewEquipTick.Prefix(ref held, ref time));
    }

    // ---- A pending weapon gesture does not cross the Seat (build-spec 6.4) ----
    // The view clears these fields only in its else branch, and the skip runs in its if branch, so a press under 0.5 s made just before
    // entering would run its quick release (a drop, or a pick-up) on the first tick after the Seat. The skip prefixes clear them.

    [TestMethod]
    public void TheDropTickPrefix_WhileSeated_ClearsThePendingPressAndItsTime_AndSkipsTheOriginal()
    {
        SeatPresence.Set(true);
        var pressed = true;
        var time = 0.3f;

        var runs = Patch140_EquipmentViewDropTick.Prefix(ref pressed, ref time);

        Assert.IsFalse(runs);
        Assert.IsFalse(pressed, "_prevDropKeyDown stays set otherwise, and the first tick after the Seat runs HandleQuickReleaseDrop");
        Assert.AreEqual(0f, time);
    }

    [TestMethod]
    public void TheEquipTickPrefix_WhileSeated_ClearsThePendingPressAndItsTime_AndSkipsTheOriginal()
    {
        SeatPresence.Set(true);
        var pressed = true;
        var time = 0.3f;

        var runs = Patch140_EquipmentViewEquipTick.Prefix(ref pressed, ref time);

        Assert.IsFalse(runs);
        Assert.IsFalse(pressed);
        Assert.AreEqual(0f, time);
    }

    [TestMethod]
    public void TheEquipmentTickPrefixes_OffTheSeat_LeaveThePendingPressAlone_AndLetTheOriginalRun()
    {
        SeatPresence.Set(false);
        var pressed = true;
        var time = 0.3f;

        Assert.IsTrue(Patch140_EquipmentViewDropTick.Prefix(ref pressed, ref time));
        Assert.IsTrue(Patch140_EquipmentViewEquipTick.Prefix(ref pressed, ref time));

        Assert.IsTrue(pressed);
        Assert.AreEqual(0.3f, time);
    }

    [DataTestMethod]
    [DataRow(typeof(Patch140_EquipmentViewDropTick), "_prevDropKeyDown", "_dropHoldTime")]
    [DataRow(typeof(Patch140_EquipmentViewEquipTick), "_prevEquipKeyDown", "_equipHoldTime")]
    public void TheEquipmentTickPrefixes_InjectThePendingFieldsByRef_WithFourUnderscores(Type patch, string pressedField, string timeField)
    {
        var view = Engine(Mission + "MissionGauntletMainAgentEquipmentControllerView");
        Assert.AreEqual(typeof(bool), AccessTools.Field(view, pressedField)?.FieldType, "v1.5.6 MissionGauntletMainAgentEquipmentControllerView " + pressedField);
        Assert.AreEqual(typeof(float), AccessTools.Field(view, timeField)?.FieldType, "v1.5.6 MissionGauntletMainAgentEquipmentControllerView " + timeField);

        var parameters = patch.GetMethod("Prefix")!.GetParameters();
        CollectionAssert.AreEqual(new[] { "___" + pressedField, "___" + timeField }, parameters.Select(p => p.Name).ToArray(), "Harmony binds by name");
        CollectionAssert.AreEqual(new[] { typeof(bool).MakeByRefType(), typeof(float).MakeByRefType() }, parameters.Select(p => p.ParameterType).ToArray());
    }

    private delegate void PostfixOf(ref bool result);

    private static Action<bool> Run(PostfixOf postfix) => start =>
    {
        var result = start;
        postfix(ref result);
        Assert.IsFalse(result, "a Seat in the air answers false");
    };

    [TestMethod]
    public void TheFocusPrefix_WithNoCursor_LeavesTheHandlersPointAlone()
    {
        SeatPresence.Set(true);
        SeatPresence.SetCursor(false);
        var centre = new Vec2(960f, 540f);

        Patch140_FormationFocusPoint.Prefix(ref centre);

        Assert.AreEqual(960f, centre.x);
        Assert.AreEqual(540f, centre.y);
        Assert.IsFalse(SeatPresence.TakeFocusSet(), "nothing was set, so nothing is marked");
    }

    [TestMethod]
    public void TheFocusPrefix_OffTheSeat_LeavesTheHandlersPointAlone()
    {
        SeatPresence.Set(false);
        var centre = new Vec2(960f, 540f);

        Patch140_FormationFocusPoint.Prefix(ref centre);

        Assert.AreEqual(960f, centre.x);
        Assert.AreEqual(540f, centre.y);
    }
}
