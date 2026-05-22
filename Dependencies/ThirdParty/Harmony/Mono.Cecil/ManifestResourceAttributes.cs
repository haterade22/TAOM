using System;

namespace Mono.Cecil;

[Flags]
internal enum ManifestResourceAttributes : uint
{
	VisibilityMask = 7u,
	Public = 1u,
	Private = 2u
}
