# Thread-Local and Cache Patterns

Two patterns for Harmony patches and hot paths. Hook, Strategy and GameModel overrides are in `.claude/rules/csharp-patterns.md`.

## Thread-Local State Pattern for Multi-Patch Coordination

### When to Use

Use this pattern when:
- Multiple Harmony patches need to coordinate and share context
- A Prefix patch needs to pass data to a Postfix patch
- One patch sets context that another patch consumes
- No GameModel override point exists for the desired behavior
- Patches operate on related but separate method calls

### Pattern Structure

```csharp
// Patch 1: Context Capture - Stores data in thread-local storage
[HarmonyPatch(typeof(SomeGameClass), "MethodWithParameters")]
public class ContextCapture_Patch
{
    /// <summary>
    /// Thread-local storage for sharing context between patches.
    /// Each thread has its own copy, preventing race conditions.
    /// </summary>
    [ThreadStatic]
    private static SomeContext? _currentContext;

    /// <summary>
    /// Public accessor for other patches to read the context.
    /// </summary>
    public static SomeContext? CurrentContext => _currentContext;

    /// <summary>
    /// Prefix: Capture parameters and store in thread-local storage.
    /// </summary>
    public static void Prefix(ParamType param1, ParamType param2, ...)
    {
        // Store parameters for later consumption
        _currentContext = new SomeContext(param1, param2);
    }

    /// <summary>
    /// Finalizer: CRITICAL - Always cleanup thread-local storage.
    /// A Postfix does not run when the original throws.
    /// </summary>
    public static void Finalizer()
    {
        // MUST set to null to prevent data leaking to unrelated operations
        _currentContext = null;
    }
}

// Patch 2: Context Consumer - Reads from thread-local storage
[HarmonyPatch(typeof(OtherGameClass), "RelatedMethod")]
public class ContextConsumer_Patch
{
    /// <summary>
    /// Prefix: Use context from thread-local storage to make decisions.
    /// Returns false to skip original method execution.
    /// </summary>
    public static bool Prefix()
    {
        // Retrieve context from thread-local storage
        var context = ContextCapture_Patch.CurrentContext;

        // CRITICAL: Always check for null (safety)
        if (context == null)
        {
            return true; // No context available, allow normal processing
        }

        // Use context to make decisions
        var hooks = IoC.ResolveAll<ISomeHook>();
        foreach (var hook in hooks)
        {
            if (!hook.ShouldProceed(context))
            {
                return false; // Block original method execution
            }
        }

        return true; // Allow normal processing
    }
}
```

### Critical Rules

1. **Always Cleanup in a Finalizer**: Set thread-static fields to `null` in a finalizer, never a Postfix: a Postfix does not run when the original throws, so state leaks across unrelated operations
2. **Null Safety**: Consumer patches MUST check for null context before use
3. **Thread Isolation**: `[ThreadStatic]` ensures each thread has its own copy (safe for parallel execution)
4. **Minimal Scope**: Only use when patches MUST coordinate; prefer simpler patterns when possible
5. **Public Accessor**: Provide read-only property for other patches to access context
6. **Documentation**: Document the coordination between patches clearly


### When NOT to Use

- **GameModel exists**: Use GameModel override instead (see [architecture.md](architecture.md))
- **Single patch suffices**: Don't add complexity if one patch can handle everything
- **Parameters available**: If the method you're patching has the data you need, use it directly
- **Global state acceptable**: If data should persist beyond single operation, use service-level state

## Cache Pattern for Performance Optimization

### When to Use

Use caching when:
- Operation is called frequently (hot paths: combat, AI, per-frame)
- Underlying operation is expensive (dictionary lookups, string operations, reflection)
- Data is relatively stable within a scope (mission, campaign)
- Thread safety is required for concurrent access

### Cache Pattern Structure

```csharp
// Interface
public interface IMyCache
{
    TValue Get(TKey key);
    void Clear();
}

// Implementation with ConcurrentDictionary
public class MyCache : IMyCache
{
    private readonly ConcurrentDictionary<TKey, TValue> _cache = new();
    private readonly IExpensiveService _service;

    public MyCache(IExpensiveService service)
    {
        _service = service;
    }

    public TValue Get(TKey key)
    {
        return _cache.GetOrAdd(key, k => _service.ExpensiveOperation(k));
    }

    public void Clear()
    {
        _cache.Clear();
    }
}

// IoC Registration
container.Register<IMyCache, MyCache>(Reuse.Singleton);
```

### Critical Rules

1. **Thread Safety**: Always use `ConcurrentDictionary` for caches accessed from multiple threads
   ```csharp
   // ✓ Thread-safe
   private readonly ConcurrentDictionary<K, V> _cache = new();

   // ✗ Not thread-safe
   private readonly Dictionary<K, V> _cache = new();
   ```

2. **Null Safety**: Check for null inputs before caching
   ```csharp
   if (key == null) return defaultValue;
   ```

3. **Cleanup**: Clear mission-scoped caches when missions end
   ```csharp
   public void ClearMissionScopedCache() { _missionCache.Clear(); }
   ```

4. **Singleton Lifetime**: Register caches as singletons for shared access
   ```csharp
   container.Register<ICache, Cache>(Reuse.Singleton);
   ```

5. **LRU Eviction**: Never clear ALL cache entries on overflow - use LRU eviction instead
   ```csharp
   // ✗ ANTI-PATTERN: Full cache clear causes thrashing
   if (_cache.Count > MaxSize) _cache.Clear();

   // ✓ CORRECT: Remove oldest 25% on overflow
   if (_cache.Count > MaxSize)
       RemoveOldestEntries(_cache.Count / 4);
   ```

6. **Avoid O(n) in Hot Paths**: Never iterate all entities in frequently-called methods
   ```csharp
   // ✗ ANTI-PATTERN: O(n) per call × thousands of calls
   return Campaign.Current.Factions.Count(f => IsAllied(f));

   // ✓ CORRECT: Cache computed metrics, invalidate on changes
   return _factionMetricsCache.GetAlliedCount(faction);
   ```

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/agent-operating-manual.md](./agent-operating-manual.md)
- [docs/ai-includes/agent-teams.md](./agent-teams.md)
- [docs/ai-includes/architecture.md](./architecture.md)
- [docs/ai-includes/code-quality.md](./code-quality.md)
- [docs/ai-includes/taleworlds-research-guide.md](./taleworlds-research-guide.md)
- [docs/ai-includes/testing-guide.md](./testing-guide.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)

<!-- backlinks-end -->
