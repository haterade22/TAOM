using System;
using System.Collections.Generic;

namespace HarmonyLib;

/// <summary>Annotation to define your Harmony patch methods</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Delegate, AllowMultiple = true)]
public class HarmonyPatch : HarmonyAttribute
{
	/// <summary>An empty annotation can be used together with TargetMethod(s)</summary>
	public HarmonyPatch()
	{
	}

	/// <summary>An annotation that specifies a class to patch</summary>
	/// <param name="declaringType">The declaring class/type</param>
	public HarmonyPatch(Type declaringType)
	{
		info.declaringType = declaringType;
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="declaringType">The declaring class/type</param>
	/// <param name="argumentTypes">The argument types of the method or constructor to patch</param>
	public HarmonyPatch(Type declaringType, Type[] argumentTypes)
	{
		info.declaringType = declaringType;
		info.argumentTypes = argumentTypes;
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="declaringType">The declaring class/type</param>
	/// <param name="methodName">The name of the method, property or constructor to patch</param>
	public HarmonyPatch(Type declaringType, string methodName)
	{
		info.declaringType = declaringType;
		info.methodName = methodName;
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="declaringType">The declaring class/type</param>
	/// <param name="methodName">The name of the method, property or constructor to patch</param>
	/// <param name="argumentTypes">An array of argument types to target overloads</param>
	public HarmonyPatch(Type declaringType, string methodName, params Type[] argumentTypes)
	{
		info.declaringType = declaringType;
		info.methodName = methodName;
		info.argumentTypes = argumentTypes;
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="declaringType">The declaring class/type</param>
	/// <param name="methodName">The name of the method, property or constructor to patch</param>
	/// <param name="argumentTypes">An array of argument types to target overloads</param>
	/// <param name="argumentVariations">Array of <see cref="T:HarmonyLib.ArgumentType" /></param>
	public HarmonyPatch(Type declaringType, string methodName, Type[] argumentTypes, ArgumentType[] argumentVariations)
	{
		info.declaringType = declaringType;
		info.methodName = methodName;
		ParseSpecialArguments(argumentTypes, argumentVariations);
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="declaringType">The declaring class/type</param>
	/// <param name="methodType">The <see cref="T:HarmonyLib.MethodType" /></param>
	public HarmonyPatch(Type declaringType, MethodType methodType)
	{
		info.declaringType = declaringType;
		info.methodType = methodType;
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="declaringType">The declaring class/type</param>
	/// <param name="methodType">The <see cref="T:HarmonyLib.MethodType" /></param>
	/// <param name="argumentTypes">An array of argument types to target overloads</param>
	public HarmonyPatch(Type declaringType, MethodType methodType, params Type[] argumentTypes)
	{
		info.declaringType = declaringType;
		info.methodType = methodType;
		info.argumentTypes = argumentTypes;
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="declaringType">The declaring class/type</param>
	/// <param name="methodType">The <see cref="T:HarmonyLib.MethodType" /></param>
	/// <param name="argumentTypes">An array of argument types to target overloads</param>
	/// <param name="argumentVariations">Array of <see cref="T:HarmonyLib.ArgumentType" /></param>
	public HarmonyPatch(Type declaringType, MethodType methodType, Type[] argumentTypes, ArgumentType[] argumentVariations)
	{
		info.declaringType = declaringType;
		info.methodType = methodType;
		ParseSpecialArguments(argumentTypes, argumentVariations);
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="declaringType">The declaring class/type</param>
	/// <param name="methodName">The name of the method, property or constructor to patch</param>
	/// <param name="methodType">The <see cref="T:HarmonyLib.MethodType" /></param>
	public HarmonyPatch(Type declaringType, string methodName, MethodType methodType)
	{
		info.declaringType = declaringType;
		info.methodName = methodName;
		info.methodType = methodType;
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="methodName">The name of the method, property or constructor to patch</param>
	public HarmonyPatch(string methodName)
	{
		info.methodName = methodName;
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="methodName">The name of the method, property or constructor to patch</param>
	/// <param name="argumentTypes">An array of argument types to target overloads</param>
	public HarmonyPatch(string methodName, params Type[] argumentTypes)
	{
		info.methodName = methodName;
		info.argumentTypes = argumentTypes;
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="methodName">The name of the method, property or constructor to patch</param>
	/// <param name="argumentTypes">An array of argument types to target overloads</param>
	/// <param name="argumentVariations">An array of <see cref="T:HarmonyLib.ArgumentType" /></param>
	public HarmonyPatch(string methodName, Type[] argumentTypes, ArgumentType[] argumentVariations)
	{
		info.methodName = methodName;
		ParseSpecialArguments(argumentTypes, argumentVariations);
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="methodName">The name of the method, property or constructor to patch</param>
	/// <param name="methodType">The <see cref="T:HarmonyLib.MethodType" /></param>
	public HarmonyPatch(string methodName, MethodType methodType)
	{
		info.methodName = methodName;
		info.methodType = methodType;
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="methodType">The <see cref="T:HarmonyLib.MethodType" /></param>
	public HarmonyPatch(MethodType methodType)
	{
		info.methodType = methodType;
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="methodType">The <see cref="T:HarmonyLib.MethodType" /></param>
	/// <param name="argumentTypes">An array of argument types to target overloads</param>
	public HarmonyPatch(MethodType methodType, params Type[] argumentTypes)
	{
		info.methodType = methodType;
		info.argumentTypes = argumentTypes;
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="methodType">The <see cref="T:HarmonyLib.MethodType" /></param>
	/// <param name="argumentTypes">An array of argument types to target overloads</param>
	/// <param name="argumentVariations">An array of <see cref="T:HarmonyLib.ArgumentType" /></param>
	public HarmonyPatch(MethodType methodType, Type[] argumentTypes, ArgumentType[] argumentVariations)
	{
		info.methodType = methodType;
		ParseSpecialArguments(argumentTypes, argumentVariations);
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="argumentTypes">An array of argument types to target overloads</param>
	public HarmonyPatch(Type[] argumentTypes)
	{
		info.argumentTypes = argumentTypes;
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="argumentTypes">An array of argument types to target overloads</param>
	/// <param name="argumentVariations">An array of <see cref="T:HarmonyLib.ArgumentType" /></param>
	public HarmonyPatch(Type[] argumentTypes, ArgumentType[] argumentVariations)
	{
		ParseSpecialArguments(argumentTypes, argumentVariations);
	}

	/// <summary>An annotation that specifies a method, property or constructor to patch</summary>
	/// <param name="typeName">The full name of the declaring class/type</param>
	/// <param name="methodName">The name of the method, property or constructor to patch</param>
	/// <param name="methodType">The <see cref="T:HarmonyLib.MethodType" /></param>
	public HarmonyPatch(string typeName, string methodName, MethodType methodType = MethodType.Normal)
	{
		info.declaringType = AccessTools.TypeByName(typeName);
		info.methodName = methodName;
		info.methodType = methodType;
	}

	private void ParseSpecialArguments(Type[] argumentTypes, ArgumentType[] argumentVariations)
	{
		if (argumentVariations == null || argumentVariations.Length == 0)
		{
			info.argumentTypes = argumentTypes;
			return;
		}
		if (argumentTypes.Length < argumentVariations.Length)
		{
			throw new ArgumentException("argumentVariations contains more elements than argumentTypes", "argumentVariations");
		}
		List<Type> list = new List<Type>();
		for (int i = 0; i < argumentTypes.Length; i++)
		{
			Type type = argumentTypes[i];
			switch (argumentVariations[i])
			{
			case ArgumentType.Ref:
			case ArgumentType.Out:
				type = type.MakeByRefType();
				break;
			case ArgumentType.Pointer:
				type = type.MakePointerType();
				break;
			}
			list.Add(type);
		}
		info.argumentTypes = list.ToArray();
	}
}
