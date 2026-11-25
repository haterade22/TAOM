using System.Collections.Generic;

namespace TAOM.Features.FactionPower;

public interface IFactionPowerCatalog
{
    IReadOnlyList<FactionPowerEntry> Entries { get; }
}
