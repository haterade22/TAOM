using System.Collections.Generic;

namespace System.Runtime.CompilerServices;

internal interface ICWTEnumerable<T>
{
	IEnumerable<T> SelfEnumerable { get; }

	IEnumerator<T> GetEnumerator();
}
