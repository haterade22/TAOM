using System;

namespace Microsoft.Cci.Pdb;

[Flags]
internal enum CV_SEPCODEFLAGS : uint
{
	fIsLexicalScope = 1u,
	fReturnsToParent = 2u
}
