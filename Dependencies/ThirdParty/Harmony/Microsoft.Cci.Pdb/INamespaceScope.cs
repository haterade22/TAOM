using System.Collections.Generic;

namespace Microsoft.Cci.Pdb;

internal interface INamespaceScope
{
	IEnumerable<IUsedNamespace> UsedNamespaces { get; }
}
