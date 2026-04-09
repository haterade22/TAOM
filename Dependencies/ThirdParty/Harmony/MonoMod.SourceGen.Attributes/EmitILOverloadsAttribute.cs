using System;

namespace MonoMod.SourceGen.Attributes;

[AttributeUsage(AttributeTargets.Class)]
internal sealed class EmitILOverloadsAttribute : Attribute
{
	public EmitILOverloadsAttribute(string filename, string kind)
	{
	}
}
