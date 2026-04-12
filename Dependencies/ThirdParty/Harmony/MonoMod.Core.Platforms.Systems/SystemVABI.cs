using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using MonoMod.Utils;

namespace MonoMod.Core.Platforms.Systems;

internal static class SystemVABI
{
	private static readonly ConditionalWeakTable<Type, StrongBox<bool>> SysVIsMemoryCache = new ConditionalWeakTable<Type, StrongBox<bool>>();

	private static readonly StrongBox<bool> SBTrue = new StrongBox<bool>(value: true);

	private static readonly StrongBox<bool> SBFalse = new StrongBox<bool>(value: false);

	public static TypeClassification ClassifyAMD64(Type type, bool isReturn)
	{
		int managedSize = type.GetManagedSize();
		if (managedSize > 16)
		{
			if (managedSize > 32)
			{
				if (!isReturn)
				{
					return TypeClassification.OnStack;
				}
				return TypeClassification.ByReference;
			}
			if (true)
			{
				if (!isReturn)
				{
					return TypeClassification.OnStack;
				}
				return TypeClassification.ByReference;
			}
		}
		return TypeClassification.InRegister;
	}

	public static TypeClassification ClassifyARM64(Type type, bool isReturn)
	{
		int managedSize = type.GetManagedSize();
		if (managedSize > 16)
		{
			if (managedSize > 32)
			{
				if (!isReturn)
				{
					return TypeClassification.OnStack;
				}
				return TypeClassification.ByReference;
			}
			if (AnyFieldsNotFloat(type))
			{
				if (!isReturn)
				{
					return TypeClassification.OnStack;
				}
				return TypeClassification.ByReference;
			}
		}
		return TypeClassification.InRegister;
	}

	private static bool AnyFieldsNotFloat(Type type)
	{
		return SysVIsMemoryCache.GetValue(type, delegate(Type type2)
		{
			FieldInfo[] fields = type2.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			for (int i = 0; i < fields.Length; i++)
			{
				Type fieldType = fields[i].FieldType;
				if ((object)fieldType != null && !fieldType.IsPrimitive && fieldType.IsValueType && AnyFieldsNotFloat(fieldType))
				{
					return SBTrue;
				}
				TypeCode typeCode = Type.GetTypeCode(fieldType);
				if (typeCode != TypeCode.Single && typeCode != TypeCode.Double)
				{
					return SBTrue;
				}
			}
			return SBFalse;
		}).Value;
	}
}
