using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HarmonyLib;

/// <summary>A wrapper around a method to use it as a patch (for example a Prefix)</summary>
public class HarmonyMethod
{
	/// <summary>The original method</summary>
	public MethodInfo method;

	/// <summary>Patch Category</summary>
	public string category;

	/// <summary>Class/type declaring this patch</summary>
	public Type declaringType;

	/// <summary>Patch method name</summary>
	public string methodName;

	/// <summary>Optional patch <see cref="T:HarmonyLib.MethodType" /></summary>
	public MethodType? methodType;

	/// <summary>Array of argument types of the patch method</summary>
	public Type[] argumentTypes;

	/// <summary>
	///     <see cref="T:HarmonyLib.Priority" /> of the patch</summary>
	public int priority = -1;

	/// <summary>Install this patch before patches with these Harmony IDs</summary>
	public string[] before;

	/// <summary>Install this patch after patches with these Harmony IDs</summary>
	public string[] after;

	/// <summary>Reverse patch type, see <see cref="T:HarmonyLib.HarmonyReversePatchType" /></summary>
	public HarmonyReversePatchType? reversePatchType;

	/// <summary>Create debug output for this patch</summary>
	public bool? debug;

	/// <summary>Whether to use <see cref="F:HarmonyLib.MethodDispatchType.Call" /> (<c>true</c>) or <see cref="F:HarmonyLib.MethodDispatchType.VirtualCall" /> (<c>false</c>) mechanics
	/// for <see cref="T:HarmonyLib.HarmonyDelegate" />-attributed delegate</summary>
	public bool nonVirtualDelegate;

	/// <summary>Default constructor</summary>
	public HarmonyMethod()
	{
	}

	private void ImportMethod(MethodInfo theMethod)
	{
		method = theMethod;
		if ((object)method != null)
		{
			List<HarmonyMethod> fromMethod = HarmonyMethodExtensions.GetFromMethod(method);
			if (fromMethod != null)
			{
				Merge(fromMethod).CopyTo(this);
			}
		}
	}

	/// <summary>Creates a patch from a given method</summary>
	/// <param name="method">The original method</param>
	public HarmonyMethod(MethodInfo method)
	{
		if ((object)method == null)
		{
			throw new ArgumentNullException("method");
		}
		ImportMethod(method);
	}

	/// <summary>Creates a patch from a given method</summary>
	/// <param name="delegate">The original method</param>
	public HarmonyMethod(Delegate @delegate)
		: this(@delegate.Method)
	{
	}

	/// <summary>Creates a patch from a given method</summary>
	/// <param name="method">The original method</param>
	/// <param name="priority">The patch <see cref="T:HarmonyLib.Priority" /></param>
	/// <param name="before">A list of harmony IDs that should come after this patch</param>
	/// <param name="after">A list of harmony IDs that should come before this patch</param>
	/// <param name="debug">Set to true to generate debug output</param>
	public HarmonyMethod(MethodInfo method, int priority = -1, string[] before = null, string[] after = null, bool? debug = null)
	{
		if ((object)method == null)
		{
			throw new ArgumentNullException("method");
		}
		ImportMethod(method);
		this.priority = priority;
		this.before = before;
		this.after = after;
		this.debug = debug;
	}

	/// <summary>Creates a patch from a given method</summary>
	/// <param name="delegate">The original method</param>
	/// <param name="priority">The patch <see cref="T:HarmonyLib.Priority" /></param>
	/// <param name="before">A list of harmony IDs that should come after this patch</param>
	/// <param name="after">A list of harmony IDs that should come before this patch</param>
	/// <param name="debug">Set to true to generate debug output</param>
	public HarmonyMethod(Delegate @delegate, int priority = -1, string[] before = null, string[] after = null, bool? debug = null)
		: this(@delegate.Method, priority, before, after, debug)
	{
	}

	/// <summary>Creates a patch from a given method</summary>
	/// <param name="methodType">The patch class/type</param>
	/// <param name="methodName">The patch method name</param>
	/// <param name="argumentTypes">The optional argument types of the patch method (for overloaded methods)</param>
	public HarmonyMethod(Type methodType, string methodName, Type[] argumentTypes = null)
	{
		MethodInfo methodInfo = AccessTools.Method(methodType, methodName, argumentTypes);
		if ((object)methodInfo == null)
		{
			throw new ArgumentException($"Cannot not find method for type {methodType} and name {methodName} and parameters {argumentTypes?.Description()}");
		}
		ImportMethod(methodInfo);
	}

	/// <summary>Gets the names of all internal patch info fields</summary>
	/// <returns>A list of field names</returns>
	public static List<string> HarmonyFields()
	{
		return (from s in AccessTools.GetFieldNames(typeof(HarmonyMethod))
			where s != "method"
			select s).ToList();
	}

	/// <summary>Merges annotations</summary>
	/// <param name="attributes">The list of <see cref="T:HarmonyLib.HarmonyMethod" /> to merge</param>
	/// <returns>The merged <see cref="T:HarmonyLib.HarmonyMethod" /></returns>
	public static HarmonyMethod Merge(List<HarmonyMethod> attributes)
	{
		HarmonyMethod harmonyMethod = new HarmonyMethod();
		if (attributes == null || attributes.Count == 0)
		{
			return harmonyMethod;
		}
		Traverse resultTrv = Traverse.Create(harmonyMethod);
		attributes.ForEach(delegate(HarmonyMethod attribute)
		{
			Traverse trv = Traverse.Create(attribute);
			HarmonyFields().ForEach(delegate(string f)
			{
				object value = trv.Field(f).GetValue();
				if (value != null && (f != "priority" || (int)value != -1))
				{
					HarmonyMethodExtensions.SetValue(resultTrv, f, value);
				}
			});
		});
		return harmonyMethod;
	}

	/// <summary>Returns a string that represents the annotation</summary>
	/// <returns>A string representation</returns>
	public override string ToString()
	{
		string result = "";
		Traverse trv = Traverse.Create(this);
		HarmonyFields().ForEach(delegate(string f)
		{
			if (result.Length > 0)
			{
				result += ", ";
			}
			result += $"{f}={trv.Field(f).GetValue()}";
		});
		return "HarmonyMethod[" + result + "]";
	}

	internal string Description()
	{
		string value = (((object)declaringType != null) ? declaringType.FullName : "undefined");
		string value2 = methodName ?? "undefined";
		string value3 = (methodType.HasValue ? methodType.Value.ToString() : "undefined");
		string value4 = ((argumentTypes != null) ? argumentTypes.Description() : "undefined");
		return $"(class={value}, methodname={value2}, type={value3}, args={value4})";
	}

	/// <summary>Creates a patch from a given method</summary>
	/// <param name="method">The original method</param>
	public static implicit operator HarmonyMethod(MethodInfo method)
	{
		return new HarmonyMethod(method);
	}

	/// <summary>Creates a patch from a given method</summary>
	/// <param name="delegate">The original method</param>
	public static implicit operator HarmonyMethod(Delegate @delegate)
	{
		return new HarmonyMethod(@delegate);
	}
}
