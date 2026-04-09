using System;

namespace HarmonyLib;

/// <summary>Specifies the Finalizer function in a patch class</summary>
[AttributeUsage(AttributeTargets.Method)]
public class HarmonyFinalizer : Attribute
{
}
