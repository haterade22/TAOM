using System;

namespace HarmonyLib;

/// <summary>Specifies the Transpiler function in a patch class</summary>
[AttributeUsage(AttributeTargets.Method)]
public class HarmonyTranspiler : Attribute
{
}
