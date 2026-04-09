using System;

namespace HarmonyLib;

/// <summary>Annotation to define the original method for delegate injection</summary>
[AttributeUsage(AttributeTargets.Delegate, AllowMultiple = true)]
public class HarmonyDelegate : HarmonyPatch
{
	/// <summary>An annotation that specifies a class to patch</summary>
	/// <param name="declaringType">The declaring class/type</param>
	public HarmonyDelegate(Type declaringType)
		: base(declaringType)
	{
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="declaringType">The declaring class/type</param>
	/// <param name="argumentTypes">The argument types of the method or constructor to patch</param>
	public HarmonyDelegate(Type declaringType, Type[] argumentTypes)
		: base(declaringType, argumentTypes)
	{
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="declaringType">The declaring class/type</param>
	/// <param name="methodName">The name of the method, property or constructor to patch</param>
	public HarmonyDelegate(Type declaringType, string methodName)
		: base(declaringType, methodName)
	{
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="declaringType">The declaring class/type</param>
	/// <param name="methodName">The name of the method, property or constructor to patch</param>
	/// <param name="argumentTypes">An array of argument types to target overloads</param>
	public HarmonyDelegate(Type declaringType, string methodName, params Type[] argumentTypes)
		: base(declaringType, methodName, argumentTypes)
	{
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="declaringType">The declaring class/type</param>
	/// <param name="methodName">The name of the method, property or constructor to patch</param>
	/// <param name="argumentTypes">An array of argument types to target overloads</param>
	/// <param name="argumentVariations">Array of <see cref="T:HarmonyLib.ArgumentType" /></param>
	public HarmonyDelegate(Type declaringType, string methodName, Type[] argumentTypes, ArgumentType[] argumentVariations)
		: base(declaringType, methodName, argumentTypes, argumentVariations)
	{
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="declaringType">The declaring class/type</param>
	/// <param name="methodDispatchType">The <see cref="T:HarmonyLib.MethodDispatchType" /></param>
	public HarmonyDelegate(Type declaringType, MethodDispatchType methodDispatchType)
		: base(declaringType, MethodType.Normal)
	{
		info.nonVirtualDelegate = methodDispatchType == MethodDispatchType.Call;
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="declaringType">The declaring class/type</param>
	/// <param name="methodDispatchType">The <see cref="T:HarmonyLib.MethodDispatchType" /></param>
	/// <param name="argumentTypes">An array of argument types to target overloads</param>
	public HarmonyDelegate(Type declaringType, MethodDispatchType methodDispatchType, params Type[] argumentTypes)
		: base(declaringType, MethodType.Normal, argumentTypes)
	{
		info.nonVirtualDelegate = methodDispatchType == MethodDispatchType.Call;
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="declaringType">The declaring class/type</param>
	/// <param name="methodDispatchType">The <see cref="T:HarmonyLib.MethodDispatchType" /></param>
	/// <param name="argumentTypes">An array of argument types to target overloads</param>
	/// <param name="argumentVariations">Array of <see cref="T:HarmonyLib.ArgumentType" /></param>
	public HarmonyDelegate(Type declaringType, MethodDispatchType methodDispatchType, Type[] argumentTypes, ArgumentType[] argumentVariations)
		: base(declaringType, MethodType.Normal, argumentTypes, argumentVariations)
	{
		info.nonVirtualDelegate = methodDispatchType == MethodDispatchType.Call;
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="declaringType">The declaring class/type</param>
	/// <param name="methodName">The name of the method, property or constructor to patch</param>
	/// <param name="methodDispatchType">The <see cref="T:HarmonyLib.MethodDispatchType" /></param>
	public HarmonyDelegate(Type declaringType, string methodName, MethodDispatchType methodDispatchType)
		: base(declaringType, methodName, MethodType.Normal)
	{
		info.nonVirtualDelegate = methodDispatchType == MethodDispatchType.Call;
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="methodName">The name of the method, property or constructor to patch</param>
	public HarmonyDelegate(string methodName)
		: base(methodName)
	{
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="methodName">The name of the method, property or constructor to patch</param>
	/// <param name="argumentTypes">An array of argument types to target overloads</param>
	public HarmonyDelegate(string methodName, params Type[] argumentTypes)
		: base(methodName, argumentTypes)
	{
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="methodName">The name of the method, property or constructor to patch</param>
	/// <param name="argumentTypes">An array of argument types to target overloads</param>
	/// <param name="argumentVariations">An array of <see cref="T:HarmonyLib.ArgumentType" /></param>
	public HarmonyDelegate(string methodName, Type[] argumentTypes, ArgumentType[] argumentVariations)
		: base(methodName, argumentTypes, argumentVariations)
	{
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="methodName">The name of the method, property or constructor to patch</param>
	/// <param name="methodDispatchType">The <see cref="T:HarmonyLib.MethodDispatchType" /></param>
	public HarmonyDelegate(string methodName, MethodDispatchType methodDispatchType)
		: base(methodName, MethodType.Normal)
	{
		info.nonVirtualDelegate = methodDispatchType == MethodDispatchType.Call;
	}

	/// <summary>An annotation that specifies call dispatching mechanics for the delegate</summary>
	/// <param name="methodDispatchType">The <see cref="T:HarmonyLib.MethodDispatchType" /></param>
	public HarmonyDelegate(MethodDispatchType methodDispatchType)
	{
		info.nonVirtualDelegate = methodDispatchType == MethodDispatchType.Call;
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="methodDispatchType">The <see cref="T:HarmonyLib.MethodDispatchType" /></param>
	/// <param name="argumentTypes">An array of argument types to target overloads</param>
	public HarmonyDelegate(MethodDispatchType methodDispatchType, params Type[] argumentTypes)
		: base(MethodType.Normal, argumentTypes)
	{
		info.nonVirtualDelegate = methodDispatchType == MethodDispatchType.Call;
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="methodDispatchType">The <see cref="T:HarmonyLib.MethodDispatchType" /></param>
	/// <param name="argumentTypes">An array of argument types to target overloads</param>
	/// <param name="argumentVariations">An array of <see cref="T:HarmonyLib.ArgumentType" /></param>
	public HarmonyDelegate(MethodDispatchType methodDispatchType, Type[] argumentTypes, ArgumentType[] argumentVariations)
		: base(MethodType.Normal, argumentTypes, argumentVariations)
	{
		info.nonVirtualDelegate = methodDispatchType == MethodDispatchType.Call;
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="argumentTypes">An array of argument types to target overloads</param>
	public HarmonyDelegate(Type[] argumentTypes)
		: base(argumentTypes)
	{
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="argumentTypes">An array of argument types to target overloads</param>
	/// <param name="argumentVariations">An array of <see cref="T:HarmonyLib.ArgumentType" /></param>
	public HarmonyDelegate(Type[] argumentTypes, ArgumentType[] argumentVariations)
		: base(argumentTypes, argumentVariations)
	{
	}
}
