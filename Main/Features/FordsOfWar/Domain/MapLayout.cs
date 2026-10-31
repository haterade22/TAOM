using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Domain;

internal sealed record MapLayout(string SceneName, IReadOnlyList<LaneLayout> Lanes, IReadOnlyList<TowerSlot> Towers,
    IReadOnlyList<CapturePointSlot> CapturePoints, IReadOnlyList<CampSlot> Camps);
