using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FiefManagement.UI;

namespace TAOM.Tests.Migration;

/// <summary>
/// Engine pins for the campaign-map "Fiefs" button (#789). The button is a Harmony postfix on the
/// protected virtual <c>MapNavigationHandler.OnCreateElements</c> plus a <c>MapNavigationElementBase</c>
/// subclass. Either side drifting is a silent no-op or a TypeLoadException at map load, so each member
/// the feature relies on is pinned here against the installed engine (v1.5.5 at the time of writing).
/// </summary>
[TestClass]
public class FiefsNavButtonBindingTests
{
    private const BindingFlags Any =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static Type Engine(string fullName)
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var type = AccessTools.TypeByName(fullName);
        Assert.IsNotNull(type, fullName + " did not resolve against the installed engine; the navigation bar moved.");
        return type!;
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void OnCreateElements_IsAProtectedVirtualInstanceMethod_ReturningTheElementArray()
    {
        var handler = Engine("SandBox.View.Map.Navigation.MapNavigationHandler");

        var method = handler.GetMethod("OnCreateElements", Any | BindingFlags.DeclaredOnly);

        Assert.IsNotNull(method, "MapNavigationHandler.OnCreateElements is gone; the Fiefs button has no seam.");
        Assert.IsFalse(method!.IsStatic);
        Assert.IsTrue(method.IsVirtual, "OnCreateElements must stay virtual (NavalDLC overrides it).");
        Assert.IsTrue(method.IsFamily, "OnCreateElements is protected in the engine; a visibility change moves the seam.");
        Assert.AreEqual(0, method.GetParameters().Length);
        Assert.AreEqual(
            typeof(TaleWorlds.CampaignSystem.INavigationElement[]), method.ReturnType,
            "The postfix writes __result as INavigationElement[].");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void GetMapScreenActionIsEnabledWithReason_IsAPublicStaticBoolWithAnOutTextObject()
    {
        // MapMenuGate calls it as vanilla MapScreen.OnFrameTick does (the map action gate: prisoner, encounter,
        // raft and ferry states, a mission). Pinned by reflection so a rename, a move or a shape change is a red
        // test rather than a MissingMethodException on the first map frame.
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var helper = typeof(TaleWorlds.CampaignSystem.ViewModelCollection.CampaignUIHelper);

        var method = helper.GetMethod("GetMapScreenActionIsEnabledWithReason", BindingFlags.Public | BindingFlags.Static);

        Assert.IsNotNull(method, "CampaignUIHelper.GetMapScreenActionIsEnabledWithReason is gone; the map-menu gate lost vanilla's own term.");
        Assert.AreEqual(typeof(bool), method!.ReturnType);
        var parameters = method.GetParameters();
        Assert.AreEqual(1, parameters.Length);
        Assert.IsTrue(parameters[0].IsOut);
        Assert.AreEqual(typeof(TaleWorlds.Localization.TextObject).MakeByRefType(), parameters[0].ParameterType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void IsNavigationBarEnabled_IsAPublicStaticBoolTakingTheHandler()
    {
        var helper = Engine("SandBox.View.Map.Navigation.MapNavigationHelper");
        var handler = Engine("SandBox.View.Map.Navigation.MapNavigationHandler");

        var method = helper.GetMethod("IsNavigationBarEnabled", BindingFlags.Public | BindingFlags.Static, null, new[] { handler }, null);

        Assert.IsNotNull(method, "MapNavigationHelper.IsNavigationBarEnabled(MapNavigationHandler) is gone.");
        Assert.AreEqual(typeof(bool), method!.ReturnType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MapNavigationElementBase_StillExposesEveryMemberTheElementOverrides()
    {
        var baseType = Engine("SandBox.View.Map.Navigation.MapNavigationElementBase");
        var handler = Engine("SandBox.View.Map.Navigation.MapNavigationHandler");

        Assert.IsTrue(baseType.IsAbstract);
        Assert.IsNotNull(baseType.GetConstructor(Any, null, new[] { handler }, null), "Base ctor (MapNavigationHandler) moved.");

        foreach (var getter in new[] { "IsActive", "IsLockingNavigation", "HasAlert", "StringId" })
        {
            var property = baseType.GetProperty(getter, Any);
            Assert.IsNotNull(property, "Base property " + getter + " is gone.");
            Assert.IsTrue(property!.GetMethod!.IsAbstract, getter + " is no longer abstract; the override may be redundant or shadowing.");
        }

        Assert.IsTrue(baseType.GetMethod("OpenView", Any, null, Type.EmptyTypes, null)!.IsAbstract);
        Assert.IsTrue(baseType.GetMethod("OpenView", Any, null, new[] { typeof(object[]) }, null)!.IsAbstract);
        Assert.IsTrue(baseType.GetMethod("GoToLink", Any, null, Type.EmptyTypes, null)!.IsAbstract);

        foreach (var hook in new[] { "GetPermission", "GetTooltip", "GetAlertTooltip" })
        {
            var method = baseType.GetMethod(hook, Any, null, Type.EmptyTypes, null);
            Assert.IsNotNull(method, "Base method " + hook + " is gone.");
            Assert.IsTrue(method!.IsAbstract && method.IsFamily, hook + " is no longer a protected abstract method.");
        }

        Assert.IsNotNull(baseType.GetField("_handler", Any), "The protected _handler field is what the permission check passes on.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TaomFiefsNavigationElement_OverridesEveryAbstractMemberItselfAndDerivesFromTheBase()
    {
        var baseType = Engine("SandBox.View.Map.Navigation.MapNavigationElementBase");
        var element = typeof(TaomFiefsNavigationElement);

        Assert.AreSame(baseType, element.BaseType);

        var abstractMembers = baseType.GetMethods(Any)
            .Where(m => m.IsAbstract)
            .Select(m => m.Name + "/" + m.GetParameters().Length)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        Assert.IsTrue(abstractMembers.Count >= 10, "Expected the base's abstract surface to be at least 10 members.");

        var overridden = element.GetMethods(Any | BindingFlags.DeclaredOnly)
            .Select(m => m.Name + "/" + m.GetParameters().Length)
            .ToHashSet();
        var missing = abstractMembers.Where(a => !overridden.Contains(a)).ToList();
        Assert.AreEqual(0, missing.Count, "The element does not declare: " + string.Join(", ", missing));
    }
}
