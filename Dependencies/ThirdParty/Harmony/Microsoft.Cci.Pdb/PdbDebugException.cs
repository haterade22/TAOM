using System.IO;

namespace Microsoft.Cci.Pdb;

internal class PdbDebugException : IOException
{
	internal PdbDebugException(string format, params object[] args)
		: base(string.Format(format, args))
	{
	}
}
