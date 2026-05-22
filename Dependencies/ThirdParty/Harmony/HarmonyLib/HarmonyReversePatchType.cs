namespace HarmonyLib;

/// <summary>Specifies the type of reverse patch</summary>
public enum HarmonyReversePatchType
{
	/// <summary>Use the unmodified original method (directly from IL)</summary>
	Original,
	/// <summary>Use the original as it is right now including previous patches but excluding future ones</summary>
	Snapshot
}
