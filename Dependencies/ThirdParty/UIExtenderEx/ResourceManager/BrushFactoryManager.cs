using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml;
using HarmonyLib;
using HarmonyLib.BUTR.Extensions;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;

namespace Bannerlord.UIExtenderEx.ResourceManager;

public static class BrushFactoryManager
{
	private delegate Brush LoadBrushFromDelegate(object instance, XmlNode brushNode);

	private static readonly Dictionary<string, Brush> CustomBrushes = new Dictionary<string, Brush>();

	private static readonly LoadBrushFromDelegate? LoadBrushFrom = AccessTools2.GetDeclaredDelegate<LoadBrushFromDelegate>(typeof(BrushFactory), "LoadBrushFrom");

	public static IEnumerable<Brush> Create(XmlDocument xmlDocument)
	{
		XmlNode? brushesNode = xmlDocument.SelectSingleNode("Brushes");
		if (brushesNode == null)
		{
			Trace.TraceWarning("BrushFactoryManager.Create: XML document has no <Brushes> root node, skipping.");
			yield break;
		}
		foreach (XmlNode childNode in brushesNode.ChildNodes)
		{
			Brush val = LoadBrushFrom?.Invoke(UIResourceManager.BrushFactory, childNode);
			if (val != null)
			{
				yield return val;
			}
		}
	}

	public static void Register(IEnumerable<Brush> brushes)
	{
		foreach (Brush brush in brushes)
		{
			CustomBrushes[brush.Name] = brush;
		}
	}

	public static void CreateAndRegister(XmlDocument xmlDocument)
	{
		Register(Create(xmlDocument));
	}

	internal static void Patch(Harmony harmony)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Expected O, but got Unknown
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Expected O, but got Unknown
		harmony.Patch((MethodBase)AccessTools2.DeclaredPropertyGetter(typeof(BrushFactory), "Brushes"), (HarmonyMethod)null, new HarmonyMethod(typeof(BrushFactoryManager), "GetBrushesPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null);
		harmony.Patch((MethodBase)AccessTools2.DeclaredMethod(typeof(BrushFactory), "GetBrush"), new HarmonyMethod(typeof(BrushFactoryManager), "GetBrushPrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		harmony.TryPatch(AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.PrefabSystem.ConstantDefinition:GetValue"), null, null, AccessTools2.DeclaredMethod(typeof(BrushFactoryManager), "BlankTranspiler"));
		harmony.TryPatch(AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions:SetWidgetAttributeFromString"), null, null, AccessTools2.DeclaredMethod(typeof(BrushFactoryManager), "BlankTranspiler"));
		harmony.TryPatch(AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.UIContext:GetBrush"), null, null, AccessTools2.DeclaredMethod(typeof(BrushFactoryManager), "BlankTranspiler"));
		harmony.TryPatch(AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions:ConvertObject"), null, null, AccessTools2.DeclaredMethod(typeof(BrushFactoryManager), "BlankTranspiler"));
		harmony.TryPatch(AccessTools2.DeclaredMethod("TaleWorlds.MountAndBlade.GauntletUI.Widgets.BoolBrushChangerBrushWidget:OnBooleanUpdated"), null, null, AccessTools2.DeclaredMethod(typeof(BrushFactoryManager), "BlankTranspiler"));
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void GetBrushesPostfix(ref IEnumerable<Brush> __result)
	{
		__result = __result.Concat<Brush>(CustomBrushes.Values);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool GetBrushPrefix(string name, IReadOnlyDictionary<string, Brush> ____brushes, ref Brush __result)
	{
		if (____brushes.ContainsKey(name) || !CustomBrushes.ContainsKey(name))
		{
			return true;
		}
		Brush val = CustomBrushes[name];
		if (val != null)
		{
			__result = val;
			return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static IEnumerable<CodeInstruction> BlankTranspiler(IEnumerable<CodeInstruction> instructions)
	{
		return instructions;
	}
}
