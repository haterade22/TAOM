using System;
using System.Collections.Generic;
using System.Linq;

namespace HarmonyLib;

/// <summary>General extensions for collections</summary>
public static class CollectionExtensions
{
	/// <summary>A simple way to execute code for every element in a collection</summary>
	/// <typeparam name="T">The inner type of the collection</typeparam>
	/// <param name="sequence">The collection</param>
	/// <param name="action">The action to execute</param>
	public static void Do<T>(this IEnumerable<T> sequence, Action<T> action)
	{
		if (sequence != null)
		{
			IEnumerator<T> enumerator = sequence.GetEnumerator();
			while (enumerator.MoveNext())
			{
				action(enumerator.Current);
			}
		}
	}

	/// <summary>A simple way to execute code for elements in a collection matching a condition</summary>
	/// <typeparam name="T">The inner type of the collection</typeparam>
	/// <param name="sequence">The collection</param>
	/// <param name="condition">The predicate</param>
	/// <param name="action">The action to execute</param>
	public static void DoIf<T>(this IEnumerable<T> sequence, Func<T, bool> condition, Action<T> action)
	{
		sequence.Where(condition).Do(action);
	}

	/// <summary>A helper to add an item to a collection</summary>
	/// <typeparam name="T">The inner type of the collection</typeparam>
	/// <param name="sequence">The collection</param>
	/// <param name="item">The item to add</param>
	/// <returns>The collection containing the item</returns>
	public static IEnumerable<T> AddItem<T>(this IEnumerable<T> sequence, T item)
	{
		return (sequence ?? Array.Empty<T>()).Concat(new T[1] { item });
	}

	/// <summary>A helper to add an item to an array</summary>
	/// <typeparam name="T">The inner type of the collection</typeparam>
	/// <param name="sequence">The array</param>
	/// <param name="item">The item to add</param>
	/// <returns>The array containing the item</returns>
	public static T[] AddToArray<T>(this T[] sequence, T item)
	{
		return sequence.AddItem(item).ToArray();
	}

	/// <summary>A helper to add items to an array</summary>
	/// <typeparam name="T">The inner type of the collection</typeparam>
	/// <param name="sequence">The array</param>
	/// <param name="items">The items to add</param>
	/// <returns>The array containing the items</returns>
	public static T[] AddRangeToArray<T>(this T[] sequence, T[] items)
	{
		List<T> list = new List<T>();
		list.AddRange(sequence ?? Enumerable.Empty<T>());
		list.AddRange(items);
		return list.ToArray();
	}

	internal static Dictionary<K, V> Merge<K, V>(this IEnumerable<KeyValuePair<K, V>> firstDict, params IEnumerable<KeyValuePair<K, V>>[] otherDicts)
	{
		Dictionary<K, V> dictionary = new Dictionary<K, V>();
		foreach (KeyValuePair<K, V> item in firstDict)
		{
			dictionary[item.Key] = item.Value;
		}
		foreach (IEnumerable<KeyValuePair<K, V>> enumerable in otherDicts)
		{
			foreach (KeyValuePair<K, V> item2 in enumerable)
			{
				dictionary[item2.Key] = item2.Value;
			}
		}
		return dictionary;
	}

	internal static Dictionary<K, V> TransformKeys<K, V>(this Dictionary<K, V> origDict, Func<K, K> transform)
	{
		Dictionary<K, V> dictionary = new Dictionary<K, V>();
		foreach (KeyValuePair<K, V> item in origDict)
		{
			dictionary.Add(transform(item.Key), item.Value);
		}
		return dictionary;
	}
}
