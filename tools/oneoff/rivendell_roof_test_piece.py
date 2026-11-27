"""Pure geometry of a roof test piece: a wall slab under a gable roof, for the roof slate set (round 4).

The roof slope maps U along the eave and V up the slope at 0.45 UV units per metre, as the gatehouse meshes
will. blender_roof_test_piece.py builds the piece in Blender from these numbers.
"""
import math

UV_PER_METRE = 0.45


def roof_slope(x0, x1, y_eave, y_ridge, z_eave, pitch_deg):
    """One roof plane as a quad. The eave runs from x0 to x1 at y_eave and z_eave; the ridge is at y_ridge.
    Returns the four vertices (eave left, eave right, ridge right, ridge left), their UVs (U along the eave, V up
    the slope, 0.45 per metre, V 0 at the eave), and the unit normal."""
    run = y_ridge - y_eave
    rise = abs(run) * math.tan(math.radians(pitch_deg))
    verts = [(x0, y_eave, z_eave), (x1, y_eave, z_eave), (x1, y_ridge, z_eave + rise), (x0, y_ridge, z_eave + rise)]
    up = math.sqrt(run * run + rise * rise)
    uvs = [(0.0, 0.0), ((x1 - x0) * UV_PER_METRE, 0.0), ((x1 - x0) * UV_PER_METRE, up * UV_PER_METRE),
           (0.0, up * UV_PER_METRE)]
    # The normal of the plane: the cross product of the eave direction and the up-slope direction.
    ex = (x1 - x0, 0.0, 0.0)
    ey = (0.0, run, rise)
    n = (ex[1] * ey[2] - ex[2] * ey[1], ex[2] * ey[0] - ex[0] * ey[2], ex[0] * ey[1] - ex[1] * ey[0])
    length = math.sqrt(sum(c * c for c in n))
    n = tuple(c / length for c in n)
    if n[2] < 0:
        n = tuple(-c for c in n)
    return {"verts": verts, "uvs": uvs, "normal": n}


def box_uv(point, normal, per_metre=UV_PER_METRE):
    """UV of a point on a box face by the axis of its normal: a face facing X takes (y, z), one facing Y takes
    (x, z), one facing Z takes (x, y)."""
    ax = max(range(3), key=lambda i: abs(normal[i]))
    a, b = {0: (1, 2), 1: (0, 2), 2: (0, 1)}[ax]
    return point[a] * per_metre, point[b] * per_metre
