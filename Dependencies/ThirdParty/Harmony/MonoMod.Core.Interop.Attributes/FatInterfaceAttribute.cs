using System;

namespace MonoMod.Core.Interop.Attributes;

[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false)]
internal sealed class FatInterfaceAttribute : Attribute
{
}
