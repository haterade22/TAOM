using System;
using System.Reflection;

namespace MonoMod.Core.Platforms;

internal delegate void OnMethodCompiledCallback(RuntimeMethodHandle methodHandle, MethodBase? method, IntPtr codeStart, IntPtr codeRw, ulong codeSize);
