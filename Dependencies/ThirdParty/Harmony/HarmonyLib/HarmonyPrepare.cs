using System;

namespace HarmonyLib;

/// <summary>Specifies the Prepare function in a patch class</summary>
[AttributeUsage(AttributeTargets.Method)]
public class HarmonyPrepare : Attribute
{
}
