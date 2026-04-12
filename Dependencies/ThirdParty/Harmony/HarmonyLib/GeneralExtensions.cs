using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace HarmonyLib;

/// <summary>General extensions for common cases</summary>
public static class GeneralExtensions
{
	/// <summary>Joins an enumeration with a value converter and a delimiter to a string</summary>
	/// <typeparam name="T">The inner type of the enumeration</typeparam>
	/// <param name="enumeration">The enumeration</param>
	/// <param name="converter">An optional value converter (from T to string)</param>
	/// <param name="delimiter">An optional delimiter</param>
	/// <returns>The values joined into a string</returns>
	public static string Join<T>(this IEnumerable<T> enumeration, Func<T, string> converter = null, string delimiter = ", ")
	{
		if (converter == null)
		{
			converter = (T t) => t.ToString();
		}
		return enumeration.Aggregate("", (string prev, T curr) => prev + ((prev.Length > 0) ? delimiter : "") + converter(curr));
	}

	/// <summary>Converts an array of types (for example methods arguments) into a human readable form</summary>
	/// <param name="parameters">The array of types</param>
	/// <returns>A human readable description including brackets</returns>
	public static string Description(this Type[] parameters)
	{
		if (parameters == null)
		{
			return "NULL";
		}
		return "(" + parameters.Join((Type p) => p.FullDescription()) + ")";
	}

	/// <summary>A full description of a type</summary>
	/// <param name="type">The type</param>
	/// <returns>A human readable description</returns>
	public static string FullDescription(this Type type)
	{
		if ((object)type == null)
		{
			return "null";
		}
		string text = type.Namespace;
		if (!string.IsNullOrEmpty(text))
		{
			text += ".";
		}
		string text2 = text + type.Name;
		if (type.IsGenericType)
		{
			text2 += "<";
			Type[] genericArguments = type.GetGenericArguments();
			for (int i = 0; i < genericArguments.Length; i++)
			{
				if (!text2.EndsWith("<", StringComparison.Ordinal))
				{
					text2 += ", ";
				}
				text2 += genericArguments[i].FullDescription();
			}
			text2 += ">";
		}
		return text2;
	}

	/// <summary>A a full description of a method or a constructor without assembly details but with generics</summary>
	/// <param name="member">The method/constructor</param>
	/// <returns>A human readable description</returns>
	public static string FullDescription(this MethodBase member)
	{
		if ((object)member == null)
		{
			return "null";
		}
		Type returnedType = AccessTools.GetReturnedType(member);
		StringBuilder stringBuilder = new StringBuilder();
		if (member.IsStatic)
		{
			stringBuilder.Append("static ");
		}
		if (member.IsAbstract)
		{
			stringBuilder.Append("abstract ");
		}
		if (member.IsVirtual)
		{
			stringBuilder.Append("virtual ");
		}
		stringBuilder.Append(returnedType.FullDescription() + " ");
		if ((object)member.DeclaringType != null)
		{
			stringBuilder.Append(member.DeclaringType.FullDescription() + "::");
		}
		string text = member.GetParameters().Join((ParameterInfo p) => p.ParameterType.FullDescription() + " " + p.Name);
		stringBuilder.Append(member.Name + "(" + text + ")");
		return stringBuilder.ToString();
	}

	/// <summary>A helper converting parameter infos to types</summary>
	/// <param name="pinfo">The array of parameter infos</param>
	/// <returns>An array of types</returns>
	public static Type[] Types(this ParameterInfo[] pinfo)
	{
		return pinfo.Select((ParameterInfo pi) => pi.ParameterType).ToArray();
	}

	/// <summary>Tests if a type has the <see cref="T:HarmonyLib.HarmonyAttribute" /></summary>
	/// <param name="type">The class/type to test</param>
	/// <returns>True if the type has the <see cref="T:HarmonyLib.HarmonyAttribute" /></returns>
	public static bool HasHarmonyAttribute(this Type type)
	{
		if ((object)type == null)
		{
			throw new ArgumentNullException("type");
		}
		return HarmonyMethodExtensions.GetFromType(type).Count > 0;
	}

	/// <summary>A helper to access a value via key from a dictionary</summary>
	/// <typeparam name="S">The key type</typeparam>
	/// <typeparam name="T">The value type</typeparam>
	/// <param name="dictionary">The dictionary</param>
	/// <param name="key">The key</param>
	/// <returns>The value for the key or the default value (of T) if that key does not exist</returns>
	public static T GetValueSafe<S, T>(this Dictionary<S, T> dictionary, S key)
	{
		if (dictionary.TryGetValue(key, out var value))
		{
			return value;
		}
		return default(T);
	}

	/// <summary>A helper to access a value via key from a dictionary with extra casting</summary>
	/// <typeparam name="T">The value type</typeparam>
	/// <param name="dictionary">The dictionary</param>
	/// <param name="key">The key</param>
	/// <returns>The value for the key or the default value (of T) if that key does not exist or cannot be cast to T</returns>
	public static T GetTypedValue<T>(this Dictionary<string, object> dictionary, string key)
	{
		if (dictionary.TryGetValue(key, out var value) && value is T)
		{
			return (T)value;
		}
		return default(T);
	}

	/// <summary>Escapes Unicode and ASCII non printable characters</summary>
	/// <param name="input">The string to convert</param>
	/// <param name="quoteChar">The string to convert</param>
	/// <returns>A string literal surrounded by <paramref name="quoteChar" /></returns>
	public static string ToLiteral(this string input, string quoteChar = "\"")
	{
		StringBuilder stringBuilder = new StringBuilder(input.Length + 2);
		stringBuilder.Append(quoteChar);
		foreach (char c in input)
		{
			switch (c)
			{
			case '\'':
				stringBuilder.Append("\\'");
				continue;
			case '"':
				stringBuilder.Append("\\\"");
				continue;
			case '\\':
				stringBuilder.Append("\\\\");
				continue;
			case '\0':
				stringBuilder.Append("\\0");
				continue;
			case '\a':
				stringBuilder.Append("\\a");
				continue;
			case '\b':
				stringBuilder.Append("\\b");
				continue;
			case '\f':
				stringBuilder.Append("\\f");
				continue;
			case '\n':
				stringBuilder.Append("\\n");
				continue;
			case '\r':
				stringBuilder.Append("\\r");
				continue;
			case '\t':
				stringBuilder.Append("\\t");
				continue;
			case '\v':
				stringBuilder.Append("\\v");
				continue;
			}
			if (c >= ' ' && c <= '~')
			{
				stringBuilder.Append(c);
				continue;
			}
			stringBuilder.Append("\\u");
			int num = c;
			stringBuilder.Append(num.ToString("x4"));
		}
		stringBuilder.Append(quoteChar);
		return stringBuilder.ToString();
	}
}
