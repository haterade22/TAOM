using System;

namespace HarmonyLib;

/// <summary>An exception block</summary>
public class ExceptionBlock
{
	/// <summary>Block type</summary>
	public ExceptionBlockType blockType;

	/// <summary>Catch type</summary>
	public Type catchType;

	/// <summary>Creates a new ExceptionBlock</summary>
	/// <param name="blockType">The <see cref="T:HarmonyLib.ExceptionBlockType" /></param>
	/// <param name="catchType">The catch type</param>
	public ExceptionBlock(ExceptionBlockType blockType, Type catchType = null)
	{
		this.blockType = blockType;
		this.catchType = catchType ?? typeof(object);
	}
}
