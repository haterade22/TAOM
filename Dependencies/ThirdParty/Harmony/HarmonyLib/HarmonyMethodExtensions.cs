using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HarmonyLib;

/// <summary>Annotation extensions</summary>
public static class HarmonyMethodExtensions
{
	internal static void SetValue(Traverse trv, string name, object val)
	{
		if (val != null)
		{
			Traverse traverse = trv.Field(name);
			if (name == "methodType" || name == "reversePatchType")
			{
				Type underlyingType = Nullable.GetUnderlyingType(traverse.GetValueType());
				val = Enum.ToObject(underlyingType, (int)val);
			}
			traverse.SetValue(val);
		}
	}

	/// <summary>Copies annotation information</summary>
	/// <param name="from">The source <see cref="T:HarmonyLib.HarmonyMethod" /></param>
	/// <param name="to">The destination <see cref="T:HarmonyLib.HarmonyMethod" /></param>
	public static void CopyTo(this HarmonyMethod from, HarmonyMethod to)
	{
		if (to == null)
		{
			return;
		}
		Traverse fromTrv = Traverse.Create(from);
		Traverse toTrv = Traverse.Create(to);
		HarmonyMethod.HarmonyFields().ForEach(delegate(string f)
		{
			object value = fromTrv.Field(f).GetValue();
			if (value != null)
			{
				SetValue(toTrv, f, value);
			}
		});
	}

	/// <summary>Clones an annotation</summary>
	/// <param name="original">The <see cref="T:HarmonyLib.HarmonyMethod" /> to clone</param>
	/// <returns>A copied <see cref="T:HarmonyLib.HarmonyMethod" /></returns>
	public static HarmonyMethod Clone(this HarmonyMethod original)
	{
		HarmonyMethod harmonyMethod = new HarmonyMethod();
		original.CopyTo(harmonyMethod);
		return harmonyMethod;
	}

	/// <summary>Merges annotations</summary>
	/// <param name="master">The master <see cref="T:HarmonyLib.HarmonyMethod" /></param>
	/// <param name="detail">The detail <see cref="T:HarmonyLib.HarmonyMethod" /></param>
	/// <returns>A new, merged <see cref="T:HarmonyLib.HarmonyMethod" /></returns>
	public static HarmonyMethod Merge(this HarmonyMethod master, HarmonyMethod detail)
	{
		if (detail == null)
		{
			return master;
		}
		HarmonyMethod harmonyMethod = new HarmonyMethod();
		Traverse resultTrv = Traverse.Create(harmonyMethod);
		Traverse masterTrv = Traverse.Create(master);
		Traverse detailTrv = Traverse.Create(detail);
		HarmonyMethod.HarmonyFields().ForEach(delegate(string f)
		{
			object value = masterTrv.Field(f).GetValue();
			object value2 = detailTrv.Field(f).GetValue();
			if (f != "priority")
			{
				SetValue(resultTrv, f, value2 ?? value);
			}
			else
			{
				int num = (int)value;
				int num2 = (int)value2;
				int num3 = Math.Max(num, num2);
				if (num == -1 && num2 != -1)
				{
					num3 = num2;
				}
				if (num != -1 && num2 == -1)
				{
					num3 = num;
				}
				SetValue(resultTrv, f, num3);
			}
		});
		return harmonyMethod;
	}

	private static HarmonyMethod GetHarmonyMethodInfo(object attribute)
	{
		FieldInfo field = attribute.GetType().GetField("info", AccessTools.all);
		if ((object)field == null)
		{
			return null;
		}
		if (field.FieldType.FullName != PatchTools.harmonyMethodFullName)
		{
			return null;
		}
		object value = field.GetValue(attribute);
		return AccessTools.MakeDeepCopy<HarmonyMethod>(value);
	}

	/// <summary>Gets all annotations on a class/type</summary>
	/// <param name="type">The class/type</param>
	/// <returns>A list of all <see cref="T:HarmonyLib.HarmonyMethod" /></returns>
	public static List<HarmonyMethod> GetFromType(Type type)
	{
		return (from info in type.GetCustomAttributes(inherit: true).Select(GetHarmonyMethodInfo)
			where info != null
			select info).ToList();
	}

	/// <summary>Gets merged annotations on a class/type</summary>
	/// <param name="type">The class/type</param>
	/// <returns>The merged <see cref="T:HarmonyLib.HarmonyMethod" /></returns>
	public static HarmonyMethod GetMergedFromType(Type type)
	{
		return HarmonyMethod.Merge(GetFromType(type));
	}

	/// <summary>Gets all annotations on a method</summary>
	/// <param name="method">The method/constructor</param>
	/// <returns>A list of <see cref="T:HarmonyLib.HarmonyMethod" /></returns>
	public static List<HarmonyMethod> GetFromMethod(MethodBase method)
	{
		return (from info in method.GetCustomAttributes(inherit: true).Select(GetHarmonyMethodInfo)
			where info != null
			select info).ToList();
	}

	/// <summary>Gets merged annotations on a method</summary>
	/// <param name="method">The method/constructor</param>
	/// <returns>The merged <see cref="T:HarmonyLib.HarmonyMethod" /></returns>
	public static HarmonyMethod GetMergedFromMethod(MethodBase method)
	{
		return HarmonyMethod.Merge(GetFromMethod(method));
	}
}
