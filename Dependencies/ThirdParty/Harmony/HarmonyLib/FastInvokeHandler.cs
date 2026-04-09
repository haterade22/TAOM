namespace HarmonyLib;

/// <summary>A delegate to invoke a method</summary>
/// <param name="target">The instance</param>
/// <param name="parameters">The method parameters</param>
/// <returns>The method result</returns>
public delegate object FastInvokeHandler(object target, params object[] parameters);
