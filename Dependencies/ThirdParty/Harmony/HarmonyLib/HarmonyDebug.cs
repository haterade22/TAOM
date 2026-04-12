using System;

namespace HarmonyLib;

/// <summary>A Harmony annotation</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method)]
public class HarmonyDebug : HarmonyAttribute
{
	/// <summary>A Harmony annotation to debug a patch (output uses <see cref="T:HarmonyLib.FileLog" /> to log to your Desktop)</summary>
	public HarmonyDebug()
	{
		info.debug = true;
	}
}
