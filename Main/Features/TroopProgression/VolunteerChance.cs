namespace TAOM.Features.TroopProgression;

public readonly struct VolunteerChance
{
    public string CharacterId { get; }
    public int Weight { get; }

    public VolunteerChance(string characterId, int weight)
    {
        CharacterId = characterId;
        Weight = weight;
    }
}
