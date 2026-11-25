namespace TAOM.CoopCompat.LoadOrder;

/// <summary>One active module as the load order check sees it: no engine type, so the rules and the service stay testable.</summary>
public sealed class ModuleEntry
{
    public ModuleEntry(string id, string name, bool isOfficial, bool hasObjectData)
    {
        Id = id;
        Name = name;
        IsOfficial = isOfficial;
        HasObjectData = hasObjectData;
    }

    public string Id { get; }
    public string Name { get; }
    public bool IsOfficial { get; }

    /// <summary>Its SubModule.xml lists game data that can add or replace items, troops or factions (<see cref="LoadOrderRules.ObjectData"/>).</summary>
    public bool HasObjectData { get; }
}
