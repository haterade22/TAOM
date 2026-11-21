namespace TAOM.Adapters;

/// <summary>A party whose <c>MapEventSide</c> points at a side that no longer lists it.</summary>
public interface IStrandedPartyAdapter
{
    /// <summary>The party, its clan and the battle it points at, for the log.</summary>
    string Label();

    /// <summary>Clears the party's side reference without the setter, which would throw on the missing entry.</summary>
    void Release();
}
