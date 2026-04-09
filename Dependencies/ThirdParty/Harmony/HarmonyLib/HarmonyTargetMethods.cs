using System;

namespace HarmonyLib;

/// <summary>Specifies the TargetMethods function in a patch class</summary>
[AttributeUsage(AttributeTargets.Method)]
public class HarmonyTargetMethods : Attribute
{
}
