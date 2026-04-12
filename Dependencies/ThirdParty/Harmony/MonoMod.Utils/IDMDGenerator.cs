using System.Reflection;

namespace MonoMod.Utils;

internal interface IDMDGenerator
{
	MethodInfo Generate(DynamicMethodDefinition dmd, object? context);
}
