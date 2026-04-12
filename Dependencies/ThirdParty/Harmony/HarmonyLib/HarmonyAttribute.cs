using System;

namespace HarmonyLib;

/// <summary>The base class for all Harmony annotations (not meant to be used directly)</summary>
public class HarmonyAttribute : Attribute
{
	/// <summary>The common information for all attributes</summary>
	public HarmonyMethod info = new HarmonyMethod();
}
