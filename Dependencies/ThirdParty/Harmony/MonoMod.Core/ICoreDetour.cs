using System;
using System.Reflection;

namespace MonoMod.Core;

[CLSCompliant(true)]
internal interface ICoreDetour : ICoreDetourBase, IDisposable
{
	MethodBase Source { get; }

	MethodBase Target { get; }
}
