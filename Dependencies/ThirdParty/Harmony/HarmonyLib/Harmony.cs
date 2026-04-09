using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using MonoMod;

namespace HarmonyLib;

/// <summary>The Harmony instance is the main entry to Harmony. After creating one with an unique identifier, it is used to patch and query the current application domain</summary>
public class Harmony
{
	/// <summary>Set to true before instantiating Harmony to debug Harmony or use an environment variable to set HARMONY_DEBUG to '1' like this: cmd /C "set HARMONY_DEBUG=1 &amp;&amp; game.exe"</summary>
	/// <remarks>This is for full debugging. To debug only specific patches, use the <see cref="T:HarmonyLib.HarmonyDebug" /> attribute</remarks>
	public static bool DEBUG;

	private static readonly ConditionalWeakTable<Assembly, Dictionary<string, List<Type>>> AssemblyCachedCategories = new ConditionalWeakTable<Assembly, Dictionary<string, List<Type>>>();

	/// <summary>The unique identifier</summary>
	public string Id { get; private set; }

	/// <summary>Creates a new Harmony instance</summary>
	/// <param name="id">A unique identifier (you choose your own)</param>
	/// <returns>A Harmony instance</returns>
	public Harmony(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			throw new ArgumentException("id cannot be null or empty");
		}
		try
		{
			string environmentVariable = Environment.GetEnvironmentVariable("HARMONY_DEBUG");
			if (environmentVariable != null && environmentVariable.Length > 0)
			{
				environmentVariable = environmentVariable.Trim();
				DEBUG = environmentVariable == "1" || bool.Parse(environmentVariable);
			}
		}
		catch
		{
		}
		if (DEBUG)
		{
			Assembly assembly = typeof(Harmony).Assembly;
			Version version = assembly.GetName().Version;
			string value = assembly.Location;
			string value2 = Environment.Version.ToString();
			string value3 = Environment.OSVersion.Platform.ToString();
			if (string.IsNullOrEmpty(value))
			{
				value = new Uri(assembly.CodeBase).LocalPath;
			}
			FileLog.Log($"### Harmony id={id}, version={version}, location={value}, env/clr={value2}, platform={value3}");
			MethodBase outsideCaller = AccessTools.GetOutsideCaller();
			if ((object)outsideCaller.DeclaringType != null)
			{
				Assembly assembly2 = outsideCaller.DeclaringType.Assembly;
				value = assembly2.Location;
				if (string.IsNullOrEmpty(value))
				{
					value = new Uri(assembly2.CodeBase).LocalPath;
				}
				FileLog.Log("### Started from " + outsideCaller.FullDescription() + ", location " + value);
				FileLog.Log($"### At {DateTime.Now:yyyy-MM-dd hh.mm.ss}");
			}
		}
		Id = id;
	}

	/// <summary>Searches the current assembly for Harmony annotations and uses them to create patches</summary>
	/// <remarks>This method can fail to use the correct assembly when being inlined. It calls StackTrace.GetFrame(1) which can point to the wrong method/assembly. If you are unsure or run into problems, use <code>PatchAll(Assembly.GetExecutingAssembly())</code> instead.</remarks>
	public void PatchAll()
	{
		MethodBase method = new StackTrace().GetFrame(1).GetMethod();
		Assembly assembly = method.ReflectedType.Assembly;
		PatchAll(assembly);
	}

	/// <summary>Creates a empty patch processor for an original method</summary>
	/// <param name="original">The original method/constructor</param>
	/// <returns>A new <see cref="T:HarmonyLib.PatchProcessor" /> instance</returns>
	public PatchProcessor CreateProcessor(MethodBase original)
	{
		return new PatchProcessor(this, original);
	}

	/// <summary>Creates a patch class processor from a class</summary>
	/// <param name="type">The class/type</param>
	/// <returns>A new <see cref="T:HarmonyLib.PatchClassProcessor" /> instance</returns>
	public PatchClassProcessor CreateClassProcessor(Type type)
	{
		return new PatchClassProcessor(this, type);
	}

	/// <summary>Creates a reverse patcher for one of your stub methods</summary>
	/// <param name="original">The original method/constructor</param>
	/// <param name="standin">The stand-in stub method as <see cref="T:HarmonyLib.HarmonyMethod" /></param>
	/// <returns>A new <see cref="T:HarmonyLib.ReversePatcher" /> instance</returns>
	public ReversePatcher CreateReversePatcher(MethodBase original, HarmonyMethod standin)
	{
		return new ReversePatcher(this, original, standin);
	}

	/// <summary>Searches an assembly for HarmonyPatch-annotated classes/structs and uses them to create patches</summary>
	/// <param name="assembly">The assembly</param>
	public void PatchAll(Assembly assembly)
	{
		AccessTools.GetTypesFromAssembly(assembly).DoIf((Type type) => type.HasHarmonyAttribute(), delegate(Type type)
		{
			CreateClassProcessor(type).Patch();
		});
	}

	/// <summary>Searches an assembly for HarmonyPatch-annotated classes/structs without category annotations and uses them to create patches</summary>
	public void PatchAllUncategorized()
	{
		MethodBase method = new StackTrace().GetFrame(1).GetMethod();
		Assembly assembly = method.ReflectedType.Assembly;
		PatchAllUncategorized(assembly);
	}

	/// <summary>Searches an assembly for HarmonyPatch-annotated classes/structs without category annotations and uses them to create patches</summary>
	/// <param name="assembly">The assembly</param>
	public void PatchAllUncategorized(Assembly assembly)
	{
		PatchClassProcessor[] sequence = (from type in AccessTools.GetTypesFromAssembly(assembly)
			where type.HasHarmonyAttribute()
			select type).Select(CreateClassProcessor).ToArray();
		sequence.DoIf((PatchClassProcessor patchClass) => string.IsNullOrEmpty(patchClass.Category), delegate(PatchClassProcessor patchClass)
		{
			patchClass.Patch();
		});
	}

	/// <summary>Searches the current assembly for Harmony annotations with a specific category and uses them to create patches</summary>
	/// <param name="category">Name of patch category</param>
	public void PatchCategory(string category)
	{
		MethodBase method = new StackTrace().GetFrame(1).GetMethod();
		Assembly assembly = method.ReflectedType.Assembly;
		PatchCategory(assembly, category);
	}

	/// <summary>Searches an assembly for HarmonyPatch-annotated classes/structs with a specific category and uses them to create patches</summary>
	/// <param name="assembly">The assembly</param>
	/// <param name="category">Name of patch category</param>
	public void PatchCategory(Assembly assembly, string category)
	{
		Dictionary<string, List<Type>> value = AssemblyCachedCategories.GetValue(assembly, BuildCategoryCache);
		if (value.TryGetValue(category, out var value2))
		{
			value2.Do(delegate(Type type)
			{
				CreateClassProcessor(type).Patch();
			});
		}
	}

	private static Dictionary<string, List<Type>> BuildCategoryCache(Assembly assembly)
	{
		Dictionary<string, List<Type>> dictionary = new Dictionary<string, List<Type>>();
		Type[] typesFromAssembly = AccessTools.GetTypesFromAssembly(assembly);
		foreach (Type type in typesFromAssembly)
		{
			List<HarmonyMethod> fromType = HarmonyMethodExtensions.GetFromType(type);
			if (fromType.Count == 0)
			{
				continue;
			}
			HarmonyMethod harmonyMethod = HarmonyMethod.Merge(fromType);
			string category = harmonyMethod.category;
			if (!string.IsNullOrEmpty(category))
			{
				if (!dictionary.TryGetValue(category, out var value) && value == null)
				{
					value = new List<Type>();
				}
				value.Add(type);
				dictionary[category] = value;
			}
		}
		return dictionary;
	}

	/// <summary>Creates patches by manually specifying the methods</summary>
	/// <param name="original">The original method/constructor</param>
	/// <param name="prefix">An optional prefix method wrapped in a <see cref="T:HarmonyLib.HarmonyMethod" /> object</param>
	/// <param name="postfix">An optional postfix method wrapped in a <see cref="T:HarmonyLib.HarmonyMethod" /> object</param>
	/// <param name="transpiler">An optional transpiler method wrapped in a <see cref="T:HarmonyLib.HarmonyMethod" /> object</param>
	/// <param name="finalizer">An optional finalizer method wrapped in a <see cref="T:HarmonyLib.HarmonyMethod" /> object</param>
	/// <returns>The replacement method that was created to patch the original method</returns>
	public MethodInfo Patch(MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null, HarmonyMethod finalizer = null)
	{
		PatchProcessor patchProcessor = CreateProcessor(original);
		patchProcessor.AddPrefix(prefix);
		patchProcessor.AddPostfix(postfix);
		patchProcessor.AddTranspiler(transpiler);
		patchProcessor.AddFinalizer(finalizer);
		return patchProcessor.Patch();
	}

	/// <summary>Patches a foreign method onto a stub method of yours and optionally applies transpilers during the process</summary>
	/// <param name="original">The original method/constructor you want to duplicate</param>
	/// <param name="standin">Your stub method as <see cref="T:HarmonyLib.HarmonyMethod" /> that will become the original. Needs to have the correct signature (either original or whatever your transpilers generates)</param>
	/// <param name="transpiler">An optional transpiler as method that will be applied during the process</param>
	/// <returns>The replacement method that was created to patch the stub method</returns>
	public static MethodInfo ReversePatch(MethodBase original, HarmonyMethod standin, MethodInfo transpiler = null)
	{
		return PatchFunctions.ReversePatch(standin, original, transpiler);
	}

	/// <summary>Unpatches methods by patching them with zero patches. Fully unpatching is not supported. Be careful, unpatching is global</summary>
	/// <param name="harmonyID">The optional Harmony ID to restrict unpatching to a specific Harmony instance</param>
	/// <remarks>This method could be static if it wasn't for the fact that unpatching creates a new replacement method that contains your harmony ID</remarks>
	public void UnpatchAll(string harmonyID = null)
	{
		List<MethodBase> list = GetAllPatchedMethods().ToList();
		foreach (MethodBase original in list)
		{
			bool flag = original.HasMethodBody();
			Patches patchInfo = GetPatchInfo(original);
			if (flag)
			{
				patchInfo.Postfixes.DoIf(IDCheck, delegate(Patch patch)
				{
					Unpatch(original, patch.PatchMethod);
				});
				patchInfo.Prefixes.DoIf(IDCheck, delegate(Patch patch)
				{
					Unpatch(original, patch.PatchMethod);
				});
				patchInfo.InnerPostfixes.DoIf(IDCheck, delegate(Patch patch)
				{
					Unpatch(original, patch.PatchMethod);
				});
				patchInfo.InnerPrefixes.DoIf(IDCheck, delegate(Patch patch)
				{
					Unpatch(original, patch.PatchMethod);
				});
			}
			patchInfo.Transpilers.DoIf(IDCheck, delegate(Patch patch)
			{
				Unpatch(original, patch.PatchMethod);
			});
			if (flag)
			{
				patchInfo.Finalizers.DoIf(IDCheck, delegate(Patch patch)
				{
					Unpatch(original, patch.PatchMethod);
				});
			}
		}
		bool IDCheck(Patch patch)
		{
			if (harmonyID != null)
			{
				return patch.owner == harmonyID;
			}
			return true;
		}
	}

	/// <summary>Unpatches a method by patching it with zero patches. Fully unpatching is not supported. Be careful, unpatching is global</summary>
	/// <param name="original">The original method/constructor</param>
	/// <param name="type">The <see cref="T:HarmonyLib.HarmonyPatchType" /></param>
	/// <param name="harmonyID">The optional Harmony ID to restrict unpatching to a specific Harmony instance</param>
	public void Unpatch(MethodBase original, HarmonyPatchType type, string harmonyID = "*")
	{
		PatchProcessor patchProcessor = CreateProcessor(original);
		patchProcessor.Unpatch(type, harmonyID);
	}

	/// <summary>Unpatches a method by patching it with zero patches. Fully unpatching is not supported. Be careful, unpatching is global</summary>
	/// <param name="original">The original method/constructor</param>
	/// <param name="patch">The patch method as method to remove</param>
	public void Unpatch(MethodBase original, MethodInfo patch)
	{
		PatchProcessor patchProcessor = CreateProcessor(original);
		patchProcessor.Unpatch(patch);
	}

	/// <summary>Searches the current assembly for types with a specific category annotation and uses them to unpatch existing patches. Fully unpatching is not supported. Be careful, unpatching is global</summary>
	/// <param name="category">Name of patch category</param>
	public void UnpatchCategory(string category)
	{
		MethodBase method = new StackTrace().GetFrame(1).GetMethod();
		Assembly assembly = method.ReflectedType.Assembly;
		UnpatchCategory(assembly, category);
	}

	/// <summary>Searches an assembly for HarmonyPatch-annotated classes/structs with a specific category annotation and uses them to unpatch existing patches. Fully unpatching is not supported. Be careful, unpatching is global</summary>
	/// <param name="assembly">The assembly</param>
	/// <param name="category">Name of patch category</param>
	public void UnpatchCategory(Assembly assembly, string category)
	{
		Dictionary<string, List<Type>> value = AssemblyCachedCategories.GetValue(assembly, BuildCategoryCache);
		if (value.TryGetValue(category, out var value2))
		{
			value2.Do(delegate(Type type)
			{
				CreateClassProcessor(type).Unpatch();
			});
		}
	}

	/// <summary>Test for patches from a specific Harmony ID</summary>
	/// <param name="harmonyID">The Harmony ID</param>
	/// <returns>True if patches for this ID exist</returns>
	public static bool HasAnyPatches(string harmonyID)
	{
		return GetAllPatchedMethods().Select(GetPatchInfo).Any((Patches info) => info.Owners.Contains(harmonyID));
	}

	/// <summary>Gets patch information for a given original method</summary>
	/// <param name="method">The original method/constructor</param>
	/// <returns>The patch information as <see cref="T:HarmonyLib.Patches" /></returns>
	public static Patches GetPatchInfo(MethodBase method)
	{
		return PatchProcessor.GetPatchInfo(method);
	}

	/// <summary>Gets the methods this instance has patched</summary>
	/// <returns>An enumeration of original methods/constructors</returns>
	public IEnumerable<MethodBase> GetPatchedMethods()
	{
		return from original in GetAllPatchedMethods()
			where GetPatchInfo(original).Owners.Contains(Id)
			select original;
	}

	/// <summary>Gets all patched original methods in the appdomain</summary>
	/// <returns>An enumeration of patched original methods/constructors</returns>
	public static IEnumerable<MethodBase> GetAllPatchedMethods()
	{
		return PatchProcessor.GetAllPatchedMethods();
	}

	/// <summary>Gets the original method from a given replacement method</summary>
	/// <param name="replacement">A replacement method (patched original method)</param>
	/// <returns>The original method/constructor or <c>null</c> if not found</returns>
	public static MethodBase GetOriginalMethod(MethodInfo replacement)
	{
		if (replacement == null)
		{
			throw new ArgumentNullException("replacement");
		}
		return HarmonySharedState.GetRealMethod(replacement, useReplacement: false);
	}

	/// <summary>Tries to get the method from a stackframe including dynamic replacement methods</summary>
	/// <param name="frame">The <see cref="T:System.Diagnostics.StackFrame" /></param>
	/// <returns>For normal frames, <c>frame.GetMethod()</c> is returned. For frames containing patched methods, the replacement method is returned or <c>null</c> if no method can be found</returns>
	public static MethodBase GetMethodFromStackframe(StackFrame frame)
	{
		if (frame == null)
		{
			throw new ArgumentNullException("frame");
		}
		return HarmonySharedState.GetStackFrameMethod(frame, useReplacement: true);
	}

	/// <summary>Gets the original method from the stackframe and uses original if method is a dynamic replacement</summary>
	/// <param name="frame">The <see cref="T:System.Diagnostics.StackFrame" /></param>
	/// <returns>The original method from that stackframe</returns>
	public static MethodBase GetOriginalMethodFromStackframe(StackFrame frame)
	{
		if (frame == null)
		{
			throw new ArgumentNullException("frame");
		}
		return HarmonySharedState.GetStackFrameMethod(frame, useReplacement: false);
	}

	/// <summary>Gets Harmony version for all active Harmony instances</summary>
	/// <param name="currentVersion">[out] The current Harmony version</param>
	/// <returns>A dictionary containing assembly versions keyed by Harmony IDs</returns>
	public static Dictionary<string, Version> VersionInfo(out Version currentVersion)
	{
		return PatchProcessor.VersionInfo(out currentVersion);
	}

	/// <summary>Sets a MonoMod switch value (e.g., "DMDDebug", "DMDDumpTo")</summary>
	/// <param name="name">The switch name</param>
	/// <param name="value">The value to set (bool, string, etc.)</param>
	public static void SetSwitch(string name, object value)
	{
		Switches.SetSwitchValue(name, value);
	}

	/// <summary>Clears a MonoMod switch value</summary>
	/// <param name="name">The switch name</param>
	public static void ClearSwitch(string name)
	{
		Switches.ClearSwitchValue(name);
	}

	/// <summary>Tries to get a MonoMod switch value</summary>
	/// <param name="name">The switch name</param>
	/// <param name="value">The switch value if found</param>
	/// <returns>True if the switch was found, false otherwise</returns>
	public static bool TryGetSwitch(string name, out object value)
	{
		return Switches.TryGetSwitchValue(name, out value);
	}

	/// <summary>Tries to determine if a MonoMod switch is enabled</summary>
	/// <param name="name">The switch name</param>
	/// <param name="isEnabled">True if the switch is enabled, false otherwise</param>
	/// <returns>True if the switch enablement state could be determined, false otherwise</returns>
	public static bool TryIsSwitchEnabled(string name, out bool isEnabled)
	{
		return Switches.TryGetSwitchEnabled(name, out isEnabled);
	}
}
