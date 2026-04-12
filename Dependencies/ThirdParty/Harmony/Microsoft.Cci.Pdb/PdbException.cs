using System.IO;

namespace Microsoft.Cci.Pdb;

internal class PdbException : IOException
{
	internal PdbException(string format, params object[] args)
		: base(string.Format(format, args))
	{
	}
}
