using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// One scene of maps.json. <paramref name="DisplayName"/> is the raw <c>{=KEY}English</c> text (the client resolves it);
/// <paramref name="Options"/> feed <see cref="LaneLayoutGenerator"/>; <paramref name="Tower"/> and <paramref name="Keep"/>
/// name the prefab and scale per <see cref="TowerKind"/>. <paramref name="TowerHalfWidth"/> is the half width in metres of the real
/// towers a tags scene built itself (0 for a generated layout): it sets how close a melee unit must stand to be in contact
/// (<see cref="FordsTowerGeometry.ContactMetres"/>), and a map that declares it gets no ground garrison.
/// </summary>
internal sealed record MapEntry(string SceneId, string DisplayName, MapLayoutMode Layout, LayoutOptions Options,
    TowerPrefab Tower, TowerPrefab Keep, CampDefaults Camp, float TowerHalfWidth = 0f);
