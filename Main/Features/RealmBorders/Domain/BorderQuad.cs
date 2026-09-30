namespace TAOM.Features.RealmBorders.Domain;

/// <summary>A corner of a border quad: map position, ARGB colour (alpha included) and texture UV.</summary>
public readonly record struct BorderVertex(MapPoint Position, uint Colour, float U, float V);

/// <summary>
/// One quad of a painted border, corners in strip order: the near edge at the start, the far edge at
/// the start, the far edge at the end, the near edge at the end. The renderer lifts it onto the
/// terrain and adds it as two triangles.
/// </summary>
public readonly record struct BorderQuad(BorderVertex NearStart, BorderVertex FarStart, BorderVertex FarEnd, BorderVertex NearEnd);
