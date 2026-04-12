using System;

namespace HarmonyLib;

/// <summary>Annotation to define your standin methods for reverse patching</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method, AllowMultiple = true)]
public class HarmonyReversePatch : HarmonyAttribute
{
	/// <summary>An annotation that specifies the type of reverse patching</summary>
	/// <param name="type">The <see cref="T:HarmonyLib.HarmonyReversePatchType" /> of the reverse patch</param>
	public HarmonyReversePatch(HarmonyReversePatchType type = HarmonyReversePatchType.Original)
	{
		info.reversePatchType = type;
	}
}
