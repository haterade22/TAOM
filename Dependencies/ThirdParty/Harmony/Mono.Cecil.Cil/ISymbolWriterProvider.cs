using System.IO;

namespace Mono.Cecil.Cil;

internal interface ISymbolWriterProvider
{
	ISymbolWriter GetSymbolWriter(ModuleDefinition module, string fileName);

	ISymbolWriter GetSymbolWriter(ModuleDefinition module, Stream symbolStream);
}
