using TAOM.Features.LoreGreetings.Domain;

namespace TAOM.Features.LoreGreetings;

/// <summary>The validated persons and pair lines of <c>lore_greetings/lore_greetings.json</c>; the service tests fake it.</summary>
internal interface ILoreGreetingsConfigProvider
{
    LoreGreetingTable GetTable();
}
