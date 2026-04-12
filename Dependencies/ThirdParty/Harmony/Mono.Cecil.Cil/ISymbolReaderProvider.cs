using System.IO;

namespace Mono.Cecil.Cil;

internal interface ISymbolReaderProvider
{
	ISymbolReader GetSymbolReader(ModuleDefinition module, string fileName);

	ISymbolReader GetSymbolReader(ModuleDefinition module, Stream symbolStream);
}
