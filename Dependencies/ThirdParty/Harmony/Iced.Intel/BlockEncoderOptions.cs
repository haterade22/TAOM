using System;

namespace Iced.Intel;

[Flags]
internal enum BlockEncoderOptions
{
	None = 0,
	DontFixBranches = 1,
	ReturnRelocInfos = 2,
	ReturnNewInstructionOffsets = 4,
	ReturnConstantOffsets = 8
}
