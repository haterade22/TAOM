using System;

namespace HarmonyLib;

/// <summary>Specifies the Cleanup function in a patch class</summary>
[AttributeUsage(AttributeTargets.Method)]
public class HarmonyCleanup : Attribute
{
}
