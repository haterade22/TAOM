"""Independent synthetic cases used to compare public retopology behavior."""

from .model import Mesh, RetopoSettings


def curved_grid():
    return Mesh(
        vertices=[
            (-0.5, -0.5, 0.0), (0.0, -0.5, 0.1), (0.5, -0.5, 0.0),
            (-0.5, 0.0, 0.1), (0.0, 0.0, 0.2), (0.5, 0.0, 0.1),
            (-0.5, 0.5, 0.0), (0.0, 0.5, 0.1), (0.5, 0.5, 0.0),
        ],
        faces=[(0, 1, 4, 3), (1, 2, 5, 4), (3, 4, 7, 6), (4, 5, 8, 7)],
        uv=[
            (0, 0), (0.5, 0), (0.5, 0.5), (0, 0.5),
            (0.5, 0), (1, 0), (1, 0.5), (0.5, 0.5),
            (0, 0.5), (0.5, 0.5), (0.5, 1), (0, 1),
            (0.5, 0.5), (1, 0.5), (1, 1), (0.5, 1),
        ],
    )


def two_islands():
    return Mesh(
        vertices=[
            (-0.5, -0.5, 0.0), (0.0, -0.5, 0.1), (0.0, 0.5, 0.1), (-0.5, 0.5, 0.0),
            (0.5, -0.5, 0.0), (1.0, -0.5, 0.1), (1.0, 0.5, 0.1), (0.5, 0.5, 0.0),
        ],
        faces=[(0, 1, 2, 3), (4, 5, 6, 7)],
        uv=[
            (0, 0), (0.5, 0), (0.5, 1), (0, 1),
            (0.5, 0), (1, 0), (1, 1), (0.5, 1),
        ],
    )


def ring():
    import math

    vertices = []
    for radius in (1.0, 0.5):
        for index in range(8):
            angle = math.tau * index / 8
            vertices.append((radius * math.cos(angle), radius * math.sin(angle), 0.1))
    faces = []
    for index in range(8):
        following = (index + 1) % 8
        faces.append((index, following, 8 + following, 8 + index))
    source = Mesh(vertices=vertices, faces=faces)
    source.uv = [((vertices[vertex][0] + 1) / 2, (vertices[vertex][1] + 1) / 2) for face in faces for vertex in face]
    return source


def hexagon():
    import math

    vertices = [(math.cos(math.tau * i / 6), math.sin(math.tau * i / 6), 0.0) for i in range(6)]
    return Mesh(
        vertices=vertices,
        faces=[tuple(range(6))],
        uv=[((vertex[0] + 1) / 2, (vertex[1] + 1) / 2) for vertex in vertices],
    )


def mirror_source():
    return Mesh(
        vertices=[
            (-1, 0, 0), (0, 0, 0), (0, 1, 0), (-1, 1, 0),
            (0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0),
            (0, -1, 0), (1, -1, 0), (1, -2, 0), (0, -2, 0),
            (-1, -1, 0), (0, -1, 0), (0, -2, 0), (-1, -2, 0),
        ],
        faces=[(0, 1, 2, 3), (4, 5, 6, 7), (8, 9, 10, 11), (12, 13, 14, 15)],
        uv=[
            (0, 0.65), (0.4, 0.65), (0.4, 1), (0, 1),
            (0.6, 0.65), (1, 0.65), (1, 1), (0.6, 1),
            (0.6, 0.35), (1, 0.35), (1, 0), (0.6, 0),
            (0, 0.35), (0.4, 0.35), (0.4, 0), (0, 0),
        ],
    )


def unit_guide():
    return Mesh(vertices=[(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0)], faces=[(0, 1, 2, 3)])


def two_guide():
    return Mesh(
        vertices=[(0, 0, 0), (0.5, 0, 0), (1, 0, 0), (0, 1, 0), (0.5, 1, 0), (1, 1, 0)],
        faces=[(0, 1, 4, 3), (1, 2, 5, 4)],
    )


def ring_guide():
    source = ring()
    source.vertices = [((vertex[0] + 1) / 2, (vertex[1] + 1) / 2, 0.0) for vertex in source.vertices]
    source.uv = []
    return source


def mirror_guide(extra_points=1):
    guide = Mesh(
        vertices=[(0.6, 0.65, 0), (1, 0.65, 0), (1, 1, 0), (0.6, 1, 0)],
        faces=[(0, 1, 2, 3)],
    )
    guide.vertices.extend([(0.2, 0.825, 0.0), (0.8, 0.175, 0.0)][:extra_points])
    guide.lines = [(0, 4), (0, 5)][:extra_points]
    return guide


def hex_guide():
    return Mesh(
        vertices=[(0.75, 0.0669873, 0), (0.25, 0.0669873, 0), (0.25, 0.9330127, 0), (0.75, 0.9330127, 0)],
        faces=[(0, 1, 2, 3)],
    )


def small_guide():
    return Mesh(
        vertices=[(0.25, 0.25, 0), (0.75, 0.25, 0), (0.75, 0.75, 0), (0.25, 0.75, 0)],
        faces=[(0, 1, 2, 3)],
    )


def non_manifold():
    return Mesh(
        vertices=[(0, 0, 0), (1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1)],
        faces=[(0, 1, 2), (1, 0, 3), (0, 1, 4)],
    )


def case(name, source, guide, **settings):
    point_guides = settings.pop("point_guides", [])
    return {
        "name": name,
        "source": source,
        "guide": guide,
        "settings": RetopoSettings(**settings),
        "point_guides": point_guides,
    }


def cases():
    result = [
        case("base", curved_grid(), unit_guide(), fit_uv=False),
        case("subdiv_1", curved_grid(), unit_guide(), subdiv_reproject=1, fit_uv=False),
        case("subdiv_2", curved_grid(), unit_guide(), subdiv_reproject=2, fit_uv=False),
        case("smooth_low_poly", curved_grid(), unit_guide(), smooth_low_poly=2, fit_uv=False),
        case("morph_half", curved_grid(), unit_guide(), subdiv_reproject=1, morph_2d_3d=0.5, fit_uv=False),
        case("fit_uv", curved_grid(), unit_guide(), fit_uv=True),
        case("thickness", curved_grid(), unit_guide(), thickness_enabled=True, thickness=0.05, thickness_offset=0.0, fit_uv=False),
        case("thickness_offset", curved_grid(), unit_guide(), thickness_enabled=True, thickness=0.05, thickness_offset=1.0, fit_uv=False),
        case("initialize", curved_grid(), unit_guide(), initialize_planes=True, output_generated_planes=True),
        case("two_guides", curved_grid(), two_guide(), fit_uv=False, separate_parts=False),
        case("smooth_uv_off", curved_grid(), two_guide(), smooth_low_poly=1, smooth_uvs=False, fit_uv=False, separate_parts=False),
        case("smooth_uv_on", curved_grid(), two_guide(), smooth_low_poly=1, smooth_uvs=True, fit_uv=False, separate_parts=False),
        case("separate_patterns", two_islands(), two_guide(), separate_patterns=True, separate_parts=False),
        case("separate_parts", two_islands(), two_guide(), separate_parts=True),
        case("ring", ring(), ring_guide(), fit_uv=False),
        case("ring_thickness", ring(), ring_guide(), thickness_enabled=True, thickness=0.1, thickness_offset=0.0, fit_uv=False),
        case("hex_initialize", hexagon(), unit_guide(), initialize_planes=True, output_generated_planes=True),
        case("mirror_x", mirror_source(), mirror_guide(), fit_uv=False),
        case("mirror_xy", mirror_source(), mirror_guide(extra_points=2), fit_uv=False),
        case("auto_fill_prepass", curved_grid(), unit_guide(), auto_fill_prepass=True, auto_fill_max_iterations=5, fit_uv=False, point_guides=[1, 3, 5, 7]),
        case("auto_fill_connected", curved_grid(), unit_guide(), auto_fill=True, auto_fill_max_iterations=5, fit_uv=False, point_guides=[1, 3, 5, 7]),
        case("auto_fill_hex_prepass", hexagon(), hex_guide(), auto_fill_prepass=True, auto_fill_max_iterations=5, fit_uv=False, point_guides=[0, 1, 2, 3, 4, 5]),
        case("auto_fill_hex_connected", hexagon(), hex_guide(), auto_fill=True, auto_fill_max_iterations=5, fit_uv=False, point_guides=[0, 1, 2, 3, 4, 5]),
    ]
    for name, origins, targets in (
        ("mirror_same_x_separate", (0, 1), ((0.2, 0.825, 0), (0.2, 0.825, 0))),
        ("mirror_same_x_shared", (0, 0), ((0.2, 0.825, 0), (0.2, 0.825, 0))),
        ("mirror_same_y_separate", (0, 1), ((0.8, 0.175, 0), (0.8, 0.175, 0))),
        ("mirror_same_y_shared", (0, 0), ((0.8, 0.175, 0), (0.8, 0.175, 0))),
        ("mirror_y_single", (0,), ((0.8, 0.175, 0),)),
    ):
        guide = mirror_guide(extra_points=len(origins))
        guide.lines = [(origin, 4 + index) for index, origin in enumerate(origins)]
        for index, target in enumerate(targets):
            guide.vertices[4 + index] = target
        result.append(case(name, mirror_source(), guide, fit_uv=False))
    local_density = two_guide()
    local_density.edge_attributes["crease_edge"] = {(0, 1): 0.5}
    result.append(case("density_local", curved_grid(), local_density, base_density=100, auto_density=False, subdiv_reproject=1, fit_uv=False, separate_parts=False))
    connected_density = two_guide()
    connected_density.edge_attributes["crease_edge"] = {(0, 1): 0.5}
    result.append(case("density_connected", curved_grid(), connected_density, base_density=100, auto_density=True, subdiv_reproject=1, fit_uv=False, separate_parts=False))
    sharp_density = two_guide()
    sharp_density.edge_attributes["sharp_edge"] = {(0, 1): True}
    result.append(case("density_sharp", curved_grid(), sharp_density, density_add=2, subdiv_reproject=1, fit_uv=False, separate_parts=False))
    result.append(case("density_sharp_high", curved_grid(), sharp_density, base_density=100, density_add=2, subdiv_reproject=1, fit_uv=False, separate_parts=False))
    for name, crease in (("density_full_neutral", 0.5), ("density_full_high", 0.75), ("density_full_low", 0.25)):
        guide = two_guide()
        guide.edge_attributes["crease_edge"] = {(0, 1): crease}
        result.append(case(name, curved_grid(), guide, base_density=100, full_crease_control=True, subdiv_reproject=1, fit_uv=False, separate_parts=False))
    return result


def summary(mesh):
    bounds = [
        [min((vertex[axis] for vertex in mesh.vertices), default=0.0) for axis in range(3)],
        [max((vertex[axis] for vertex in mesh.vertices), default=0.0) for axis in range(3)],
    ]
    uv_bounds = [
        [min((value[axis] for value in mesh.uv), default=0.0) for axis in range(2)],
        [max((value[axis] for value in mesh.uv), default=0.0) for axis in range(2)],
    ]
    return {
        "vertices": len(mesh.vertices),
        "edges": len(mesh.edges()),
        "faces": len(mesh.faces),
        "face_sizes": {str(size): sum(len(face) == size for face in mesh.faces) for size in sorted({len(face) for face in mesh.faces})},
        "components": len(mesh.connected_components()),
        "bounds": bounds,
        "uv_bounds": uv_bounds,
        "uv_name": mesh.uv_name,
        "attributes": sorted(mesh.attributes),
        "sample_vertices": mesh.vertices[:12],
        "sample_faces": mesh.faces[:12],
        "sample_uv": mesh.uv[:12],
    }
