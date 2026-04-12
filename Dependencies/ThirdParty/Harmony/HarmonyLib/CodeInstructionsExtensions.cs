using System.Collections.Generic;

namespace HarmonyLib;

/// <summary>Extensions for a sequence of <see cref="T:HarmonyLib.CodeInstruction" /></summary>
public static class CodeInstructionsExtensions
{
	/// <summary>Searches a list of <see cref="T:HarmonyLib.CodeInstruction" /> by running a sequence of <see cref="T:HarmonyLib.CodeMatch" /> against it</summary>
	/// <param name="instructions">The CodeInstructions (like a body of a method) to search in</param>
	/// <param name="matches">An array of <see cref="T:HarmonyLib.CodeMatch" /> representing the sequence of codes you want to search for</param>
	/// <returns />
	public static bool Matches(this IEnumerable<CodeInstruction> instructions, CodeMatch[] matches)
	{
		return new CodeMatcher(instructions).MatchStartForward(matches).IsValid;
	}
}
