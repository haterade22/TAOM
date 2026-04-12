using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace HarmonyLib;

/// <summary>Under Mono, HarmonyException wraps IL compile errors with detailed information about the failure</summary>
public class HarmonyException : Exception
{
	private Dictionary<int, CodeInstruction> instructions;

	private int errorOffset;

	internal HarmonyException()
	{
	}

	internal HarmonyException(string message)
		: base(message)
	{
	}

	internal HarmonyException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	internal HarmonyException(Exception innerException, Dictionary<int, CodeInstruction> instructions, int errorOffset)
		: base("IL Compile Error", innerException)
	{
		this.instructions = instructions;
		this.errorOffset = errorOffset;
	}

	internal static Exception Create(Exception ex, Dictionary<int, CodeInstruction> finalInstructions)
	{
		Match match = Regex.Match(ex.Message.TrimEnd(), "Reason: Invalid IL code in.+: IL_(\\d{4}): (.+)$");
		if (!match.Success)
		{
			return ex;
		}
		int num = int.Parse(match.Groups[1].Value, NumberStyles.HexNumber);
		Regex.Replace(match.Groups[2].Value, " {2,}", " ");
		if (ex is HarmonyException ex2)
		{
			ex2.instructions = finalInstructions;
			ex2.errorOffset = num;
			return ex2;
		}
		return new HarmonyException(ex, finalInstructions, num);
	}

	/// <summary>Get a list of IL instructions in pairs of offset+code</summary>
	/// <returns>A list of key/value pairs which represent an offset and the code at that offset</returns>
	public List<KeyValuePair<int, CodeInstruction>> GetInstructionsWithOffsets()
	{
		return instructions.OrderBy((KeyValuePair<int, CodeInstruction> ins) => ins.Key).ToList();
	}

	/// <summary>Get a list of IL instructions without offsets</summary>
	/// <returns>A list of <see cref="T:HarmonyLib.CodeInstruction" /></returns>
	public List<CodeInstruction> GetInstructions()
	{
		return (from ins in instructions
			orderby ins.Key
			select ins.Value).ToList();
	}

	/// <summary>Get the error offset of the errornous IL instruction</summary>
	/// <returns>The offset</returns>
	public int GetErrorOffset()
	{
		return errorOffset;
	}

	/// <summary>Get the index of the errornous IL instruction</summary>
	/// <returns>The index into the list of instructions or -1 if not found</returns>
	public int GetErrorIndex()
	{
		if (instructions.TryGetValue(errorOffset, out var value))
		{
			return GetInstructions().IndexOf(value);
		}
		return -1;
	}
}
