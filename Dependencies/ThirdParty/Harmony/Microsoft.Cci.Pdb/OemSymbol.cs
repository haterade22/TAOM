using System;

namespace Microsoft.Cci.Pdb;

internal struct OemSymbol
{
	internal Guid idOem;

	internal uint typind;

	internal byte[] rgl;
}
