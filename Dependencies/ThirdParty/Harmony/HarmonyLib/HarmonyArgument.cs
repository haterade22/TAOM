using System;

namespace HarmonyLib;

/// <summary>A Harmony annotation</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Parameter, AllowMultiple = true)]
public class HarmonyArgument : Attribute
{
	/// <summary>The name of the original argument</summary>
	public string OriginalName { get; private set; }

	/// <summary>The index of the original argument</summary>
	public int Index { get; private set; }

	/// <summary>The new name of the original argument</summary>
	public string NewName { get; private set; }

	/// <summary>An annotation to declare injected arguments by name</summary>
	public HarmonyArgument(string originalName)
		: this(originalName, null)
	{
	}

	/// <summary>An annotation to declare injected arguments by index</summary>
	/// <param name="index">Zero-based index</param>
	public HarmonyArgument(int index)
		: this(index, null)
	{
	}

	/// <summary>An annotation to declare injected arguments by renaming them</summary>
	/// <param name="originalName">Name of the original argument</param>
	/// <param name="newName">New name</param>
	public HarmonyArgument(string originalName, string newName)
	{
		OriginalName = originalName;
		Index = -1;
		NewName = newName;
	}

	/// <summary>An annotation to declare injected arguments by index and renaming them</summary>
	/// <param name="index">Zero-based index</param>
	/// <param name="name">New name</param>
	public HarmonyArgument(int index, string name)
	{
		OriginalName = null;
		Index = index;
		NewName = name;
	}
}
