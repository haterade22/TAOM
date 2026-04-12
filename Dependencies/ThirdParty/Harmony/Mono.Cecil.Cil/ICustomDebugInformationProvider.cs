using Mono.Collections.Generic;

namespace Mono.Cecil.Cil;

internal interface ICustomDebugInformationProvider : IMetadataTokenProvider
{
	bool HasCustomDebugInformations { get; }

	Collection<CustomDebugInformation> CustomDebugInformations { get; }
}
