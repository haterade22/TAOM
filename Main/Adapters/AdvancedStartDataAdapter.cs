using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AdvancedStartOptions;

namespace TAOM.Adapters;

/// <summary>
/// The current campaign's Advanced Start Options data (<c>Campaign.Current.AdvancedStartData</c>), which
/// the engine saves with the campaign. Typed values only, so the services above never see an engine type.
/// </summary>
public interface IAdvancedStartDataAdapter
{
    /// <summary>True when a campaign exists and carries an options object to read and write.</summary>
    bool IsAvailable { get; }

    /// <summary>True when the campaign was just created (not loaded from a save).</summary>
    bool IsNewCampaign { get; }

    /// <summary>
    /// Reads a key of type <typeparamref name="T"/>. False when the key is absent or holds another type.
    /// </summary>
    bool TryGet<T>(string key, out T value);

    /// <summary>Appends a key. False, and nothing changed, when the key already exists.</summary>
    bool Add<T>(string key, string categoryId, T value);

    /// <summary>Overwrites an existing key of type <typeparamref name="T"/>. False, and nothing changed, when it is absent or holds another type.</summary>
    bool Set<T>(string key, T value);
}

public class AdvancedStartDataAdapter : IAdvancedStartDataAdapter
{
    private static AdvancedStartOptionsData? Data => Campaign.Current?.AdvancedStartData;

    public bool IsAvailable => Data != null;

    public bool IsNewCampaign => Campaign.Current?.CampaignGameLoadingType == Campaign.GameLoadingType.NewCampaign;

    public bool TryGet<T>(string key, out T value)
    {
        // GetOption, not GetValue<T>: GetValue returns default for a missing key and a wrong type alike.
        if (Data?.GetOption(key) is AdvancedStartData<T> typed)
        {
            value = typed.Value;
            return true;
        }
        value = default!;
        return false;
    }

    public bool Add<T>(string key, string categoryId, T value)
    {
        var data = Data;
        return data != null && data.AddData(new AdvancedStartData<T>(key, categoryId, value));
    }

    public bool Set<T>(string key, T value)
    {
        if (Data?.GetOption(key) is not AdvancedStartData<T> typed) return false;
        typed.Value = value;
        return true;
    }
}
