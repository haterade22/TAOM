using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace HarmonyLib;

/// <summary>Serializable patch information</summary>
[Serializable]
public class PatchInfo
{
	/// <summary>Prefixes as an array of <see cref="T:HarmonyLib.Patch" /></summary>
	public Patch[] prefixes = Array.Empty<Patch>();

	/// <summary>Postfixes as an array of <see cref="T:HarmonyLib.Patch" /></summary>
	public Patch[] postfixes = Array.Empty<Patch>();

	/// <summary>Transpilers as an array of <see cref="T:HarmonyLib.Patch" /></summary>
	public Patch[] transpilers = Array.Empty<Patch>();

	/// <summary>Finalizers as an array of <see cref="T:HarmonyLib.Patch" /></summary>
	public Patch[] finalizers = Array.Empty<Patch>();

	/// <summary>InnerPrefixes as an array of <see cref="T:HarmonyLib.Patch" /></summary>
	public Patch[] innerprefixes = Array.Empty<Patch>();

	/// <summary>InnerPostfixes as an array of <see cref="T:HarmonyLib.Patch" /></summary>
	public Patch[] innerpostfixes = Array.Empty<Patch>();

	/// <summary>Number of replacements created</summary>
	public int VersionCount;

	/// <summary>Returns if any of the patches wants debugging turned on</summary>
	public bool Debugging
	{
		get
		{
			if (!prefixes.Any((Patch p) => p.debug) && !postfixes.Any((Patch p) => p.debug) && !transpilers.Any((Patch p) => p.debug) && !finalizers.Any((Patch p) => p.debug) && !innerprefixes.Any((Patch p) => p.debug))
			{
				return innerpostfixes.Any((Patch p) => p.debug);
			}
			return true;
		}
	}

	/// <summary>Adds prefixes</summary>
	/// <param name="owner">An owner (Harmony ID)</param>
	/// <param name="methods">The patch methods</param>
	internal void AddPrefixes(string owner, params HarmonyMethod[] methods)
	{
		prefixes = Add(owner, methods, prefixes);
	}

	/// <summary>Adds a prefix</summary>
	[Obsolete("This method only exists for backwards compatibility since the class is public.")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void AddPrefix(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug)
	{
		AddPrefixes(owner, new HarmonyMethod(patch, priority, before, after, debug));
	}

	/// <summary>Removes prefixes</summary>
	/// <param name="owner">The owner of the prefixes, or <c>*</c> for all</param>
	public void RemovePrefix(string owner)
	{
		prefixes = Remove(owner, prefixes);
	}

	/// <summary>Adds postfixes</summary>
	/// <param name="owner">An owner (Harmony ID)</param>
	/// <param name="methods">The patch methods</param>
	internal void AddPostfixes(string owner, params HarmonyMethod[] methods)
	{
		postfixes = Add(owner, methods, postfixes);
	}

	/// <summary>Adds a postfix</summary>
	[Obsolete("This method only exists for backwards compatibility since the class is public.")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void AddPostfix(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug)
	{
		AddPostfixes(owner, new HarmonyMethod(patch, priority, before, after, debug));
	}

	/// <summary>Removes postfixes</summary>
	/// <param name="owner">The owner of the postfixes, or <c>*</c> for all</param>
	public void RemovePostfix(string owner)
	{
		postfixes = Remove(owner, postfixes);
	}

	/// <summary>Adds transpilers</summary>
	/// <param name="owner">An owner (Harmony ID)</param>
	/// <param name="methods">The patch methods</param>
	internal void AddTranspilers(string owner, params HarmonyMethod[] methods)
	{
		transpilers = Add(owner, methods, transpilers);
	}

	/// <summary>Adds a transpiler</summary>
	[Obsolete("This method only exists for backwards compatibility since the class is public.")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void AddTranspiler(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug)
	{
		AddTranspilers(owner, new HarmonyMethod(patch, priority, before, after, debug));
	}

	/// <summary>Removes transpilers</summary>
	/// <summary>Removes transpilers</summary>
	/// <param name="owner">The owner of the transpilers, or <c>*</c> for all</param>
	public void RemoveTranspiler(string owner)
	{
		transpilers = Remove(owner, transpilers);
	}

	/// <summary>Adds finalizers</summary>
	/// <param name="owner">An owner (Harmony ID)</param>
	/// <param name="methods">The patch methods</param>
	internal void AddFinalizers(string owner, params HarmonyMethod[] methods)
	{
		finalizers = Add(owner, methods, finalizers);
	}

	/// <summary>Adds a finalizer</summary>
	[Obsolete("This method only exists for backwards compatibility since the class is public.")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void AddFinalizer(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug)
	{
		AddFinalizers(owner, new HarmonyMethod(patch, priority, before, after, debug));
	}

	/// <summary>Removes finalizers</summary>
	/// <param name="owner">The owner of the finalizers, or <c>*</c> for all</param>
	public void RemoveFinalizer(string owner)
	{
		finalizers = Remove(owner, finalizers);
	}

	/// <summary>Adds inner prefixes</summary>
	/// <param name="owner">An owner (Harmony ID)</param>
	/// <param name="methods">The patch methods</param>
	internal void AddInnerPrefixes(string owner, params HarmonyMethod[] methods)
	{
		innerprefixes = Add(owner, methods, innerprefixes);
	}

	/// <summary>Removes inner prefixes</summary>
	/// <param name="owner">The owner of the inner prefixes, or <c>*</c> for all</param>
	public void RemoveInnerPrefix(string owner)
	{
		innerprefixes = Remove(owner, innerprefixes);
	}

	/// <summary>Adds inner postfixes</summary>
	/// <param name="owner">An owner (Harmony ID)</param>
	/// <param name="methods">The patch methods</param>
	internal void AddInnerPostfixes(string owner, params HarmonyMethod[] methods)
	{
		innerpostfixes = Add(owner, methods, innerpostfixes);
	}

	/// <summary>Removes inner postfixes</summary>
	/// <param name="owner">The owner of the inner postfixes, or <c>*</c> for all</param>
	public void RemoveInnerPostfix(string owner)
	{
		innerpostfixes = Remove(owner, innerpostfixes);
	}

	/// <summary>Removes a patch using its method</summary>
	/// <param name="patch">The method of the patch to remove</param>
	public void RemovePatch(MethodInfo patch)
	{
		prefixes = prefixes.Where((Patch p) => p.PatchMethod != patch).ToArray();
		postfixes = postfixes.Where((Patch p) => p.PatchMethod != patch).ToArray();
		transpilers = transpilers.Where((Patch p) => p.PatchMethod != patch).ToArray();
		finalizers = finalizers.Where((Patch p) => p.PatchMethod != patch).ToArray();
		innerprefixes = innerprefixes.Where((Patch p) => p.PatchMethod != patch).ToArray();
		innerpostfixes = innerpostfixes.Where((Patch p) => p.PatchMethod != patch).ToArray();
	}

	private static Patch[] Add(string owner, HarmonyMethod[] add, Patch[] current)
	{
		if (add.Length == 0)
		{
			return current;
		}
		int initialIndex = current.Length;
		List<Patch> list = new List<Patch>();
		list.AddRange(current);
		list.AddRange(add.Where((HarmonyMethod method) => method != null).Select((HarmonyMethod method, int i) => new Patch(method, i + initialIndex, owner)));
		return list.ToArray();
	}

	private static Patch[] Remove(string owner, Patch[] current)
	{
		if (!(owner == "*"))
		{
			return current.Where((Patch patch) => patch.owner != owner).ToArray();
		}
		return Array.Empty<Patch>();
	}
}
