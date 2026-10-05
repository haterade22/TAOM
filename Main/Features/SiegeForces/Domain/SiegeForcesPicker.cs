using System.Collections.Generic;

namespace TAOM.Features.SiegeForces.Domain;

/// <summary>
/// One row of the troop selection screen: a character aggregated across every party in scope. <see cref="Number"/> and
/// <see cref="Wounded"/> describe the whole stack (the screen shows both); <see cref="Initial"/> is how many start
/// ticked. <see cref="Source"/> is the engine character, opaque, for the adapter to build the screen's rosters from.
/// </summary>
public sealed record PickerRow(object Source, string CharacterId, int Number, int Wounded, int Initial);

/// <summary>
/// What the troop selection screen is opened with. <see cref="Max"/> is the healthy total, so everyone who can fight
/// may be ticked; <see cref="Min"/> is the least the screen accepts.
/// </summary>
public sealed record PickerRequest(IReadOnlyList<PickerRow> Rows, int Max, int Min);
