using System;

namespace Microsoft.Cci.Pdb;

[Flags]
internal enum FRAMEDATA_FLAGS : uint
{
	fHasSEH = 1u,
	fHasEH = 2u,
	fIsFunctionStart = 4u
}
