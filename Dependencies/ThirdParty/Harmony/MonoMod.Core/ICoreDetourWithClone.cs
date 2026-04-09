using System;
using System.Reflection;
using MonoMod.Utils;

namespace MonoMod.Core;

internal interface ICoreDetourWithClone : ICoreDetour, ICoreDetourBase, IDisposable
{
	MethodInfo? SourceMethodClone { get; }

	DynamicMethodDefinition? SourceMethodCloneIL { get; }
}
