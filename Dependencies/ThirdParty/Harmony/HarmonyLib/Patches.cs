using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HarmonyLib;

/// <summary>A group of patches</summary>
public class Patches
{
	/// <summary>A collection of prefix <see cref="T:HarmonyLib.Patch" /></summary>
	public readonly ReadOnlyCollection<Patch> Prefixes;

	/// <summary>A collection of postfix <see cref="T:HarmonyLib.Patch" /></summary>
	public readonly ReadOnlyCollection<Patch> Postfixes;

	/// <summary>A collection of transpiler <see cref="T:HarmonyLib.Patch" /></summary>
	public readonly ReadOnlyCollection<Patch> Transpilers;

	/// <summary>A collection of finalizer <see cref="T:HarmonyLib.Patch" /></summary>
	public readonly ReadOnlyCollection<Patch> Finalizers;

	/// <summary>A collection of inner prefix <see cref="T:HarmonyLib.Patch" /></summary>
	public readonly ReadOnlyCollection<Patch> InnerPrefixes;

	/// <summary>A collection of inner postfix <see cref="T:HarmonyLib.Patch" /></summary>
	public readonly ReadOnlyCollection<Patch> InnerPostfixes;

	/// <summary>Gets all owners (Harmony IDs) or all known patches</summary>
	/// <value>The patch owners</value>
	public ReadOnlyCollection<string> Owners
	{
		get
		{
			HashSet<string> hashSet = new HashSet<string>();
			hashSet.UnionWith(Prefixes.Select((Patch p) => p.owner));
			hashSet.UnionWith(Postfixes.Select((Patch p) => p.owner));
			hashSet.UnionWith(Transpilers.Select((Patch p) => p.owner));
			hashSet.UnionWith(Finalizers.Select((Patch p) => p.owner));
			hashSet.UnionWith(InnerPrefixes.Select((Patch p) => p.owner));
			hashSet.UnionWith(InnerPostfixes.Select((Patch p) => p.owner));
			return hashSet.ToList().AsReadOnly();
		}
	}

	/// <summary>Creates a group of patches</summary>
	/// <param name="prefixes">An array of prefixes as <see cref="T:HarmonyLib.Patch" /></param>
	/// <param name="postfixes">An array of postfixes as <see cref="T:HarmonyLib.Patch" /></param>
	/// <param name="transpilers">An array of transpileres as <see cref="T:HarmonyLib.Patch" /></param>
	/// <param name="finalizers">An array of finalizeres as <see cref="T:HarmonyLib.Patch" /></param>
	/// <param name="innerprefixes">An array of inner prefixes as <see cref="T:HarmonyLib.Patch" /></param>
	/// <param name="innerpostfixes">An array of inner postfixes as <see cref="T:HarmonyLib.Patch" /></param>
	public Patches(Patch[] prefixes, Patch[] postfixes, Patch[] transpilers, Patch[] finalizers, Patch[] innerprefixes, Patch[] innerpostfixes)
	{
		if (prefixes == null)
		{
			prefixes = Array.Empty<Patch>();
		}
		if (postfixes == null)
		{
			postfixes = Array.Empty<Patch>();
		}
		if (transpilers == null)
		{
			transpilers = Array.Empty<Patch>();
		}
		if (finalizers == null)
		{
			finalizers = Array.Empty<Patch>();
		}
		if (innerprefixes == null)
		{
			innerprefixes = Array.Empty<Patch>();
		}
		if (innerpostfixes == null)
		{
			innerpostfixes = Array.Empty<Patch>();
		}
		Prefixes = prefixes.ToList().AsReadOnly();
		Postfixes = postfixes.ToList().AsReadOnly();
		Transpilers = transpilers.ToList().AsReadOnly();
		Finalizers = finalizers.ToList().AsReadOnly();
		InnerPrefixes = innerprefixes.ToList().AsReadOnly();
		InnerPostfixes = innerpostfixes.ToList().AsReadOnly();
	}
}
