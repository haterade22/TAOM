using System;
using System.Collections.Generic;
using System.Reflection;
using Bannerlord.UIExtenderEx.Extensions;
using Bannerlord.UIExtenderEx.ViewModels;
using HarmonyLib;
using HarmonyLib.BUTR.Extensions;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Patches;

internal static class ViewModelPatch
{
	public static void Patch(Harmony harmony)
	{
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Expected O, but got Unknown
		harmony.TryPatch(AccessTools2.DeclaredConstructor(typeof(ViewModel)), AccessTools2.DeclaredMethod(typeof(ViewModelPatch), "ViewModelCtorPrefix"));
		harmony.Patch((MethodBase)AccessTools2.Method(typeof(ViewModel), "ExecuteCommand"), new HarmonyMethod(typeof(ViewModelPatch), "ExecuteCommandPatch", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
	}

	private static bool ViewModelCtorPrefix(ViewModel __instance, ref Type ____type, ref object ____propertiesAndMethods)
	{
		if (__instance is BUTRViewModel)
		{
			ViewModelExtensions.DataSourceTypeBindingPropertiesCollectionCtorDelegate dataSourceTypeBindingPropertiesCollectionCtor = ViewModelExtensions.DataSourceTypeBindingPropertiesCollectionCtor;
			if (dataSourceTypeBindingPropertiesCollectionCtor != null)
			{
				____type = ((object)__instance).GetType();
				____propertiesAndMethods = dataSourceTypeBindingPropertiesCollectionCtor(new Dictionary<string, PropertyInfo>(), new Dictionary<string, MethodInfo>());
				return false;
			}
		}
		return true;
	}

	private static bool ExecuteCommandPatch(object __instance, string commandName, object[] parameters)
	{
		if (__instance is ViewModelWrapper viewModelWrapper)
		{
			ViewModel val = viewModelWrapper.Object;
			if (val != null)
			{
				val.ExecuteCommand(commandName, parameters);
				return false;
			}
		}
		return true;
	}
}
