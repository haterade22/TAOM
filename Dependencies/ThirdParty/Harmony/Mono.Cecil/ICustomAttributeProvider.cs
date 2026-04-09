using Mono.Collections.Generic;

namespace Mono.Cecil;

internal interface ICustomAttributeProvider : IMetadataTokenProvider
{
	Collection<CustomAttribute> CustomAttributes { get; }

	bool HasCustomAttributes { get; }
}
