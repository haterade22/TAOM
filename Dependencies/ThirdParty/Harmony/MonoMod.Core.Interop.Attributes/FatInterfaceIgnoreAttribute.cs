using System;

namespace MonoMod.Core.Interop.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class FatInterfaceIgnoreAttribute : Attribute
{
}
