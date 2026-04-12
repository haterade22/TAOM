using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HarmonyLib;

/// <summary>A reverse patcher</summary>
public class ReversePatcher
{
	private readonly Harmony instance;

	private readonly MethodBase original;

	private readonly HarmonyMethod standin;

	/// <summary>Creates a reverse patcher</summary>
	/// <param name="instance">The Harmony instance</param>
	/// <param name="original">The original method/constructor</param>
	/// <param name="standin">Your stand-in stub method as <see cref="T:HarmonyLib.HarmonyMethod" /></param>
	public ReversePatcher(Harmony instance, MethodBase original, HarmonyMethod standin)
	{
		this.instance = instance;
		this.original = original;
		this.standin = standin;
	}

	/// <summary>Applies the patch</summary>
	/// <param name="type">The type of patch, see <see cref="T:HarmonyLib.HarmonyReversePatchType" /></param>
	/// <returns>The generated replacement method</returns>
	public MethodInfo Patch(HarmonyReversePatchType type = HarmonyReversePatchType.Original)
	{
		if ((object)original == null)
		{
			throw new NullReferenceException("Null method for " + instance.Id);
		}
		standin.reversePatchType = type;
		MethodInfo transpiler = GetTranspiler(standin.method);
		return PatchFunctions.ReversePatch(standin, original, transpiler);
	}

	internal static MethodInfo GetTranspiler(MethodInfo method)
	{
		string methodName = method.Name;
		Type declaringType = method.DeclaringType;
		List<MethodInfo> declaredMethods = AccessTools.GetDeclaredMethods(declaringType);
		Type ici = typeof(IEnumerable<CodeInstruction>);
		return declaredMethods.FirstOrDefault((MethodInfo m) => !(m.ReturnType != ici) && m.Name.StartsWith("<" + methodName + ">"));
	}
}
