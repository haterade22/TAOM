using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.Map.DistanceCache;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Core.Logging;
using TAOM.Features.EditorCacheRebuild;

namespace TAOM.Adapters;

public sealed class NavigationCacheAdapter : INavigationCacheAdapter
{
    private readonly object _cacheInstance;
    private readonly IModLogger? _logger;
    private readonly Type _closedCacheType;
    private readonly Type _settlementRecordType;

    private readonly FieldInfo _distanceField;
    private readonly FieldInfo _neighborField;
    private readonly PropertyInfo _navTypeProperty;

    private readonly MethodInfo _getAllRegisteredSettlements;
    private readonly MethodInfo _getFortificationsForNeighborDetection;
    private readonly MethodInfo _addClosestEntrancePairBase;
    private readonly MethodInfo _checkBeingNeighbor2Arg;
    private readonly MethodInfo _addNeighbor;
    private readonly MethodInfo _getCacheElementByRecord;
    private readonly MethodInfo _getRealDistance;
    private readonly MethodInfo _setSettlementDistance;
    private readonly MethodInfo _sortElements;
    private readonly MethodInfo _generateClosestSettlementCache;
    private readonly MethodInfo _getSceneCrcValues;
    private readonly MethodInfo _serializeMethod;
    private readonly MethodInfo _deserializeMethod;
    private readonly object _writeLock = new();

    // Per-thread reusable argument arrays for reflection invocations.
    // Hot Phase 1 path allocates ~2.2M object[] per full build (~20-30 MB GC churn)
    // without these. Each thread reuses one array per arity (2/3/4) for the duration.
    // Safe because none of our reflection targets invoke callbacks that re-enter the adapter.
    [ThreadStatic] private static object[]? _args2;
    [ThreadStatic] private static object[]? _args3;
    [ThreadStatic] private static object[]? _args4;

    private static object[] Args2() => _args2 ??= new object[2];
    private static object[] Args3() => _args3 ??= new object[3];
    private static object[] Args4() => _args4 ??= new object[4];

    public NavigationCacheAdapter(object cacheInstance, IModLogger? logger = null)
    {
        _cacheInstance = cacheInstance ?? throw new ArgumentNullException(nameof(cacheInstance));
        _logger = logger;
        var concreteType = cacheInstance.GetType();
        _closedCacheType = concreteType.BaseType
            ?? throw new InvalidOperationException("Cache instance has no base type");

        if (!_closedCacheType.IsGenericType ||
            _closedCacheType.GetGenericTypeDefinition() != typeof(NavigationCache<>))
        {
            _closedCacheType = WalkToNavigationCacheBase(cacheInstance.GetType())
                ?? throw new InvalidOperationException(
                    $"Could not find NavigationCache<T> base on {cacheInstance.GetType().FullName}");
        }

        _settlementRecordType = _closedCacheType.GetGenericArguments()[0];

        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        _distanceField = _closedCacheType.GetField("_settlementToSettlementDistanceWithLandRatio", AnyInstance)
            ?? throw new MissingFieldException(_closedCacheType.FullName, "_settlementToSettlementDistanceWithLandRatio");
        _neighborField = _closedCacheType.GetField("_fortificationNeighbors", AnyInstance)
            ?? throw new MissingFieldException(_closedCacheType.FullName, "_fortificationNeighbors");
        // v1.3.15: _navigationType is declared `protected NavigationType _navigationType { get; private set; }` — a property, not a field
        _navTypeProperty = _closedCacheType.GetProperty("_navigationType", AnyInstance)
            ?? throw new MissingMemberException(_closedCacheType.FullName, "_navigationType");

        _getAllRegisteredSettlements = _closedCacheType.GetMethod("GetAllRegisteredSettlements", AnyInstance)
            ?? throw new MissingMethodException(_closedCacheType.FullName, "GetAllRegisteredSettlements");
        _getFortificationsForNeighborDetection = _closedCacheType.GetMethod("GetUpdatedSettlementsForNeighborDetection", AnyInstance)
            ?? throw new MissingMethodException(_closedCacheType.FullName, "GetUpdatedSettlementsForNeighborDetection");
        _addClosestEntrancePairBase = _closedCacheType.GetMethod("AddClosestEntrancePairBase", AnyInstance)
            ?? throw new MissingMethodException(_closedCacheType.FullName, "AddClosestEntrancePairBase");
        _addNeighbor = _closedCacheType.GetMethod("AddNeighbor", AnyInstance)
            ?? throw new MissingMethodException(_closedCacheType.FullName, "AddNeighbor");

        _checkBeingNeighbor2Arg = FindCheckBeingNeighbor2Arg(_closedCacheType)
            ?? throw new MissingMethodException(_closedCacheType.FullName, "CheckBeingNeighbor(List<T>, T, T)");

        _getCacheElementByRecord = FindGetCacheElementByRecord(_closedCacheType, _settlementRecordType)
            ?? throw new MissingMethodException(_closedCacheType.FullName, "GetCacheElement(T, bool)");

        _getRealDistance = FindGetRealDistance(_closedCacheType, _settlementRecordType)
            ?? throw new MissingMethodException(_closedCacheType.FullName, "GetRealDistanceAndLandRatioBetweenSettlements");

        _setSettlementDistance = FindSetSettlementDistance(_closedCacheType, _settlementRecordType)
            ?? throw new MissingMethodException(_closedCacheType.FullName, "SetSettlementToSettlementDistanceWithLandRatio");

        var elementType = typeof(NavigationCacheElement<>).MakeGenericType(_settlementRecordType);
        _sortElements = elementType.GetMethod("Sort", BindingFlags.Static | BindingFlags.Public)
            ?? throw new MissingMethodException(elementType.FullName, "Sort");

        _generateClosestSettlementCache = _closedCacheType.GetMethod("GenerateClosestSettlementToFaceCache", AnyInstance)
            ?? throw new MissingMethodException(_closedCacheType.FullName, "GenerateClosestSettlementToFaceCache");

        _getSceneCrcValues = concreteType.GetMethod("GetSceneXmlCrcValues", AnyInstance)
            ?? throw new MissingMethodException(concreteType.FullName, "GetSceneXmlCrcValues");

        _serializeMethod = _closedCacheType.GetMethod("Serialize", new[] { typeof(string) })
            ?? throw new MissingMethodException(_closedCacheType.FullName, "Serialize(string)");

        _deserializeMethod = _closedCacheType.GetMethod("Deserialize", new[] { typeof(string) })
            ?? throw new MissingMethodException(_closedCacheType.FullName, "Deserialize(string)");

        TryLogConstruction(concreteType);
    }

    private void TryLogConstruction(Type concreteType)
    {
        if (_logger == null) return;
        _logger.LogInfo(
            $"[NavigationCacheAdapter] bound: concrete={concreteType.FullName}, " +
            $"closedGeneric={_closedCacheType.FullName}, " +
            $"settlementRecord={_settlementRecordType.FullName}");
        _logger.LogDebug(
            $"[NavigationCacheAdapter] reflection bindings all resolved " +
            $"(distance dict, neighbor dict, nav-type property, settlement enumeration, " +
            $"AddClosestEntrancePairBase, CheckBeingNeighbor, AddNeighbor, " +
            $"GetCacheElement, GetRealDistance, SetSettlementDistance, Sort, " +
            $"GenerateClosestSettlementCache, GetSceneCrcValues, Serialize, Deserialize)");
    }

    public void RunClosestSettlementCache()
    {
        _generateClosestSettlementCache.Invoke(_cacheInstance, Array.Empty<object>());
    }

    public void RemoveDistanceEntriesFor(HashSet<string> changedIds)
    {
        if (changedIds == null || changedIds.Count == 0) return;

        var outer = (IDictionary?)_distanceField.GetValue(_cacheInstance);
        if (outer == null) return;

        var outerKeysToRemove = new List<object>();
        var innerCleanupPairs = new List<(IDictionary inner, List<object> keys)>();

        foreach (DictionaryEntry kv1 in outer)
        {
            var (outerId, _) = ExtractElementKey(kv1.Key!);
            if (changedIds.Contains(outerId))
            {
                outerKeysToRemove.Add(kv1.Key!);
                continue;
            }
            var inner = (IDictionary?)kv1.Value;
            if (inner == null) continue;
            var innerKeysToRemove = new List<object>();
            foreach (DictionaryEntry kv2 in inner)
            {
                var (innerId, _) = ExtractElementKey(kv2.Key!);
                if (changedIds.Contains(innerId))
                    innerKeysToRemove.Add(kv2.Key!);
            }
            if (innerKeysToRemove.Count > 0)
                innerCleanupPairs.Add((inner, innerKeysToRemove));
        }

        foreach (var (inner, keys) in innerCleanupPairs)
            foreach (var k in keys)
                inner.Remove(k);

        foreach (var k in outerKeysToRemove)
            outer.Remove(k);

        _logger?.LogDebug(
            $"[NavigationCacheAdapter] RemoveDistanceEntriesFor: removed {outerKeysToRemove.Count} outer entries " +
            $"+ inner cleanups across {innerCleanupPairs.Count} remaining outer entries");
    }

    public void ClearFortificationNeighbors()
    {
        var dict = (IDictionary?)_neighborField.GetValue(_cacheInstance);
        dict?.Clear();
        _logger?.LogDebug("[NavigationCacheAdapter] ClearFortificationNeighbors: cleared");
    }

    public (uint SceneCrc, uint NavMeshCrc) GetSceneCrcValues()
    {
        var args = new object[] { 0u, 0u };
        _getSceneCrcValues.Invoke(_cacheInstance, args);
        return ((uint)args[0], (uint)args[1]);
    }

    public void SerializeCache(string filePath)
    {
        _serializeMethod.Invoke(_cacheInstance, new object[] { filePath });
    }

    public void DeserializeCache(string filePath)
    {
        _deserializeMethod.Invoke(_cacheInstance, new object[] { filePath });
    }

    public MobileParty.NavigationType NavigationType =>
        (MobileParty.NavigationType)_navTypeProperty.GetValue(_cacheInstance)!;

    public IReadOnlyList<ISettlementDataHolder> GetAllRegisteredSettlements()
    {
        var raw = _getAllRegisteredSettlements.Invoke(_cacheInstance, Array.Empty<object>());
        return CastToReadOnlyList(raw);
    }

    public SettlementCollection GetFortificationsForNeighborDetection()
    {
        // Vanilla signature: protected List<T> GetUpdatedSettlementsForNeighborDetection(List<T> settlements)
        var all = _getAllRegisteredSettlements.Invoke(_cacheInstance, Array.Empty<object>());
        var args = Args2();
        args[0] = all!;
        // _getFortificationsForNeighborDetection takes one parameter — but we use Args2 since
        // it's already allocated; reflection ignores trailing slots beyond the parameter count.
        // Build a precise 1-slot array each call would defeat the purpose; instead use a 1-slot helper.
        var raw = _getFortificationsForNeighborDetection.Invoke(_cacheInstance, new[] { all });
        var publicView = CastToReadOnlyList(raw);
        return new SettlementCollection(publicView, raw!);
    }

    public void AddClosestEntrancePair(
        ISettlementDataHolder s1, bool isPort1,
        ISettlementDataHolder s2, bool isPort2)
    {
        var args = Args4();
        args[0] = s1; args[1] = isPort1; args[2] = s2; args[3] = isPort2;
        _addClosestEntrancePairBase.Invoke(_cacheInstance, args);
    }

    public bool CheckBeingNeighbor(SettlementCollection settlements, ISettlementDataHolder s1, ISettlementDataHolder s2)
    {
        var args = Args3();
        args[0] = settlements.UnderlyingList; args[1] = s1; args[2] = s2;
        var result = _checkBeingNeighbor2Arg.Invoke(_cacheInstance, args);
        return (bool)result!;
    }

    public void AddNeighbor(ISettlementDataHolder s1, ISettlementDataHolder s2)
    {
        var args = Args2();
        args[0] = s1; args[1] = s2;
        _addNeighbor.Invoke(_cacheInstance, args);
    }

    public PairComputeResult ComputeClosestEntrancePair(
        ISettlementDataHolder s1, bool isPort1,
        ISettlementDataHolder s2, bool isPort2)
    {
        // Reuse the per-thread 2-slot array for GetCacheElement (called twice).
        var ge = Args2();
        ge[0] = s1; ge[1] = isPort1;
        var element1 = _getCacheElementByRecord.Invoke(_cacheInstance, ge)!;
        ge[0] = s2; ge[1] = isPort2;
        var element2 = _getCacheElementByRecord.Invoke(_cacheInstance, ge)!;

        // Reuse the per-thread 3-slot array for GetRealDistance (called twice) AND Sort (once).
        // Each call mutates slot[2] as `out float landRatio` / `out bool isPairChanged`; we read
        // it into a local before reusing the slot for the next call.
        var rd = Args3();
        rd[0] = element1; rd[1] = element2; rd[2] = 1f;
        var dist1 = (float)_getRealDistance.Invoke(_cacheInstance, rd)!;
        var landRatio1 = (float)rd[2];

        rd[0] = element2; rd[1] = element1; rd[2] = 1f;
        var dist2 = (float)_getRealDistance.Invoke(_cacheInstance, rd)!;
        var landRatio2 = (float)rd[2];

        var avgDistance = (dist1 + dist2) * 0.5f;
        if (avgDistance <= 0f)
            return PairComputeResult.Invalid;

        var navType = NavigationType;
        var finalLandRatio = navType switch
        {
            MobileParty.NavigationType.Naval => 0f,
            MobileParty.NavigationType.All => landRatio1,
            _ => 1f,
        };

        rd[0] = element1; rd[1] = element2; rd[2] = false;
        _sortElements.Invoke(null, rd);
        var sortedElement1 = rd[0]!;
        var sortedElement2 = rd[1]!;
        var isPairChanged = (bool)rd[2]!;
        if (isPairChanged && navType == MobileParty.NavigationType.All)
            finalLandRatio = landRatio2;

        return new PairComputeResult(sortedElement1, sortedElement2, avgDistance, finalLandRatio);
    }

    public void WriteComputedPair(in PairComputeResult result)
    {
        if (!result.IsValid) return;
        lock (_writeLock)
        {
            // WriteComputedPair runs single-threaded post-Parallel.For, but reuses the per-thread
            // 4-slot array for symmetry with the parallel compute path.
            var args = Args4();
            args[0] = result.Element1!;
            args[1] = result.Element2!;
            args[2] = result.Distance;
            args[3] = result.LandRatio;
            _setSettlementDistance.Invoke(_cacheInstance, args);
        }
    }

    public IEnumerable<DistancePair> EnumerateExistingDistances()
    {
        var outer = (IDictionary?)_distanceField.GetValue(_cacheInstance);
        if (outer == null) yield break;

        foreach (DictionaryEntry kv1 in outer)
        {
            var (id1, isPort1) = ExtractElementKey(kv1.Key!);
            var inner = (IDictionary?)kv1.Value;
            if (inner == null) continue;
            foreach (DictionaryEntry kv2 in inner)
            {
                var (id2, isPort2) = ExtractElementKey(kv2.Key!);
                var (distance, landRatio) = ExtractFloatTuple(kv2.Value!);
                yield return new DistancePair(
                    new CacheElementKey(id1, isPort1),
                    new CacheElementKey(id2, isPort2),
                    distance,
                    landRatio);
            }
        }
    }

    public IEnumerable<NeighborPair> EnumerateExistingNeighbors()
    {
        var dict = (IDictionary?)_neighborField.GetValue(_cacheInstance);
        if (dict == null) yield break;

        foreach (DictionaryEntry kv in dict)
        {
            var s1 = (ISettlementDataHolder)kv.Key!;
            var list = (IEnumerable?)kv.Value;
            if (list == null) continue;
            foreach (var item in list)
            {
                if (item is ISettlementDataHolder s2)
                    yield return new NeighborPair(s1.StringId, s2.StringId);
            }
        }
    }

    private static Type? WalkToNavigationCacheBase(Type type)
    {
        var current = type;
        while (current != null && current != typeof(object))
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(NavigationCache<>))
                return current;
            current = current.BaseType;
        }
        return null;
    }

    private static MethodInfo? FindCheckBeingNeighbor2Arg(Type closedCacheType)
    {
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var methods = closedCacheType.GetMethods(AnyInstance);
        foreach (var method in methods)
        {
            if (method.Name != "CheckBeingNeighbor") continue;
            var parameters = method.GetParameters();
            if (parameters.Length != 3) continue;
            if (!typeof(IList).IsAssignableFrom(parameters[0].ParameterType)) continue;
            return method;
        }
        return null;
    }

    private static MethodInfo? FindGetCacheElementByRecord(Type closedCacheType, Type recordType)
    {
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var method in closedCacheType.GetMethods(AnyInstance))
        {
            if (method.Name != "GetCacheElement") continue;
            var parameters = method.GetParameters();
            if (parameters.Length != 2) continue;
            if (parameters[0].ParameterType != recordType) continue;
            if (parameters[1].ParameterType != typeof(bool)) continue;
            return method;
        }
        return null;
    }

    private static MethodInfo? FindGetRealDistance(Type closedCacheType, Type recordType)
    {
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var elementType = typeof(NavigationCacheElement<>).MakeGenericType(recordType);
        foreach (var method in closedCacheType.GetMethods(AnyInstance))
        {
            if (method.Name != "GetRealDistanceAndLandRatioBetweenSettlements") continue;
            var parameters = method.GetParameters();
            if (parameters.Length != 3) continue;
            if (parameters[0].ParameterType != elementType) continue;
            if (parameters[1].ParameterType != elementType) continue;
            return method;
        }
        return null;
    }

    private static MethodInfo? FindSetSettlementDistance(Type closedCacheType, Type recordType)
    {
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var elementType = typeof(NavigationCacheElement<>).MakeGenericType(recordType);
        foreach (var method in closedCacheType.GetMethods(AnyInstance))
        {
            if (method.Name != "SetSettlementToSettlementDistanceWithLandRatio") continue;
            var parameters = method.GetParameters();
            if (parameters.Length != 4) continue;
            if (parameters[0].ParameterType != elementType) continue;
            if (parameters[1].ParameterType != elementType) continue;
            if (parameters[2].ParameterType != typeof(float)) continue;
            if (parameters[3].ParameterType != typeof(float)) continue;
            return method;
        }
        return null;
    }

    private static IReadOnlyList<ISettlementDataHolder> CastToReadOnlyList(object? raw)
    {
        if (raw is not IEnumerable enumerable)
            return Array.Empty<ISettlementDataHolder>();

        var result = new List<ISettlementDataHolder>();
        foreach (var item in enumerable)
        {
            if (item is ISettlementDataHolder holder)
                result.Add(holder);
        }
        return result;
    }

    private static (string Id, bool IsPort) ExtractElementKey(object element)
    {
        var type = element.GetType();
        var stringIdProp = type.GetProperty("StringId")
            ?? throw new MissingMemberException(type.FullName, "StringId");
        var isPortField = type.GetField("IsPortUsed")
            ?? throw new MissingFieldException(type.FullName, "IsPortUsed");
        var stringId = (string)stringIdProp.GetValue(element)!;
        var isPort = (bool)isPortField.GetValue(element)!;
        return (stringId, isPort);
    }

    private static (float Distance, float LandRatio) ExtractFloatTuple(object tuple)
    {
        var type = tuple.GetType();
        var item1 = type.GetField("Item1") ?? throw new MissingFieldException(type.FullName, "Item1");
        var item2 = type.GetField("Item2") ?? throw new MissingFieldException(type.FullName, "Item2");
        return ((float)item1.GetValue(tuple)!, (float)item2.GetValue(tuple)!);
    }
}
