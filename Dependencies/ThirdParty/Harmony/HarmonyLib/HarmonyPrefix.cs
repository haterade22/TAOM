using System;

namespace HarmonyLib;

/// <summary>Specifies the Prefix function in a patch class</summary>
[AttributeUsage(AttributeTargets.Method)]
public class HarmonyPrefix : Attribute
{
}
