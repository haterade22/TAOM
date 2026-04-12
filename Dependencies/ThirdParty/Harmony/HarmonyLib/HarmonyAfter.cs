using System;

namespace HarmonyLib;

/// <summary>A Harmony annotation</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method)]
public class HarmonyAfter : HarmonyAttribute
{
	/// <summary>A Harmony annotation to define that a patch comes after another patch</summary>
	/// <param name="after">The array of harmony IDs of the other patches</param>
	public HarmonyAfter(params string[] after)
	{
		info.after = after;
	}
}
