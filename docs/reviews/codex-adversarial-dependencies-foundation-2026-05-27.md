# Codex Adversarial Review: TAOM.Dependencies/Foundation (DR3 Phase 4)

Date: 2026-05-27  
Scope: `Dependencies/Foundation/*.cs`, `Dependencies/SubModule.cs`, `Dependencies/AliasStubSubModule.cs`, dependency/stub manifests.

## Summary Table

| # | Suspect / Finding | Verdict | Severity | Recommendation |
|---|---|---:|---:|---|
| S1 | PatchShield owner-filter `TAOM` prefix only | CONFIRMED | HIGH | Protect vendored infrastructure Harmony owners before unpatching. |
| S2 | SaveShield engine-prefix completeness vs `TAOM_Online` | CONFIRMED | MEDIUM | Replace broad `TAOM` prefix with exact TAOM-owned checks and add missing infrastructure assembly prefixes. |
| S3 | VersionProbe null-arg `FromParametersFile` safety | DISPUTED | INFO | No change. Decompiled API handles `null` by loading default `Parameters/Version.xml`. |
| S4 | CollectAssemblyTypesShim finalizer `ref Type[] __result` legality | DISPUTED | INFO | No change. Harmony 2.4.2 finalizers use the same `__result` injection path and by-ref result mutation is legal. |
| S5 | SubModuleConstructionGuard AddSubModule attribution via `TargetSite` | CONFIRMED | HIGH | Attribute AddSubModule failures from `subModuleInfo` + `subModuleAssembly`, not `Exception.TargetSite`. |
| S6 | PatchShield double-install + self-shielding | DISPUTED | INFO | No change. The `_shielded` set makes pass 2 idempotent and counters are process-global. |
| S7 | IncompatibleModDetector comment-strip regex | DISPUTED | INFO | No change required. It handles legal multiline XML comments; malformed XML edge cases are best-effort only. |
| S8 | SaveShield dedupe + repeat-failure visibility | DISPUTED | INFO | No change. Per-session catalog dedupe is by distinct failure shape; counters and diag log preserve repeat visibility. |
| A1 | IncompatibleModDetector snapshots installed modules, not enabled modules | CONFIRMED | MEDIUM | Use TaleWorlds active module state when available; keep directory scan as fallback. |
| A2 | PatchShield `SwallowedOther` counts exceptions it rethrows | CONFIRMED | LOW | Stop incrementing swallowed counters for non-swallowed exception types, or rename/count separately. |
| A3 | PatchShield unpatch-dedupe key ignores overload signatures | CONFIRMED | LOW | Include metadata token/module/signature in the `_unpatched` key. |

## Findings

[HIGH] `Dependencies/Foundation/PatchShield.cs:248` — Owner Protection — `TryUnpatchOffendingPatches` protects only Harmony owners beginning with `TAOM`, so the first shielded `MissingMethodException` on a method patched by vendored BUTR/MCM infrastructure can unpatch that infrastructure owner — Add an explicit protected-owner allowlist for TAOM plus vendored infrastructure owner prefixes.

Evidence:

```csharp
// Dependencies/Foundation/PatchShield.cs:245-258
if (owner == HarmonyId) continue;

if (owner.StartsWith("TAOM", StringComparison.OrdinalIgnoreCase))
{
    DiagLog.Log(Tag, $"refusing to unpatch TAOM-owned owner '{owner}' on {targetKey}");
    continue;
}

PatchSafe(originalMethod, owner);
DiagLog.Log(Tag, $"unpatched owner '{owner}' on {targetKey}");
```

Vendored infrastructure Harmony owners do not start with `TAOM`:

```csharp
// Decompiled vendored Bannerlord.ButterLib.dll
new Harmony("Bannerlord.ButterLib.SubModuleWrappers2");
new Harmony("Bannerlord.ButterLib.ExceptionHandler.BEW");
new Harmony("butterlib.delayedsubmoduleloader.static");

// Decompiled vendored Bannerlord.ButterLib.Implementation.1.4.0.dll
new Harmony("Bannerlord.ButterLib.SaveSystem");
new Harmony("Bannerlord.ButterLib.ObjectSystem");
new Harmony("Bannerlord.ButterLib.MBSubModuleBaseEx");

// Decompiled vendored Bannerlord.MBOptionScreen.v1.4.0.dll
new Harmony("MCM.UI.Adapter.MCMv5");
new Harmony("bannerlord.mcm.ui.optionsgauntletscreenpatch");
```

Recommended fix:

```csharp
private static readonly string[] ProtectedOwnerPrefixes =
{
    "TAOM",
    "Bannerlord.ButterLib",
    "butterlib.",
    "Bannerlord.UIExtenderEx",
    "Bannerlord.MBOptionScreen",
    "Bannerlord.ModuleLoader.Bannerlord.MBOptionScreen",
    "Bannerlord.MCM",
    "bannerlord.mcm.",
    "MCM",
    "MCMv5",
    "MCM.UI.Adapter",
    "BUTR.",
    "HarmonyLib.",
    "0Harmony"
};

private static bool IsProtectedOwner(string owner)
{
    return ProtectedOwnerPrefixes.Any(prefix =>
        owner.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}

// in TryUnpatchOffendingPatches
if (owner == HarmonyId || IsProtectedOwner(owner))
{
    DiagLog.Log(Tag, $"refusing to unpatch protected owner '{owner}' on {targetKey}");
    continue;
}
```

[HIGH] `Dependencies/Foundation/SubModuleConstructionGuard.cs:177` — Exception Attribution — The `Module.AddSubModule` path attributes constructor failures via `ex.TargetSite`, which points at the method that threw, not necessarily the offending submodule constructor. A TAOM constructor that throws inside TaleWorlds code can be swallowed because the guard sees the TaleWorlds throwing method instead of the TAOM submodule — Resolve the intended submodule type from `subModuleInfo` and `subModuleAssembly` before falling back to stack walking.

Evidence:

```csharp
// Dependencies/Foundation/SubModuleConstructionGuard.cs:177-184
declTypeName = ex.TargetSite?.DeclaringType?.FullName ?? "(unknown constructor)";
asmName = ex.TargetSite?.DeclaringType?.Assembly.GetName().Name ?? "(unknown)";

if (asmName.StartsWith("TAOM", StringComparison.OrdinalIgnoreCase))
{
    return __exception;
}
```

Vanilla `Module.AddSubModule` has the exact arguments needed for attribution:

```csharp
// Decompiled TaleWorlds.MountAndBlade.Module.AddSubModule
private AssemblyLoader.AssemblyLoadResult AddSubModule(
    SubModuleInfo subModuleInfo,
    Assembly subModuleAssembly)
{
    ConstructorInfo constructor =
        subModuleAssembly.GetType(subModuleInfo.SubModuleClassTypeName)
            .GetConstructor(..., new Type[0], null);
    ...
    MBSubModuleBase value = (MBSubModuleBase)constructor.Invoke(new object[0]);
    _subModuleBases.Add(subModuleInfo, value);
    return assemblyLoadResult;
}
```

In the scenario `FooMod.SubModule.ctor -> MBObjectManager.GetObject<T>("missing") -> throw`, `ex.TargetSite` is the TaleWorlds method that throws, while the real culprit is still `FooMod.SubModule`. The current TAOM guard therefore protects the wrong assembly.

Recommended fix:

```csharp
private static Exception? SwallowFinalizer(object __instance, object[] __args, Exception __exception)
{
    if (__exception == null) return null;

    var ex = Unwrap(__exception);
    string asmName;
    string declTypeName;

    if (__instance is MBSubModuleBase subMod)
    {
        var t = subMod.GetType();
        asmName = t.Assembly.GetName().Name ?? "(unknown)";
        declTypeName = t.FullName ?? "(unknown)";
    }
    else if (TryResolveAddSubModuleTarget(__args, out asmName, out declTypeName))
    {
        // resolved from vanilla AddSubModule inputs
    }
    else
    {
        TryAttributeFirstNonEngineFrame(ex, out asmName, out declTypeName);
    }

    if (asmName.StartsWith("TAOM", StringComparison.OrdinalIgnoreCase))
        return __exception;

    DiagLog.Log(Tag, $"swallowed {ex.GetType().Name} constructing {declTypeName} from {asmName}: {ex.Message}");
    return null;
}

private static bool TryResolveAddSubModuleTarget(object[] args, out string asmName, out string declTypeName)
{
    asmName = "(unknown)";
    declTypeName = "(unknown)";

    if (args == null || args.Length < 2 || !(args[1] is Assembly subModuleAssembly))
        return false;

    var info = args[0];
    var className = info?.GetType().GetProperty("SubModuleClassTypeName")?.GetValue(info) as string;
    var type = string.IsNullOrEmpty(className)
        ? null
        : subModuleAssembly.GetType(className, throwOnError: false);

    if (type == null) return false;
    asmName = type.Assembly.GetName().Name ?? "(unknown)";
    declTypeName = type.FullName ?? "(unknown)";
    return true;
}
```

[MEDIUM] `Dependencies/Foundation/SaveShield.cs:98` — Attribution Filter — `_enginePrefixes` uses the broad prefix `TAOM`, so a third-party consumer assembly named `TAOM_Online` or `TAOM_Map` is treated as engine/infrastructure and skipped during culprit attribution. The same list also misses bundled infrastructure assemblies such as `Bannerlord.MBOptionScreen`, `Bannerlord.ModuleLoader.Bannerlord.MBOptionScreen`, `MCM.UI.Adapter.MCMv5`, and `BUTR.CrashReport` — Replace the broad TAOM prefix with exact TAOM-owned checks and add missing infrastructure prefixes.

Evidence:

```csharp
// Dependencies/Foundation/SaveShield.cs:93-99
private static readonly string[] _enginePrefixes = new[]
{
    "TaleWorlds.", "SandBox", "StoryMode", "CustomBattle",
    "TAOM", "Bannerlord.Harmony", "Bannerlord.UIExtenderEx",
    "Bannerlord.ButterLib", "MCMv5", "0Harmony", "HarmonyLib",
    ...
};

// Dependencies/Foundation/SaveShield.cs:273-277
var isEngine = _enginePrefixes.Any(prefix =>
    asmName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
if (isEngine) continue;
```

Bundled dependency assemblies include names not covered by the current filter:

```text
Dependencies/_Module/bin/Win64_Shipping_Client/Bannerlord.MBOptionScreen.v1.4.0.dll
Dependencies/_Module/bin/Win64_Shipping_Client/Bannerlord.ModuleLoader.Bannerlord.MBOptionScreen.dll
Dependencies/_Module/bin/Win64_Shipping_Client/BUTR.CrashReport.dll
Dependencies/_Module/bin/Win64_Shipping_Client/MCM.UI.Adapter.MCMv5.dll
```

Recommended fix:

```csharp
private static readonly string[] _enginePrefixes =
{
    "TaleWorlds.", "SandBox", "StoryMode", "CustomBattle",
    "Bannerlord.Harmony", "Bannerlord.UIExtenderEx", "Bannerlord.ButterLib",
    "Bannerlord.MBOptionScreen", "Bannerlord.ModuleLoader.Bannerlord.MBOptionScreen",
    "MCMv5", "MCM.UI.Adapter", "BUTR.CrashReport",
    "0Harmony", "HarmonyLib", "Mono.Cecil", "MonoMod",
    "System.", "Microsoft.", "mscorlib", "Newtonsoft.Json", "Serilog"
};

private static bool IsEngineAssembly(string asmName)
{
    if (string.Equals(asmName, "TAOM", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(asmName, "TAOM.Dependencies", StringComparison.OrdinalIgnoreCase) ||
        asmName.StartsWith("TAOM.", StringComparison.OrdinalIgnoreCase))
        return true;

    return _enginePrefixes.Any(prefix =>
        asmName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
```

[MEDIUM] `Dependencies/Foundation/IncompatibleModDetector.cs:184` — Crash-Loop Attribution — `ReadCurrentModlist` snapshots every installed module folder under `Modules/`, while the detector comments and log messages say it diffs enabled/re-enabled modules. Enabling an already-installed mod after a last-good launch will not appear as newly enabled, so the culprit analysis can incorrectly report "no new mods" — Use TaleWorlds active module state when available and keep the folder scan as a fallback.

Evidence:

```csharp
// Dependencies/Foundation/IncompatibleModDetector.cs:14-16
/// reaching main menu — likely because a newly-enabled mod is incompatible.
/// Diffs the current modlist against <c>last-good-modlist.txt</c>

// Dependencies/Foundation/IncompatibleModDetector.cs:145
var newlyEnabled = current.Where(m => !lastGood.Contains(m)).ToList();

// Dependencies/Foundation/IncompatibleModDetector.cs:184-208
foreach (var dir in Directory.GetDirectories(modulesRoot))
{
    var subModuleXml = Path.Combine(dir, "SubModule.xml");
    ...
    if (idMatch.Success) result.Add(idMatch.Groups[1].Value);
}
```

Vanilla module state distinguishes active modules:

```csharp
// Decompiled TaleWorlds.MountAndBlade.Module excerpt
foreach (ModuleInfo allModule in ModuleHelper.GetAllModules())
{
    if (!allModule.IsActive)
        continue;
    ...
}
```

Recommended fix:

```csharp
private static List<string> ReadCurrentModlist()
{
    var active = TryReadActiveModuleIds();
    if (active.Count > 0)
        return active;

    return ReadInstalledModuleIdsFromFolders();
}

private static List<string> TryReadActiveModuleIds()
{
    var result = new List<string>();
    try
    {
        var helper = ReflectionUtils.FindTypeAcrossLoadedAssemblies(
            "TaleWorlds.ModuleManager.ModuleHelper");
        var getActive = helper?.GetMethod("GetActiveModules",
            BindingFlags.Public | BindingFlags.Static);
        var modules = getActive?.Invoke(null, null) as System.Collections.IEnumerable;
        if (modules == null) return result;

        foreach (var module in modules)
        {
            var id = module.GetType().GetProperty("Id")?.GetValue(module) as string;
            if (!string.IsNullOrWhiteSpace(id)) result.Add(id);
        }
    }
    catch { }

    return result;
}
```

[LOW] `Dependencies/Foundation/PatchShield.cs:210` — Diagnostic Counters — `ShouldSwallow` increments `_swallowedOther` immediately before returning `false`, so `SwallowedTotal` and the process-exit summary can report non-swallowed exceptions as swallowed — Do not count rethrown exception types in swallowed counters.

Evidence:

```csharp
// Dependencies/Foundation/PatchShield.cs:190-211
if (ex is MissingMethodException || ex is MissingFieldException || ex is TypeLoadException)
{
    ...
    return true;
}

Interlocked.Increment(ref _swallowedOther);
return false;
```

Recommended fix:

```csharp
// Non-target exception types are deliberately rethrown.
return false;
```

If visibility for rethrown exceptions is useful, add a separate `ObservedOther` counter and keep it out of `SwallowedTotal`.

[LOW] `Dependencies/Foundation/PatchShield.cs:221` — Overload-Safe Deduplication — `_unpatched` uses only `DeclaringType::Name`, so overloaded target methods share a dedupe key. If two overloads fail in the same process, the second overload can skip cleanup because the first overload already marked the name — Include module/token or full signature in the key.

Evidence:

```csharp
// Dependencies/Foundation/PatchShield.cs:218-228
string targetKey;
try
{
    targetKey = (originalMethod.DeclaringType?.FullName ?? "?") + "::" + originalMethod.Name;
}
...
if (_unpatched.Contains(targetKey)) return;
_unpatched.Add(targetKey);
```

Recommended fix:

```csharp
private static string GetTargetKey(MethodBase originalMethod)
{
    try
    {
        return $"{originalMethod.Module.ModuleVersionId}:{originalMethod.MetadataToken}";
    }
    catch
    {
        return originalMethod.ToString();
    }
}
```

## Per-Suspect Evidence

### S1: PatchShield Owner Filter

Verdict: CONFIRMED, HIGH.

TAOM source protects only `TAOM` owners:

```csharp
// Dependencies/Foundation/PatchShield.cs:245-258
if (owner == HarmonyId) continue;
if (owner.StartsWith("TAOM", StringComparison.OrdinalIgnoreCase)) continue;
PatchSafe(originalMethod, owner);
```

Vendored BUTR/MCM owners are not covered:

```csharp
// Decompiled vendored assemblies
new Harmony("Bannerlord.ButterLib.SaveSystem");
new Harmony("Bannerlord.ButterLib.ObjectSystem");
new Harmony("MCM.UI.Adapter.MCMv5");
new Harmony("bannerlord.mcm.ui.optionsvm");
```

Conclusion: current filter is too narrow for the vendored defensive foundation. See HIGH finding above.

### S2: SaveShield Engine Prefixes

Verdict: CONFIRMED, MEDIUM.

The broad `TAOM` prefix filters unrelated `TAOM_*` consumer assemblies:

```csharp
// Dependencies/Foundation/SaveShield.cs:93-99
"TAOM", "Bannerlord.Harmony", "Bannerlord.UIExtenderEx",
"Bannerlord.ButterLib", "MCMv5", "0Harmony", "HarmonyLib",
```

The culprit walk skips any prefix match:

```csharp
// Dependencies/Foundation/SaveShield.cs:273-277
var isEngine = _enginePrefixes.Any(prefix =>
    asmName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
if (isEngine) continue;
```

Conclusion: too aggressive for `TAOM_Online`/`TAOM_Map`, while still incomplete for bundled BUTR/MCM assemblies. See MEDIUM finding above.

### S3: VersionProbe Null-Arg FromParametersFile

Verdict: DISPUTED, INFO.

TAOM invokes the one-arg overload with `null`:

```csharp
// Dependencies/Foundation/VersionProbe.cs:78-104
var fromFile = appVerType.GetMethods(...)
    .FirstOrDefault(m => m.Name == "FromParametersFile" &&
                         m.GetParameters().Length == 1 &&
                         m.GetParameters()[0].ParameterType == typeof(string));
var version = fromFile.Invoke(null, new object?[] { null });
```

Decompiled v1.4.5 `TaleWorlds.Library.ApplicationVersion.FromParametersFile` handles null explicitly:

```csharp
public static ApplicationVersion FromParametersFile(string customParameterFilePath = null)
{
    string filePath = ((customParameterFilePath == null)
        ? (BasePath.Name + "Parameters/Version.xml")
        : customParameterFilePath);
    string fileContent = VirtualFolders.GetFileContent(filePath);
    if (fileContent == "") return Empty;
    ...
}
```

Conclusion: null is the intended default-parameter path. No bug.

### S4: CollectAssemblyTypesShim Finalizer `ref Type[] __result`

Verdict: DISPUTED, INFO.

TAOM finalizer:

```csharp
// Dependencies/Foundation/CollectAssemblyTypesShim.cs:100-109
private static Exception? GetTypesFinalizer(
    Assembly __instance,
    ref Type[] __result,
    Exception __exception)
{
    ...
    __result = partial;
    return null;
}
```

Harmony 2.4.2 includes finalizers in the same fix/injection set as prefixes and postfixes:

```csharp
// Decompiled 0Harmony 2.4.2
internal List<MethodInfo> Fixes =>
    prefixes.Union(postfixes).Union(finalizers).ToList();

injections = Fixes.Union(...).ToDictionary(... fix.GetParameters() ...);
```

Harmony 2.4.2 handles by-ref `__result` injection by loading the result local address:

```csharp
// Decompiled 0Harmony 2.4.2 MethodCreatorTools.EmitCallParameter
case InjectionType.Result:
    if (type.IsByRef && !returnType.IsByRef)
        type = type.GetElementType();
    if (!type.IsAssignableFrom(returnType))
        throw new Exception(...);
    OpCode opcode = ((parameterType.IsByRef && !returnType.IsByRef)
        ? OpCodes.Ldloca
        : OpCodes.Ldloc);
```

Official Harmony docs also state finalizers use the same injected-value mechanism as postfixes:

- https://harmony.pardeike.net/articles/patching-finalizer.html
- https://harmony.pardeike.net/articles/patching-postfix.html

Conclusion: `ref Type[] __result` is legal in Lib.Harmony/0Harmony 2.4.2 and assignment mutates the wrapper result. No CRITICAL finding.

### S5: SubModuleConstructionGuard TargetSite Attribution

Verdict: CONFIRMED, HIGH.

The source currently uses `ex.TargetSite` for the `Module.AddSubModule` path:

```csharp
// Dependencies/Foundation/SubModuleConstructionGuard.cs:177-180
declTypeName = ex.TargetSite?.DeclaringType?.FullName ?? "(unknown constructor)";
asmName = ex.TargetSite?.DeclaringType?.Assembly.GetName().Name ?? "(unknown)";
```

Vanilla invokes the third-party constructor via reflection:

```csharp
// Decompiled TaleWorlds.MountAndBlade.Module.AddSubModule
ConstructorInfo constructor =
    subModuleAssembly.GetType(subModuleInfo.SubModuleClassTypeName)
        .GetConstructor(..., new Type[0], null);
MBSubModuleBase value = (MBSubModuleBase)constructor.Invoke(new object[0]);
```

For a constructor body that calls a TaleWorlds API which throws, `Exception.TargetSite` is the throwing TaleWorlds method, not the third-party constructor. Attribution should use the vanilla `subModuleInfo` and `subModuleAssembly` arguments. See HIGH finding above.

### S6: PatchShield Double Install And Self-Shielding

Verdict: DISPUTED, INFO.

TAOM calls PatchShield twice:

```csharp
// Dependencies/SubModule.cs:184,233
PatchShield.Install();
...
PatchShield.Install();
```

The pass is idempotent by original method:

```csharp
// Dependencies/Foundation/PatchShield.cs:102-123
foreach (var method in Harmony.GetAllPatchedMethods().ToList())
{
    ...
    lock (_lock)
    {
        if (_shielded.Contains(method)) continue;
        _shielded.Add(method);
    }
}
```

Harmony returns every patched original method, including finalizer-only patched methods:

```csharp
// Decompiled 0Harmony 2.4.2
public static IEnumerable<MethodBase> GetAllPatchedMethods()
{
    return PatchProcessor.GetAllPatchedMethods();
}

public static IEnumerable<MethodBase> GetAllPatchedMethods()
{
    lock (locker)
    {
        return HarmonySharedState.GetPatchedMethods();
    }
}
```

Counters are static and process-wide, and the process-exit summary is registered before pass 2:

```csharp
// Dependencies/SubModule.cs:205-207
AppDomain.CurrentDomain.ProcessExit += (_, __) =>
{
    PatchShield.WriteSessionSummary();
};
```

Conclusion: pass 2 sees finalizer-only patches but does not re-shield already tracked methods. Counters include events from both passes.

### S7: IncompatibleModDetector Comment Regex

Verdict: DISPUTED, INFO.

TAOM strips XML comments before matching `<Id>`:

```csharp
// Dependencies/Foundation/IncompatibleModDetector.cs:203-208
var stripped = System.Text.RegularExpressions.Regex.Replace(
    text, @"<!--[\s\S]*?-->", string.Empty);
var idMatch = System.Text.RegularExpressions.Regex.Match(
    stripped, @"<Id\s+value\s*=\s*""([^""]+)""\s*/>");
```

`[\s\S]*?` handles multiline legal XML comments. XML does not allow nested comments or embedded `-->` inside a comment. BOM-marked text is handled by `File.ReadAllText`'s normal encoding detection; unreadable or malformed files are already skipped by the inner `catch`.

Conclusion: no bug for legal SubModule XML. Malformed-comment handling would be a robustness enhancement, not a finding.

### S8: SaveShield Dedupe

Verdict: DISPUTED, INFO.

Catalog dedupe is by culprit, exception type, and owner method:

```csharp
// Dependencies/Foundation/FailedModsCatalog.cs:37-41
var dedupeKey = $"{rec.CulpritAssembly}|{rec.ExceptionType}|{rec.OwnerType}.{rec.OwnerMethod}";
lock (_lock)
{
    if (_sessionSeen.Contains(dedupeKey)) return;
    _sessionSeen.Add(dedupeKey);
}
```

SaveShield still increments counters for every swallowed exception:

```csharp
// Dependencies/Foundation/SaveShield.cs:219-224
if (ex is ArgumentException || ex is KeyNotFoundException)
    Interlocked.Increment(ref _swallowedDuplicateKey);
else
    Interlocked.Increment(ref _swallowedOther);
Interlocked.Increment(ref _swallowedCount);
```

Conclusion: one catalog line per distinct failure shape is intentional and keeps the file usable. Repeats remain visible in counters and diag log. Different exception types from the same culprit correctly produce distinct entries.

## Vanilla Code

### ApplicationVersion.FromParametersFile

Target matches `VersionProbe.DetectViaApplicationVersion`, and null is safe:

```csharp
// Decompiled TaleWorlds.Library.ApplicationVersion
public static ApplicationVersion FromParametersFile(string customParameterFilePath = null)
{
    string filePath = ((customParameterFilePath == null)
        ? (BasePath.Name + "Parameters/Version.xml")
        : customParameterFilePath);
    XmlDocument xmlDocument = new XmlDocument();
    string fileContent = VirtualFolders.GetFileContent(filePath);
    if (fileContent == "") return Empty;
    xmlDocument.LoadXml(fileContent);
    return FromString(xmlDocument.ChildNodes[0].ChildNodes[0]
        .Attributes["Value"].InnerText);
}
```

### Module.AddSubModule

Target matches `SubModuleConstructionGuard` reflection:

```csharp
// Decompiled TaleWorlds.MountAndBlade.Module
private AssemblyLoader.AssemblyLoadResult AddSubModule(
    SubModuleInfo subModuleInfo,
    Assembly subModuleAssembly)
{
    ConstructorInfo constructor =
        subModuleAssembly.GetType(subModuleInfo.SubModuleClassTypeName)
            .GetConstructor(BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.CreateInstance,
                null, new Type[0], null);
    ...
    MBSubModuleBase value = (MBSubModuleBase)constructor.Invoke(new object[0]);
    _subModuleBases.Add(subModuleInfo, value);
    return assemblyLoadResult;
}
```

### MBSubModuleBase Constructor

The parameterless constructor exists and is protected/compiler-generated:

```il
// Decompiled IL: TaleWorlds.MountAndBlade.MBSubModuleBase::.ctor
IL_0000: ldarg.0
IL_0001: call instance void [netstandard]System.Object::.ctor()
IL_0006: ret
```

Reflection output:

```text
IsPublic: False
IsFamily: True
Signature: Void .ctor()
```

### Harmony.GetAllPatchedMethods

Target matches PatchShield pass enumeration:

```csharp
// Decompiled 0Harmony 2.4.2
public static IEnumerable<MethodBase> GetAllPatchedMethods()
{
    return PatchProcessor.GetAllPatchedMethods();
}

public static IEnumerable<MethodBase> GetAllPatchedMethods()
{
    lock (locker)
    {
        return HarmonySharedState.GetPatchedMethods();
    }
}
```

### SaveManager.Load

Target exists; name-only reflection patches both overloads:

```csharp
// Decompiled TaleWorlds.SaveSystem.SaveManager
public static LoadResult Load(string saveName, ISaveDriver driver)
{
    return Load(saveName, driver, loadAsLateInitialize: false);
}

public static LoadResult Load(string saveName, ISaveDriver driver, bool loadAsLateInitialize)
{
    _isLoading = true;
    ...
    LoadData loadData = driver.Load(saveName);
    ...
    _isLoading = false;
    OperatingVersion = ApplicationVersion.Empty;
    return loadResult;
}
```

Other SaveShield save targets verified:

```csharp
// Decompiled TaleWorlds.Core.MBSaveLoad
public static LoadResult LoadSaveGameData(string saveName) { ... }

// Decompiled SandBox.SandBoxSaveHelper
private static void LoadGameAction(SaveGameFileInfo saveInfo,
    Action<LoadResult> onStartGame, Action onCancel) { ... }
public static bool TryLoadSave(SaveGameFileInfo saveInfo, Action<LoadResult> onStartGame) { ... }

// Decompiled TaleWorlds.SaveSystem.Load.LoadResult
public void InitializeObjects() { _loadCallbackInitializator.InitializeObjects(); }
public void AfterInitializeObjects() { _loadCallbackInitializator.AfterInitializeObjects(); }
```

### MissionState Targets

Targets match SaveShield reflection:

```csharp
// Decompiled TaleWorlds.MountAndBlade.MissionState
protected override void OnInitialize()
{
    base.OnInitialize();
    Current = this;
    FirstMissionTickAfterLoading = true;
    LoadingWindow.EnableGlobalLoadingWindow();
}

private void FinishMissionLoading()
{
    _missionInitializing = false;
    CurrentMission.Scene.SetOwnerThread();
    ...
    Handler?.OnMissionLoadingFinished(CurrentMission);
    CurrentMission.Scene.ResumeLoadingRenderings();
}
```

### Mission Targets

Targets match SaveShield reflection:

```csharp
// Decompiled TaleWorlds.MountAndBlade.Mission
public void Initialize() { ... }

public void SetMissionMode(MissionMode newMode, bool atStart) { ... }

public Agent SpawnTroop(IAgentOriginBase troopOrigin, bool isPlayerSide,
    bool hasFormation, bool spawnWithHorse, bool isReinforcement,
    int formationTroopCount, int formationTroopIndex, bool isAlarmed,
    bool wieldInitialWeapons, Vec3? initialPosition, Vec2? initialDirection,
    string specialActionSetSuffix = null, ItemObject bannerItem = null,
    FormationClass formationIndex = FormationClass.NumberOfAllFormations,
    bool useTroopClassForSpawn = false)
```

## Reflection-Target Analysis

- `ApplicationVersion.FromParametersFile(string customParameterFilePath = null)`: confirmed. Null invocation is safe.
- `Module.AddSubModule(SubModuleInfo subModuleInfo, Assembly subModuleAssembly)`: confirmed private instance target.
- `MBSubModuleBase` parameterless constructor: confirmed protected parameterless constructor.
- SaveShield target list: all requested targets exist in v1.4.5. The implementation has 11 named target entries, and `SaveManager.Load` expands to two overload MethodBases, so the "10 methods" prompt wording is stale but not a runtime bug.
- `TaleWorlds.SaveSystem.Load.LoadResult`: confirmed nested namespace/type path; there is no top-level `TaleWorlds.SaveSystem.LoadResult` in this build.

## Finalizer-Signature Analysis

The `CollectAssemblyTypesShim` finalizer shape is legal for Harmony 2.4.2:

```csharp
private static Exception? GetTypesFinalizer(
    Assembly __instance,
    ref Type[] __result,
    Exception __exception)
```

Reasoning:

- Harmony finalizers are part of `Fixes`, the same injection set used for prefixes and postfixes.
- `__result` maps to `InjectionType.Result`.
- By-ref patch parameters receive the result local address via `Ldloca`.
- Returning `null` from the finalizer suppresses the original exception, so the assigned partial array is returned to the caller.

No switch to postfix or pass-through postfix is required for Lib.Harmony/0Harmony 2.4.2.

## Exception-Attribution Analysis

Hypothetical path:

1. `FooMod.SubModule` constructor calls `MBObjectManager.GetObject<Hero>("nonexistent")`.
2. The TaleWorlds API throws.
3. Reflection wraps the constructor failure in `TargetInvocationException`.
4. `SubModuleConstructionGuard` unwraps to the inner exception.
5. `ex.TargetSite` is the throwing TaleWorlds API method, not `FooMod.SubModule..ctor`.

Therefore the current `TargetSite` attribution is not reliable for the `Module.AddSubModule` patch. The vanilla method provides `subModuleInfo.SubModuleClassTypeName` and `subModuleAssembly`; those are the authoritative source for the constructed submodule type.

## Race-Condition Analysis

`DiagLog.Write`:

```csharp
// Dependencies/Foundation/DiagLog.cs:23,51-63
private static readonly object _lock = new object();
...
lock (_lock)
{
    File.AppendAllText(path, line + Environment.NewLine);
}
...
catch { }
```

No practical deadlock found. The logger takes only its private lock and the filesystem; it does not acquire engine locks or call back into TaleWorlds. C# `lock` is reentrant on the same thread, so recursive logging on the same thread does not deadlock. The main cost is possible file I/O latency while a finalizer runs under another subsystem's lock, not lock-order inversion.

`PatchShield._shielded`:

```csharp
// Dependencies/Foundation/PatchShield.cs:115-123
lock (_lock)
{
    if (_shielded.Contains(method)) continue;
    _shielded.Add(method);
}
```

No practical reentrancy bug found. A theoretical Harmony patch on `HashSet<T>.Add` would be pathological, and the monitor is reentrant for same-thread recursion. Both install passes serialize on the same lock and produce an idempotent shield set.

## Config Cross-Reference

Stub version strategy matches the requested v99 pinning:

```text
Stubs/Bannerlord.Harmony/_Module/SubModule.xml: Version v2.4.99.0
Stubs/Bannerlord.UIExtenderEx/_Module/SubModule.xml: Version v2.13.99.0
Stubs/Bannerlord.ButterLib/_Module/SubModule.xml: Version v2.10.99.0
Stubs/Bannerlord.MBOptionScreen/_Module/SubModule.xml: Version v5.11.99.0
```

Canonical alias-stub SubModuleClassType is present:

```xml
<!-- Stubs/Bannerlord.Harmony/_Module/SubModule.xml -->
<SubModuleClassType value="TAOM.Dependencies.AliasStubSubModule"/>
```

Opt-out flags match implementation:

```csharp
// Dependencies/Foundation/PatchShield.cs:36
private const string DisableFlagName = "patchshield-disabled.flag";

// Dependencies/Foundation/SaveShield.cs:44
private const string SwallowDisableFlagName = "saveshield-swallow-disabled.flag";
```

No XML/JSON config drift found.

## Closing Summary

CRITICAL: 0 | HIGH: 2 | MEDIUM: 2 | LOW: 2  
VERDICT: ISSUES FOUND

No unit-test finding raised per instruction. The missing coverage remains a practical risk for rare exception paths, but the core reflection targets and the highest-risk Harmony finalizer signature are verified against installed v1.4.5 and 0Harmony 2.4.2.
