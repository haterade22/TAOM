using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Extensions;
using Bannerlord.UIExtenderEx.ViewModels;
using HarmonyLib;
using HarmonyLib.BUTR.Extensions;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Patches;

internal static class ViewModelWithMixinPatch
{
	private static ConcurrentDictionary<Type, object?> ViewModelInitializations { get; } = new ConcurrentDictionary<Type, object>();

	private static ConcurrentDictionary<string, object?> ViewModelsRefreshPatches { get; } = new ConcurrentDictionary<string, object>();

	public static void Patch(Harmony harmony, Type viewModelType, string? refreshMethodName = null)
	{
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Expected O, but got Unknown
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Expected O, but got Unknown
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Expected O, but got Unknown
		if (ViewModelInitializations.TryAdd(viewModelType, null))
		{
			foreach (ConstructorInfo declaredConstructor in AccessTools.GetDeclaredConstructors(viewModelType, (bool?)false))
			{
				harmony.Patch((MethodBase)declaredConstructor, (HarmonyMethod)null, (HarmonyMethod)null, new HarmonyMethod(typeof(ViewModelWithMixinPatch), "ViewModel_Constructor_Transpiler", (Type[])null), (HarmonyMethod)null);
			}
			harmony.Patch((MethodBase)(AccessTools2.DeclaredMethod(viewModelType, "OnFinalize", null, null, logErrorInTrace: false) ?? AccessTools2.DeclaredMethod("TaleWorlds.Library.ViewModel:OnFinalize")), (HarmonyMethod)null, (HarmonyMethod)null, new HarmonyMethod(typeof(ViewModelWithMixinPatch), "ViewModel_Finalize_Transpiler", (Type[])null), (HarmonyMethod)null);
		}
		if (ViewModelsRefreshPatches.TryAdd(viewModelType.FullName + ":" + refreshMethodName, null) && refreshMethodName != null)
		{
			MethodInfo methodInfo = AccessTools2.Method(viewModelType, refreshMethodName);
			while (!AccessTools.IsDeclaredMember<MethodInfo>(methodInfo))
			{
				methodInfo = AccessTools.GetDeclaredMember<MethodInfo>(methodInfo);
			}
			harmony.Patch((MethodBase)methodInfo, (HarmonyMethod)null, (HarmonyMethod)null, new HarmonyMethod(typeof(ViewModelWithMixinPatch), "ViewModel_Refresh_Transpiler", (Type[])null), (HarmonyMethod)null);
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static IEnumerable<CodeInstruction> ViewModel_Constructor_Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
	{
		return InsertMethodAtEnd(instructions, method, AccessTools2.DeclaredMethod(typeof(ViewModelWithMixinPatch), "Constructor"));
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void Constructor(ViewModel viewModel, string _)
	{
		foreach (UIExtenderRuntime allRuntime in UIExtender.GetAllRuntimes())
		{
			allRuntime.ViewModelComponent.InitializeMixinsForVMInstance(viewModel);
			if (!allRuntime.ViewModelComponent.MixinInstanceCache.TryGetValue(viewModel, out List<IViewModelMixin> value) || !allRuntime.ViewModelComponent.MixinInstanceRefreshFromConstructorCache.TryGetValue(viewModel, out List<string> value2))
			{
				continue;
			}
			foreach (IViewModelMixin item in value)
			{
				ViewModelMixinAttribute customAttribute = item.GetType().GetCustomAttribute<ViewModelMixinAttribute>();
				foreach (string item2 in value2)
				{
					if (item2 == customAttribute?.RefreshMethodName)
					{
						item.OnRefresh();
					}
				}
			}
			allRuntime.ViewModelComponent.MixinInstanceRefreshFromConstructorCache.Remove(viewModel);
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static IEnumerable<CodeInstruction> ViewModel_Refresh_Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
	{
		return InsertMethodAtEnd(instructions, method, AccessTools2.DeclaredMethod(typeof(ViewModelWithMixinPatch), "Refresh"));
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void Refresh(ViewModel viewModel, string methodName)
	{
		foreach (UIExtenderRuntime allRuntime in UIExtender.GetAllRuntimes())
		{
			if (!allRuntime.ViewModelComponent.MixinInstanceCache.TryGetValue(viewModel, out List<IViewModelMixin> value))
			{
				allRuntime.ViewModelComponent.MixinInstanceRefreshFromConstructorCache.GetOrAdd(viewModel, (ViewModel _) => new List<string>()).Add(methodName);
				continue;
			}
			foreach (IViewModelMixin item in value)
			{
				if (methodName == item.GetType().GetCustomAttribute<ViewModelMixinAttribute>()?.RefreshMethodName)
				{
					item.OnRefresh();
				}
			}
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static IEnumerable<CodeInstruction> ViewModel_Finalize_Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
	{
		return InsertMethodAtEnd(instructions, method, AccessTools2.DeclaredMethod(typeof(ViewModelWithMixinPatch), "Finalize"));
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void Finalize(ViewModel viewModel, string _)
	{
		foreach (UIExtenderRuntime allRuntime in UIExtender.GetAllRuntimes())
		{
			if (!allRuntime.ViewModelComponent.MixinInstanceCache.TryGetValue(viewModel, out List<IViewModelMixin> value))
			{
				continue;
			}
			foreach (IViewModelMixin item in value)
			{
				item.OnFinalize();
			}
		}
	}

	private static IEnumerable<CodeInstruction> InsertMethodAtEnd(IEnumerable<CodeInstruction> instructions, MethodBase originalMethod, MethodInfo? method)
	{
		foreach (CodeInstruction instruction in instructions)
		{
			if ((object)method != null && instruction.opcode == OpCodes.Ret)
			{
				List<Label> labels = instruction.labels;
				instruction.labels = new List<Label>();
				yield return new CodeInstruction(OpCodes.Ldarg_0, (object)null)
				{
					labels = labels
				};
				yield return new CodeInstruction(OpCodes.Ldstr, (object)originalMethod.Name);
				yield return new CodeInstruction(OpCodes.Call, (object)method);
			}
			yield return instruction;
		}
	}
}
