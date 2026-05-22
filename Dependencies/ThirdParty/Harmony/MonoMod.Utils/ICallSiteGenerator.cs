using Mono.Cecil;

namespace MonoMod.Utils;

internal interface ICallSiteGenerator
{
	CallSite ToCallSite(ModuleDefinition module);
}
