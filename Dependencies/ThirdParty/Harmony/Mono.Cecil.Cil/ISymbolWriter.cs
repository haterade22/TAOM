using System;

namespace Mono.Cecil.Cil;

internal interface ISymbolWriter : IDisposable
{
	ISymbolReaderProvider GetReaderProvider();

	ImageDebugHeader GetDebugHeader();

	void Write(MethodDebugInformation info);

	void Write();

	void Write(ICustomDebugInformationProvider provider);
}
