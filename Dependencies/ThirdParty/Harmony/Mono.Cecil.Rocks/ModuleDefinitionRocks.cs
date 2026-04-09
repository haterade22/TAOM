using System;
using System.Collections.Generic;
using System.Linq;

namespace Mono.Cecil.Rocks;

internal static class ModuleDefinitionRocks
{
	public static IEnumerable<TypeDefinition> GetAllTypes(this ModuleDefinition self)
	{
		if (self == null)
		{
			throw new ArgumentNullException("self");
		}
		return self.Types.SelectMany(Functional.Y((Func<TypeDefinition, IEnumerable<TypeDefinition>> f) => (TypeDefinition type) => type.NestedTypes.SelectMany(f).Prepend(type)));
	}
}
