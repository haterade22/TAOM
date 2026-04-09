using System.Collections.Generic;

namespace Bannerlord.UIExtenderEx.Prefabs2;

public abstract class PrefabExtensionSetAttributePatch
{
	public readonly struct Attribute
	{
		public string Name { get; }

		public string Value { get; }

		public Attribute(string name, string value)
		{
			Name = name;
			Value = value;
		}
	}

	public abstract List<Attribute> Attributes { get; }
}
