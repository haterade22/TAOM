using Mono.Collections.Generic;

namespace Mono.Cecil;

internal interface ISecurityDeclarationProvider : IMetadataTokenProvider
{
	bool HasSecurityDeclarations { get; }

	Collection<SecurityDeclaration> SecurityDeclarations { get; }
}
