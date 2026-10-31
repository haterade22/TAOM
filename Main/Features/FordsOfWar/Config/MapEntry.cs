using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// One scene of maps.json. <paramref name="DisplayName"/> is the raw <c>{=KEY}English</c> text (the client resolves it);
/// <paramref name="Options"/> feed <see cref="LaneLayoutGenerator"/>; <paramref name="Tower"/> and <paramref name="Keep"/>
/// name the prefab and scale per <see cref="TowerKind"/>.
/// </summary>
internal sealed record MapEntry(string SceneId, string DisplayName, MapLayoutMode Layout, LayoutOptions Options,
    TowerPrefab Tower, TowerPrefab Keep, CampDefaults Camp);
