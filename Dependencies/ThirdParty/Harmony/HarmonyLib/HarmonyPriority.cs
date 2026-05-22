using System;

namespace HarmonyLib;

/// <summary>A Harmony annotation</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method)]
public class HarmonyPriority : HarmonyAttribute
{
	/// <summary>A Harmony annotation to define patch priority</summary>
	/// <param name="priority">The priority</param>
	public HarmonyPriority(int priority)
	{
		info.priority = priority;
	}
}
