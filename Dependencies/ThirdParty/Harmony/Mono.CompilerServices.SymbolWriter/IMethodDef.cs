namespace Mono.CompilerServices.SymbolWriter;

internal interface IMethodDef
{
	string Name { get; }

	int Token { get; }
}
