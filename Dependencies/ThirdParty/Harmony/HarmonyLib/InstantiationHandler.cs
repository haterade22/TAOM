namespace HarmonyLib;

/// <summary>A constructor delegate type</summary>
/// <typeparam name="T">Type that constructor creates</typeparam>
/// <returns>An delegate</returns>
public delegate T InstantiationHandler<out T>();
