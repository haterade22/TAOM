using System;
using Mono.Collections.Generic;

namespace Mono.Cecil.Cil;

internal interface ISymbolReader : IDisposable
{
	ISymbolWriterProvider GetWriterProvider();

	bool ProcessDebugHeader(ImageDebugHeader header);

	MethodDebugInformation Read(MethodDefinition method);

	Collection<CustomDebugInformation> Read(ICustomDebugInformationProvider provider);
}
