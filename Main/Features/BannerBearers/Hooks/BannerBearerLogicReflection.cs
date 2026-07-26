using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BannerBearers.Hooks;

// Cached private-member bindings for Patch63's reimplementation of
// BannerBearerLogic.SpawnBannerBearer (issue #360). FormationBannerController is a PRIVATE
// nested class, and the controller lookup + banner bookkeeping are private methods — reflection
// is the only door in. All members are resolved ONCE (harmony-patches.md: never inside a
// Prefix) and pinned by BannerBearersBindingTests so engine drift reddens the suite offline.
internal static class BannerBearerLogicReflection
{
    private static MethodInfo? _getFormationController;   // BannerBearerLogic, private instance
    private static MethodInfo? _addBannerEntity;          // BannerBearerLogic, private instance
    private static PropertyInfo? _controllerBannerItem;   // FormationBannerController.BannerItem
    private static PropertyInfo? _controllerFormation;    // FormationBannerController.Formation
    private static MethodInfo? _onBannerEntityPickedUp;   // FormationBannerController, public on private type

    public static bool BindingsOk { get; private set; }

    // Exposed for the binding tests: names looked up here, in one place, so the test and the
    // runtime cannot drift apart.
    public const string GetFormationControllerName = "GetFormationControllerFromFormation";
    public const string AddBannerEntityName = "AddBannerEntity";
    public const string ControllerTypeName = "FormationBannerController";
    public const string ControllerBannerItemName = "BannerItem";
    public const string ControllerFormationName = "Formation";
    public const string OnBannerEntityPickedUpName = "OnBannerEntityPickedUp";

    public static bool TryResolve(out List<string> missing)
    {
        missing = new List<string>();

        var logicType = typeof(BannerBearerLogic);
        _getFormationController = AccessTools.Method(logicType, GetFormationControllerName);
        if (_getFormationController == null) missing.Add($"{logicType.Name}.{GetFormationControllerName}");

        var controllerType = AccessTools.Inner(logicType, ControllerTypeName);
        if (controllerType == null)
        {
            missing.Add($"{logicType.Name}+{ControllerTypeName}");
        }
        else
        {
            _controllerBannerItem = AccessTools.Property(controllerType, ControllerBannerItemName);
            if (_controllerBannerItem == null) missing.Add($"{ControllerTypeName}.{ControllerBannerItemName}");

            _controllerFormation = AccessTools.Property(controllerType, ControllerFormationName);
            if (_controllerFormation == null) missing.Add($"{ControllerTypeName}.{ControllerFormationName}");

            _onBannerEntityPickedUp = AccessTools.Method(controllerType, OnBannerEntityPickedUpName);
            if (_onBannerEntityPickedUp == null) missing.Add($"{ControllerTypeName}.{OnBannerEntityPickedUpName}");

            _addBannerEntity = AccessTools.Method(logicType, AddBannerEntityName,
                new[] { controllerType, typeof(GameEntity) });
            if (_addBannerEntity == null) missing.Add($"{logicType.Name}.{AddBannerEntityName}");
        }

        BindingsOk = missing.Count == 0;
        return BindingsOk;
    }

    public static object? GetFormationController(BannerBearerLogic logic, Formation formation) =>
        _getFormationController!.Invoke(logic, new object[] { formation });

    public static ItemObject? GetBannerItem(object controller) =>
        (ItemObject?)_controllerBannerItem!.GetValue(controller);

    public static Formation? GetControllerFormation(object controller) =>
        (Formation?)_controllerFormation!.GetValue(controller);

    public static void RegisterBannerEntity(BannerBearerLogic logic, object controller, GameEntity entity, Agent agent)
    {
        _addBannerEntity!.Invoke(logic, new[] { controller, entity });
        _onBannerEntityPickedUp!.Invoke(controller, new object[] { entity, agent });
    }
}
