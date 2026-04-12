using Mono.Collections.Generic;

namespace Mono.Cecil;

internal interface IGenericInstance : IMetadataTokenProvider
{
	bool HasGenericArguments { get; }

	Collection<TypeReference> GenericArguments { get; }
}
