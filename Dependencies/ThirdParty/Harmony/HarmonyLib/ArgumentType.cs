namespace HarmonyLib;

/// <summary>Specifies the type of argument</summary>
public enum ArgumentType
{
	/// <summary>This is a normal argument</summary>
	Normal,
	/// <summary>This is a reference argument (ref)</summary>
	Ref,
	/// <summary>This is an out argument (out)</summary>
	Out,
	/// <summary>This is a pointer argument (&amp;)</summary>
	Pointer
}
