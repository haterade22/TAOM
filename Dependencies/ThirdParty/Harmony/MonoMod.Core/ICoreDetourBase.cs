using System;

namespace MonoMod.Core;

internal interface ICoreDetourBase : IDisposable
{
	bool IsApplied { get; }

	void Apply();

	void Undo();
}
