using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace HarmonyLib;

/// <summary>Patch serialization</summary>
internal static class PatchInfoSerialization
{
	private class Binder : SerializationBinder
	{
		/// <summary>Control the binding of a serialized object to a type</summary>
		/// <param name="assemblyName">Specifies the assembly name of the serialized object</param>
		/// <param name="typeName">Specifies the type name of the serialized object</param>
		/// <returns>The type of the object the formatter creates a new instance of</returns>
		public override Type BindToType(string assemblyName, string typeName)
		{
			Type[] array = new Type[3]
			{
				typeof(PatchInfo),
				typeof(Patch[]),
				typeof(Patch)
			};
			Type[] array2 = array;
			foreach (Type type in array2)
			{
				if (typeName == type.FullName)
				{
					return type;
				}
			}
			return Type.GetType($"{typeName}, {assemblyName}");
		}
	}

	internal static readonly BinaryFormatter binaryFormatter = new BinaryFormatter
	{
		Binder = new Binder()
	};

	/// <summary>Serializes a patch info</summary>
	/// <param name="patchInfo">The <see cref="T:HarmonyLib.PatchInfo" /></param>
	/// <returns>The serialized data</returns>
	internal static byte[] Serialize(this PatchInfo patchInfo)
	{
		using MemoryStream memoryStream = new MemoryStream();
		binaryFormatter.Serialize(memoryStream, patchInfo);
		return memoryStream.ToArray();
	}

	/// <summary>Deserialize a patch info</summary>
	/// <param name="bytes">The serialized data</param>
	/// <returns>A <see cref="T:HarmonyLib.PatchInfo" /></returns>
	internal static PatchInfo Deserialize(byte[] bytes)
	{
		using MemoryStream serializationStream = new MemoryStream(bytes);
		return (PatchInfo)binaryFormatter.Deserialize(serializationStream);
	}

	/// <summary>Compare function to sort patch priorities</summary>
	/// <param name="obj">The patch</param>
	/// <param name="index">Zero-based index</param>
	/// <param name="priority">The priority</param>
	/// <returns>A standard sort integer (-1, 0, 1)</returns>
	internal static int PriorityComparer(object obj, int index, int priority)
	{
		Traverse traverse = Traverse.Create(obj);
		int value = traverse.Field("priority").GetValue<int>();
		int value2 = traverse.Field("index").GetValue<int>();
		if (priority != value)
		{
			return -priority.CompareTo(value);
		}
		return index.CompareTo(value2);
	}
}
