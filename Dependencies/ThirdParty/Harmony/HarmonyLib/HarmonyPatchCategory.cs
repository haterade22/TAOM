using System;

namespace HarmonyLib;

/// <summary>Annotation to define a category for use with PatchCategory</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public class HarmonyPatchCategory : HarmonyAttribute
{
	/// <summary>Annotation specifying the category</summary>
	/// <param name="category">Name of patch category</param>
	public HarmonyPatchCategory(string category)
	{
		info.category = category;
	}
}
