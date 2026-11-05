using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// The one test double for a campaign behavior's <see cref="IDataStore"/>, faithful to the engine's
/// <c>CampaignBehaviorDataStore.BehaviorSaveData.SyncData</c> (v1.4.8, decompile
/// <c>TaleWorlds.CampaignSystem.CampaignBehaviorDataStore.cs</c> lines 26 to 39):
/// <list type="bullet">
/// <item>Saving (line 28 to 32): <c>_records.Add(key, data)</c> and return true. The value is recorded as handed,
/// null included, and a second save of the same key throws <see cref="System.ArgumentException"/>.</item>
/// <item>Loading (line 33 to 38): a stored key sets the ref and returns true; a missing key returns false and leaves
/// the ref unchanged.</item>
/// </list>
/// A fake that skips null on save, overwrites a duplicate key or never throws hides the same bugs the engine
/// would surface, so do not add a private copy. <see cref="Seed"/> plants a record for a load test without going
/// through a save.
/// <para>The one deliberate exception is <c>RoundTripDataStore</c> in <c>RacePersistenceServiceTests</c>: it keys by
/// type and injects typed values on load, which that service's typed round trip needs and this string-keyed fake
/// does not provide.</para>
/// <para>Two engine behaviours are not modelled. The engine calls <c>SyncData</c> on load only if the save holds a
/// record for that behavior (<c>CampaignBehaviorDataStore.LoadBehaviorData</c>), so an empty loading store models
/// "record present, key missing", not a save older than the behavior. And a load returns the very object saved,
/// where the engine returns a deserialized copy.</para>
/// </summary>
public sealed class EngineDataStore : IDataStore
{
    private readonly Dictionary<string, object?> _records = new();

    /// <summary>False saves (the default), true loads.</summary>
    public bool Loading { get; set; }

    public bool IsSaving => !Loading;

    public bool IsLoading => Loading;

    public bool SyncData<T>(string key, ref T data)
    {
        if (!Loading)
        {
            _records.Add(key, data);
            return true;
        }

        if (_records.TryGetValue(key, out var stored))
        {
            data = (T)stored!;
            return true;
        }

        return false;
    }

    public bool Has(string key) => _records.ContainsKey(key);

    /// <summary>The recorded value; throws when the key was never saved or seeded.</summary>
    public T Get<T>(string key) => (T)_records[key]!;

    /// <summary>The recorded value, or <c>default</c> when the key is absent.</summary>
    public T GetSaved<T>(string key) => _records.TryGetValue(key, out var v) ? (T)v! : default!;

    /// <summary>Plants a record (replacing any) so a load test can start from a chosen save.</summary>
    public void Seed<T>(string key, T value) => _records[key] = value;
}
