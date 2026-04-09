using System;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib;

/// <summary>A serializable patch</summary>
[Serializable]
public class Patch : IComparable
{
	/// <summary>Zero-based index</summary>
	public readonly int index;

	/// <summary>The owner (Harmony ID)</summary>
	public readonly string owner;

	/// <summary>The priority, see <see cref="T:HarmonyLib.Priority" /></summary>
	public readonly int priority;

	/// <summary>Keep this patch before the patches indicated in the list of Harmony IDs</summary>
	public readonly string[] before;

	/// <summary>Keep this patch after the patches indicated in the list of Harmony IDs</summary>
	public readonly string[] after;

	/// <summary>A flag that will log the replacement method via <see cref="T:HarmonyLib.FileLog" /> every time this patch is used to build the replacement, even in the future</summary>
	public readonly bool debug;

	[NonSerialized]
	private MethodInfo patchMethod;

	private int methodToken;

	private string moduleGUID;

	/// <summary>For an infix patch, this defines the inner method that we will apply the patch to</summary>
	public readonly InnerMethod innerMethod;

	/// <summary>The method of the static patch method</summary>
	public MethodInfo PatchMethod
	{
		get
		{
			if ((object)patchMethod == null)
			{
				patchMethod = AccessTools.GetMethodByModuleAndToken(moduleGUID, methodToken);
			}
			return patchMethod;
		}
		set
		{
			patchMethod = value;
			methodToken = patchMethod.MetadataToken;
			moduleGUID = patchMethod.Module.ModuleVersionId.ToString();
		}
	}

	/// <summary>Creates a patch</summary>
	/// <param name="patch">The method of the patch</param>
	/// <param name="index">Zero-based index</param>
	/// <param name="owner">An owner (Harmony ID)</param>
	/// <param name="priority">The priority, see <see cref="T:HarmonyLib.Priority" /></param>
	/// <param name="before">A list of Harmony IDs for patches that should run after this patch</param>
	/// <param name="after">A list of Harmony IDs for patches that should run before this patch</param>
	/// <param name="debug">A flag that will log the replacement method via <see cref="T:HarmonyLib.FileLog" /> every time this patch is used to build the replacement, even in the future</param>
	public Patch(MethodInfo patch, int index, string owner, int priority, string[] before, string[] after, bool debug)
	{
		if (patch is DynamicMethod)
		{
			throw new Exception("Cannot directly reference dynamic method \"" + patch.FullDescription() + "\" in Harmony. Use a factory method instead that will return the dynamic method.");
		}
		this.index = index;
		this.owner = owner;
		this.priority = ((priority == -1) ? 400 : priority);
		this.before = before ?? Array.Empty<string>();
		this.after = after ?? Array.Empty<string>();
		this.debug = debug;
		PatchMethod = patch;
	}

	/// <summary>Creates a patch</summary>
	/// <param name="method">The method of the patch</param>
	/// <param name="index">Zero-based index</param>
	/// <param name="owner">An owner (Harmony ID)</param>
	public Patch(HarmonyMethod method, int index, string owner)
		: this(method.method, index, owner, method.priority, method.before, method.after, method.debug == true)
	{
	}

	internal Patch(int index, string owner, int priority, string[] before, string[] after, bool debug, int methodToken, string moduleGUID)
	{
		this.index = index;
		this.owner = owner;
		this.priority = ((priority == -1) ? 400 : priority);
		this.before = before ?? Array.Empty<string>();
		this.after = after ?? Array.Empty<string>();
		this.debug = debug;
		this.methodToken = methodToken;
		this.moduleGUID = moduleGUID;
	}

	/// <summary>Get the patch method or a DynamicMethod if original patch method is a patch factory</summary>
	/// <param name="original">The original method/constructor</param>
	/// <returns>The method of the patch</returns>
	public MethodInfo GetMethod(MethodBase original)
	{
		MethodInfo methodInfo = PatchMethod;
		if (methodInfo.ReturnType != typeof(DynamicMethod) && methodInfo.ReturnType != typeof(MethodInfo))
		{
			return methodInfo;
		}
		if (!methodInfo.IsStatic)
		{
			return methodInfo;
		}
		ParameterInfo[] parameters = methodInfo.GetParameters();
		if (parameters.Length != 1)
		{
			return methodInfo;
		}
		if (parameters[0].ParameterType != typeof(MethodBase))
		{
			return methodInfo;
		}
		return methodInfo.Invoke(null, new object[1] { original }) as MethodInfo;
	}

	/// <summary>Determines whether patches are equal</summary>
	/// <param name="obj">The other patch</param>
	/// <returns>true if equal</returns>
	public override bool Equals(object obj)
	{
		if (obj != null && obj is Patch)
		{
			return PatchMethod == ((Patch)obj).PatchMethod;
		}
		return false;
	}

	/// <summary>Determines how patches sort</summary>
	/// <param name="obj">The other patch</param>
	/// <returns>integer to define sort order (-1, 0, 1)</returns>
	public int CompareTo(object obj)
	{
		return PatchInfoSerialization.PriorityComparer(obj, index, priority);
	}

	/// <summary>Hash function</summary>
	/// <returns>A hash code</returns>
	public override int GetHashCode()
	{
		return PatchMethod.GetHashCode();
	}
}
