using System;

namespace Mono.Cecil;

internal interface IAssemblyResolver : IDisposable
{
	AssemblyDefinition Resolve(AssemblyNameReference name);

	AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters);
}
