namespace HarmonyLib;

/// <summary>Specifies the type of patch</summary>
public enum HarmonyPatchType
{
	/// <summary>Any patch</summary>
	All,
	/// <summary>A prefix patch</summary>
	Prefix,
	/// <summary>A postfix patch</summary>
	Postfix,
	/// <summary>A transpiler</summary>
	Transpiler,
	/// <summary>A finalizer</summary>
	Finalizer,
	/// <summary>A reverse patch</summary>
	ReversePatch,
	/// <summary>An inner prefix patch</summary>
	InnerPrefix,
	/// <summary>An inner postfix patch</summary>
	InnerPostfix
}
