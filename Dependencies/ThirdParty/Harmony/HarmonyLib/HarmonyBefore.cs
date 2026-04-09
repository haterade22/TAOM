using System;

namespace HarmonyLib;

/// <summary>A Harmony annotation</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method)]
public class HarmonyBefore : HarmonyAttribute
{
	/// <summary>A Harmony annotation to define that a patch comes before another patch</summary>
	/// <param name="before">The array of harmony IDs of the other patches</param>
	public HarmonyBefore(params string[] before)
	{
		info.before = before;
	}
}
