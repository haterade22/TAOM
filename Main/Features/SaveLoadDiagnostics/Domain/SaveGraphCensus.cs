using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace TAOM.Features.SaveLoadDiagnostics.Domain;

// Counts what a save holds per type, to name what grows across a reload (plan 114, SAVE-TIME-01).
// Observes only. For an engine object it also counts instances that are not registered, and instances
// repeating an id already seen in this save: a stale copy of a registered object shares its original's
// id, and the save's identity dictionary keeps both because MBObjectBase overrides GetHashCode but not
// Equals (installed v1.4.8 MBObjectBase.cs:79-82). Plain values only (ADR-007): the hook describes them.
internal sealed class SaveGraphCensus
{
    internal const int MinRowCount = 1000;
    internal const int MaxRowsPerKind = 200;

    private sealed class ObjectRow
    {
        public int Count;
        public int Unregistered;
        public int DuplicateIds;
        public bool HasIdentity;
    }

    private sealed class ContainerRow
    {
        public int Count;
        public long Elements;
    }

    private readonly int _minRowCount;
    private readonly int _maxRowsPerKind;
    private readonly Dictionary<Type, ObjectRow> _objects = new Dictionary<Type, ObjectRow>();
    private readonly Dictionary<Type, ContainerRow> _containers = new Dictionary<Type, ContainerRow>();
    private readonly HashSet<uint> _seenIds = new HashSet<uint>();
    private int _objectTotal;
    private int _containerTotal;
    private int _unregisteredTotal;
    private int _duplicateTotal;

    public SaveGraphCensus() : this(MinRowCount, MaxRowsPerKind) { }

    internal SaveGraphCensus(int minRowCount, int maxRowsPerKind)
    {
        _minRowCount = minRowCount;
        _maxRowsPerKind = maxRowsPerKind;
    }

    public void AddObject(Type type)
    {
        Row(type).Count++;
        _objectTotal++;
    }

    public void AddMbObject(Type type, uint id, bool? isRegistered)
    {
        var row = Row(type);
        row.Count++;
        row.HasIdentity = true;
        _objectTotal++;
        if (isRegistered == false)
        {
            row.Unregistered++;
            _unregisteredTotal++;
        }
        if (id != 0 && !_seenIds.Add(id))
        {
            row.DuplicateIds++;
            _duplicateTotal++;
        }
    }

    public void AddContainer(Type type, int elementCount)
    {
        if (!_containers.TryGetValue(type, out var row))
            _containers[type] = row = new ContainerRow();
        row.Count++;
        if (elementCount > 0) row.Elements += elementCount;
        _containerTotal++;
    }

    public static SaveGraphCensus Walk(
        IReadOnlyList<object> objects,
        IReadOnlyList<object> containers,
        Func<object, (uint Id, bool? IsRegistered)?> describe)
    {
        var census = new SaveGraphCensus();
        for (int i = 0; i < objects.Count; i++)
        {
            var item = objects[i];
            if (item == null) continue;
            var identity = describe(item);
            if (identity == null)
                census.AddObject(item.GetType());
            else
                census.AddMbObject(item.GetType(), identity.Value.Id, identity.Value.IsRegistered);
        }
        for (int i = 0; i < containers.Count; i++)
        {
            var item = containers[i];
            if (item == null) continue;
            census.AddContainer(item.GetType(), item is ICollection collection ? collection.Count : -1);
        }
        return census;
    }

    public string FormatSummary(string reason, int loadsThisProcess, long passMs) =>
        FormattableString.Invariant(
            $"reason={reason} loadsThisProcess={loadsThisProcess} objects={_objectTotal} containers={_containerTotal} objectTypes={_objects.Count} containerTypes={_containers.Count} unregistered={_unregisteredTotal} duplicateIds={_duplicateTotal} passMs={passMs}");

    // Objects first, each kind sorted by n descending then name. `type=` stays last: a type name is the
    // one token that can hold spaces. A row with a flagged instance is always listed, past any threshold.
    public IEnumerable<string> FormatTypeLines()
    {
        int kept = 0;
        foreach (var row in _objects.Select(p => (Name: FriendlyName(p.Key), Row: p.Value))
                     .OrderByDescending(r => r.Row.Count).ThenBy(r => r.Name, StringComparer.Ordinal))
        {
            bool flagged = row.Row.Unregistered > 0 || row.Row.DuplicateIds > 0;
            if (!flagged)
            {
                if (row.Row.Count < _minRowCount || kept >= _maxRowsPerKind) continue;
                kept++;
            }
            var identity = row.Row.HasIdentity
                ? FormattableString.Invariant($" unregistered={row.Row.Unregistered} dupIds={row.Row.DuplicateIds}")
                : string.Empty;
            yield return FormattableString.Invariant($"obj n={row.Row.Count}{identity} type='{row.Name}'");
        }

        kept = 0;
        foreach (var row in _containers.Select(p => (Name: FriendlyName(p.Key), Row: p.Value))
                     .OrderByDescending(r => r.Row.Count).ThenBy(r => r.Name, StringComparer.Ordinal))
        {
            if (row.Row.Count < _minRowCount || kept >= _maxRowsPerKind) continue;
            kept++;
            yield return FormattableString.Invariant($"ctr n={row.Row.Count} elements={row.Row.Elements} type='{row.Name}'");
        }
    }

    internal static string FriendlyName(Type type)
    {
        if (type.IsArray)
            return FriendlyName(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        if (!type.IsGenericType)
            return type.FullName ?? type.Name;

        var definition = type.GetGenericTypeDefinition();
        var name = Regex.Replace(definition.FullName ?? definition.Name, @"`\d+", string.Empty);
        return name + "<" + string.Join(",", type.GetGenericArguments().Select(FriendlyName)) + ">";
    }

    private ObjectRow Row(Type type)
    {
        if (!_objects.TryGetValue(type, out var row))
            _objects[type] = row = new ObjectRow();
        return row;
    }
}
