using System;

namespace HarmonyLib;

/// <summary>Specifies the TargetMethod function in a patch class</summary>
[AttributeUsage(AttributeTargets.Method)]
public class HarmonyTargetMethod : Attribute
{
}
