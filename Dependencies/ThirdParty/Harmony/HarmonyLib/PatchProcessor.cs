using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using MonoMod.Utils;

namespace HarmonyLib;

/// <summary>A PatchProcessor handles patches on a method/constructor</summary>
public class PatchProcessor
{
	private readonly Harmony instance;

	private readonly MethodBase original;

	private HarmonyMethod prefix;

	private HarmonyMethod postfix;

	private HarmonyMethod transpiler;

	private HarmonyMethod finalizer;

	private HarmonyMethod innerprefix;

	private HarmonyMethod innerpostfix;

	internal static readonly object locker = new object();

	/// <summary>Creates a new PatchProcessor</summary>
	/// <param name="instance">The Harmony instance</param>
	/// <param name="original">The original method/constructor</param>
	public PatchProcessor(Harmony instance, MethodBase original)
	{
		this.instance = instance;
		this.original = original;
	}

	/// <summary>Adds a prefix</summary>
	/// <param name="prefix">The prefix as a <see cref="T:HarmonyLib.HarmonyMethod" /></param>
	/// <returns>A <see cref="T:HarmonyLib.PatchProcessor" /> for chaining calls</returns>
	public PatchProcessor AddPrefix(HarmonyMethod prefix)
	{
		this.prefix = prefix;
		return this;
	}

	/// <summary>Adds a prefix</summary>
	/// <param name="fixMethod">The prefix method</param>
	/// <returns>A <see cref="T:HarmonyLib.PatchProcessor" /> for chaining calls</returns>
	public PatchProcessor AddPrefix(MethodInfo fixMethod)
	{
		prefix = new HarmonyMethod(fixMethod);
		return this;
	}

	/// <summary>Adds a postfix</summary>
	/// <param name="postfix">The postfix as a <see cref="T:HarmonyLib.HarmonyMethod" /></param>
	/// <returns>A <see cref="T:HarmonyLib.PatchProcessor" /> for chaining calls</returns>
	public PatchProcessor AddPostfix(HarmonyMethod postfix)
	{
		this.postfix = postfix;
		return this;
	}

	/// <summary>Adds a postfix</summary>
	/// <param name="fixMethod">The postfix method</param>
	/// <returns>A <see cref="T:HarmonyLib.PatchProcessor" /> for chaining calls</returns>
	public PatchProcessor AddPostfix(MethodInfo fixMethod)
	{
		postfix = new HarmonyMethod(fixMethod);
		return this;
	}

	/// <summary>Adds a transpiler</summary>
	/// <param name="transpiler">The transpiler as a <see cref="T:HarmonyLib.HarmonyMethod" /></param>
	/// <returns>A <see cref="T:HarmonyLib.PatchProcessor" /> for chaining calls</returns>
	public PatchProcessor AddTranspiler(HarmonyMethod transpiler)
	{
		this.transpiler = transpiler;
		return this;
	}

	/// <summary>Adds a transpiler</summary>
	/// <param name="fixMethod">The transpiler method</param>
	/// <returns>A <see cref="T:HarmonyLib.PatchProcessor" /> for chaining calls</returns>
	public PatchProcessor AddTranspiler(MethodInfo fixMethod)
	{
		transpiler = new HarmonyMethod(fixMethod);
		return this;
	}

	/// <summary>Adds a finalizer</summary>
	/// <param name="finalizer">The finalizer as a <see cref="T:HarmonyLib.HarmonyMethod" /></param>
	/// <returns>A <see cref="T:HarmonyLib.PatchProcessor" /> for chaining calls</returns>
	public PatchProcessor AddFinalizer(HarmonyMethod finalizer)
	{
		this.finalizer = finalizer;
		return this;
	}

	/// <summary>Adds a finalizer</summary>
	/// <param name="fixMethod">The finalizer method</param>
	/// <returns>A <see cref="T:HarmonyLib.PatchProcessor" /> for chaining calls</returns>
	public PatchProcessor AddFinalizer(MethodInfo fixMethod)
	{
		finalizer = new HarmonyMethod(fixMethod);
		return this;
	}

	/// <summary>Adds an inner prefix</summary>
	/// <param name="innerPrefix">The inner prefix as a <see cref="T:HarmonyLib.HarmonyMethod" /></param>
	/// <returns>A <see cref="T:HarmonyLib.PatchProcessor" /> for chaining calls</returns>
	public PatchProcessor AddInnerPrefix(HarmonyMethod innerPrefix)
	{
		innerprefix = innerPrefix;
		return this;
	}

	/// <summary>Adds an inner prefix</summary>
	/// <param name="fixMethod">The inner prefix method</param>
	/// <returns>A <see cref="T:HarmonyLib.PatchProcessor" /> for chaining calls</returns>
	public PatchProcessor AddInnerPrefix(MethodInfo fixMethod)
	{
		innerprefix = new HarmonyMethod(fixMethod);
		return this;
	}

	/// <summary>Adds an inner postfix</summary>
	/// <param name="innerPostfix">The inner postfix as a <see cref="T:HarmonyLib.HarmonyMethod" /></param>
	/// <returns>A <see cref="T:HarmonyLib.PatchProcessor" /> for chaining calls</returns>
	public PatchProcessor AddInnerPostfix(HarmonyMethod innerPostfix)
	{
		innerpostfix = innerPostfix;
		return this;
	}

	/// <summary>Adds an inner postfix</summary>
	/// <param name="fixMethod">The inner postfix method</param>
	/// <returns>A <see cref="T:HarmonyLib.PatchProcessor" /> for chaining calls</returns>
	public PatchProcessor AddInnerPostfix(MethodInfo fixMethod)
	{
		innerpostfix = new HarmonyMethod(fixMethod);
		return this;
	}

	/// <summary>Gets all patched original methods in the appdomain</summary>
	/// <returns>An enumeration of patched method/constructor</returns>
	public static IEnumerable<MethodBase> GetAllPatchedMethods()
	{
		lock (locker)
		{
			return HarmonySharedState.GetPatchedMethods();
		}
	}

	/// <summary>Applies all registered patches</summary>
	/// <returns>The generated replacement method</returns>
	public MethodInfo Patch()
	{
		if ((object)original == null)
		{
			throw new NullReferenceException("Null method for " + instance.Id);
		}
		if (!original.IsDeclaredMember())
		{
			MethodBase declaredMember = original.GetDeclaredMember();
			throw new ArgumentException("You can only patch implemented methods/constructors. Patch the declared method " + declaredMember.FullDescription() + " instead.");
		}
		lock (locker)
		{
			PatchInfo patchInfo = HarmonySharedState.GetPatchInfo(original) ?? new PatchInfo();
			patchInfo.AddPrefixes(instance.Id, prefix);
			patchInfo.AddPostfixes(instance.Id, postfix);
			patchInfo.AddTranspilers(instance.Id, transpiler);
			patchInfo.AddFinalizers(instance.Id, finalizer);
			patchInfo.AddInnerPrefixes(instance.Id, innerprefix);
			patchInfo.AddInnerPostfixes(instance.Id, innerpostfix);
			MethodInfo methodInfo = PatchFunctions.UpdateWrapper(original, patchInfo);
			HarmonySharedState.UpdatePatchInfo(original, methodInfo, patchInfo);
			return methodInfo;
		}
	}

	/// <summary>Unpatches patches of a given type and/or Harmony ID</summary>
	/// <param name="type">The <see cref="T:HarmonyLib.HarmonyPatchType" /> patch type</param>
	/// <param name="harmonyID">Harmony ID or <c>*</c> for any</param>
	/// <returns>A <see cref="T:HarmonyLib.PatchProcessor" /> for chaining calls</returns>
	public PatchProcessor Unpatch(HarmonyPatchType type, string harmonyID)
	{
		if ((object)original == null)
		{
			throw new NullReferenceException("Null method for " + instance.Id);
		}
		lock (locker)
		{
			PatchInfo patchInfo = HarmonySharedState.GetPatchInfo(original);
			if (patchInfo == null)
			{
				patchInfo = new PatchInfo();
			}
			if (type == HarmonyPatchType.All || type == HarmonyPatchType.Prefix)
			{
				patchInfo.RemovePrefix(harmonyID);
			}
			if (type == HarmonyPatchType.All || type == HarmonyPatchType.Postfix)
			{
				patchInfo.RemovePostfix(harmonyID);
			}
			if (type == HarmonyPatchType.All || type == HarmonyPatchType.Transpiler)
			{
				patchInfo.RemoveTranspiler(harmonyID);
			}
			if (type == HarmonyPatchType.All || type == HarmonyPatchType.Finalizer)
			{
				patchInfo.RemoveFinalizer(harmonyID);
			}
			if (type == HarmonyPatchType.All || type == HarmonyPatchType.InnerPrefix)
			{
				patchInfo.RemoveInnerPrefix(harmonyID);
			}
			if (type == HarmonyPatchType.All || type == HarmonyPatchType.InnerPostfix)
			{
				patchInfo.RemoveInnerPostfix(harmonyID);
			}
			MethodInfo replacement = PatchFunctions.UpdateWrapper(original, patchInfo);
			HarmonySharedState.UpdatePatchInfo(original, replacement, patchInfo);
			return this;
		}
	}

	/// <summary>Unpatches a specific patch</summary>
	/// <param name="patch">The method of the patch</param>
	/// <returns>A <see cref="T:HarmonyLib.PatchProcessor" /> for chaining calls</returns>
	public PatchProcessor Unpatch(MethodInfo patch)
	{
		if ((object)original == null)
		{
			throw new NullReferenceException("Null method for " + instance.Id);
		}
		lock (locker)
		{
			PatchInfo patchInfo = HarmonySharedState.GetPatchInfo(original);
			if (patchInfo == null)
			{
				patchInfo = new PatchInfo();
			}
			patchInfo.RemovePatch(patch);
			MethodInfo replacement = PatchFunctions.UpdateWrapper(original, patchInfo);
			HarmonySharedState.UpdatePatchInfo(original, replacement, patchInfo);
			return this;
		}
	}

	/// <summary>Gets patch information on an original</summary>
	/// <param name="method">The original method/constructor</param>
	/// <returns>The patch information as <see cref="T:HarmonyLib.Patches" /></returns>
	public static Patches GetPatchInfo(MethodBase method)
	{
		PatchInfo patchInfo;
		lock (locker)
		{
			patchInfo = HarmonySharedState.GetPatchInfo(method);
		}
		if (patchInfo == null)
		{
			return null;
		}
		return new Patches(patchInfo.prefixes, patchInfo.postfixes, patchInfo.transpilers, patchInfo.finalizers, patchInfo.innerprefixes, patchInfo.innerpostfixes);
	}

	/// <summary>Sort patch methods by their priority rules</summary>
	/// <param name="original">The original method</param>
	/// <param name="patches">Patches to sort</param>
	/// <returns>The sorted patch methods</returns>
	public static List<MethodInfo> GetSortedPatchMethods(MethodBase original, Patch[] patches)
	{
		return PatchFunctions.GetSortedPatchMethods(original, patches, debug: false);
	}

	/// <summary>Gets Harmony version for all active Harmony instances</summary>
	/// <param name="currentVersion">[out] The current Harmony version</param>
	/// <returns>A dictionary containing assembly version keyed by Harmony ID</returns>
	public static Dictionary<string, Version> VersionInfo(out Version currentVersion)
	{
		currentVersion = typeof(Harmony).Assembly.GetName().Version;
		Dictionary<string, Assembly> assemblies = new Dictionary<string, Assembly>();
		GetAllPatchedMethods().Do(delegate(MethodBase method)
		{
			PatchInfo patchInfo;
			lock (locker)
			{
				patchInfo = HarmonySharedState.GetPatchInfo(method);
			}
			patchInfo.prefixes.Do(delegate(Patch fix)
			{
				assemblies[fix.owner] = fix.PatchMethod.DeclaringType.Assembly;
			});
			patchInfo.postfixes.Do(delegate(Patch fix)
			{
				assemblies[fix.owner] = fix.PatchMethod.DeclaringType.Assembly;
			});
			patchInfo.transpilers.Do(delegate(Patch fix)
			{
				assemblies[fix.owner] = fix.PatchMethod.DeclaringType.Assembly;
			});
			patchInfo.finalizers.Do(delegate(Patch fix)
			{
				assemblies[fix.owner] = fix.PatchMethod.DeclaringType.Assembly;
			});
			patchInfo.innerprefixes.Do(delegate(Patch fix)
			{
				assemblies[fix.owner] = fix.PatchMethod.DeclaringType.Assembly;
			});
			patchInfo.innerpostfixes.Do(delegate(Patch fix)
			{
				assemblies[fix.owner] = fix.PatchMethod.DeclaringType.Assembly;
			});
		});
		Dictionary<string, Version> result = new Dictionary<string, Version>();
		assemblies.Do(delegate(KeyValuePair<string, Assembly> info)
		{
			AssemblyName assemblyName = info.Value.GetReferencedAssemblies().FirstOrDefault((AssemblyName a) => a.FullName.StartsWith("0Harmony, Version", StringComparison.Ordinal) || a.FullName.StartsWith("TAOM.Dependencies, Version", StringComparison.Ordinal));
			if (assemblyName != null)
			{
				result[info.Key] = assemblyName.Version;
			}
		});
		return result;
	}

	/// <summary>Creates a new empty <see cref="T:System.Reflection.Emit.ILGenerator">generator</see> to use when reading method bodies</summary>
	/// <returns>A new <see cref="T:System.Reflection.Emit.ILGenerator" /></returns>
	public static ILGenerator CreateILGenerator()
	{
		DynamicMethodDefinition dynamicMethodDefinition = new DynamicMethodDefinition($"ILGenerator_{Guid.NewGuid()}", typeof(void), Array.Empty<Type>());
		return dynamicMethodDefinition.GetILGenerator();
	}

	/// <summary>Creates a new <see cref="T:System.Reflection.Emit.ILGenerator">generator</see> matching the method/constructor to use when reading method bodies</summary>
	/// <param name="original">The original method/constructor to copy method information from</param>
	/// <returns>A new <see cref="T:System.Reflection.Emit.ILGenerator" /></returns>
	public static ILGenerator CreateILGenerator(MethodBase original)
	{
		Type returnType = ((original is MethodInfo methodInfo) ? methodInfo.ReturnType : typeof(void));
		List<Type> list = (from pi in original.GetParameters()
			select pi.ParameterType).ToList();
		if (!original.IsStatic)
		{
			list.Insert(0, original.DeclaringType);
		}
		DynamicMethodDefinition dynamicMethodDefinition = new DynamicMethodDefinition("ILGenerator_" + original.Name, returnType, list.ToArray());
		return dynamicMethodDefinition.GetILGenerator();
	}

	/// <summary>Returns the methods unmodified list of code instructions</summary>
	/// <param name="original">The original method/constructor</param>
	/// <param name="generator">Optionally an existing generator that will be used to create all local variables and labels contained in the result (if not specified, an internal generator is used)</param>
	/// <returns>A list containing all the original <see cref="T:HarmonyLib.CodeInstruction" /></returns>
	public static List<CodeInstruction> GetOriginalInstructions(MethodBase original, ILGenerator generator = null)
	{
		return MethodCopier.GetInstructions(generator ?? CreateILGenerator(original), original, 0);
	}

	/// <summary>Returns the methods unmodified list of code instructions</summary>
	/// <param name="original">The original method/constructor</param>
	/// <param name="generator">A new generator that now contains all local variables and labels contained in the result</param>
	/// <returns>A list containing all the original <see cref="T:HarmonyLib.CodeInstruction" /></returns>
	public static List<CodeInstruction> GetOriginalInstructions(MethodBase original, out ILGenerator generator)
	{
		generator = CreateILGenerator(original);
		return MethodCopier.GetInstructions(generator, original, 0);
	}

	/// <summary>Returns the methods current list of code instructions after all existing transpilers have been applied</summary>
	/// <param name="original">The original method/constructor</param>
	/// <param name="maxTranspilers">Apply only the first count of transpilers</param>
	/// <param name="generator">Optionally an existing generator that will be used to create all local variables and labels contained in the result (if not specified, an internal generator is used)</param>
	/// <returns>A list of <see cref="T:HarmonyLib.CodeInstruction" /></returns>
	public static List<CodeInstruction> GetCurrentInstructions(MethodBase original, int maxTranspilers = int.MaxValue, ILGenerator generator = null)
	{
		return MethodCopier.GetInstructions(generator ?? CreateILGenerator(original), original, maxTranspilers);
	}

	/// <summary>Returns the methods current list of code instructions after all existing transpilers have been applied</summary>
	/// <param name="original">The original method/constructor</param>
	/// <param name="generator">A new generator that now contains all local variables and labels contained in the result</param>
	/// <param name="maxTranspilers">Apply only the first count of transpilers</param>
	/// <returns>A list of <see cref="T:HarmonyLib.CodeInstruction" /></returns>
	public static List<CodeInstruction> GetCurrentInstructions(MethodBase original, out ILGenerator generator, int maxTranspilers = int.MaxValue)
	{
		generator = CreateILGenerator(original);
		return MethodCopier.GetInstructions(generator, original, maxTranspilers);
	}

	/// <summary>A low level way to read the body of a method. Used for quick searching in methods</summary>
	/// <param name="method">The original method</param>
	/// <returns>All instructions as opcode/operand pairs</returns>
	public static IEnumerable<KeyValuePair<OpCode, object>> ReadMethodBody(MethodBase method)
	{
		return from instr in MethodBodyReader.GetInstructions(CreateILGenerator(method), method)
			select new KeyValuePair<OpCode, object>(instr.opcode, instr.operand);
	}

	/// <summary>A low level way to read the body of a method. Used for quick searching in methods</summary>
	/// <param name="method">The original method</param>
	/// <param name="generator">An existing generator that will be used to create all local variables and labels contained in the result</param>
	/// <returns>All instructions as opcode/operand pairs</returns>
	public static IEnumerable<KeyValuePair<OpCode, object>> ReadMethodBody(MethodBase method, ILGenerator generator)
	{
		return from instr in MethodBodyReader.GetInstructions(generator, method)
			select new KeyValuePair<OpCode, object>(instr.opcode, instr.operand);
	}
}
