using System;

namespace HarmonyLib;

/// <summary>Specifies the Postfix function in a patch class</summary>
[AttributeUsage(AttributeTargets.Method)]
public class HarmonyPostfix : Attribute
{
}
