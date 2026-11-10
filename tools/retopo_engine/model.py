from __future__ import annotations

from dataclasses import dataclass, field
import math


Vector = tuple[float, float, float]
Face = tuple[int, ...]
Edge = tuple[int, int]


@dataclass
class Mesh:
    vertices: list[Vector]
    faces: list[Face]
    lines: list[Edge] = field(default_factory=list)
    uv: list[tuple[float, float]] = field(default_factory=list)
    uv_name: str = "UVMap"
    attributes: dict[str, list] = field(default_factory=dict)
    edge_attributes: dict[str, dict[Edge, object]] = field(default_factory=dict)

    def __post_init__(self):
        self.vertices = [tuple(float(value) for value in vertex) for vertex in self.vertices]
        self.faces = [tuple(int(index) for index in face) for face in self.faces]
        self.lines = [self.edge_key(*edge) for edge in self.lines]
        self.uv = [tuple(float(value) for value in coordinate) for coordinate in self.uv]
        self.attributes = {name: list(values) for name, values in self.attributes.items()}
        self.edge_attributes = {
            name: {self.edge_key(*edge): value for edge, value in values.items()}
            for name, values in self.edge_attributes.items()
        }

    @staticmethod
    def edge_key(first: int, second: int) -> Edge:
        return (first, second) if first < second else (second, first)

    def copy(self) -> Mesh:
        return Mesh(
            vertices=list(self.vertices),
            faces=list(self.faces),
            lines=list(self.lines),
            uv=list(self.uv),
            uv_name=self.uv_name,
            attributes={name: list(values) for name, values in self.attributes.items()},
            edge_attributes={
                name: dict(values) for name, values in self.edge_attributes.items()
            },
        )

    def edges(self) -> list[Edge]:
        face_edges = {
            self.edge_key(face[index], face[(index + 1) % len(face)])
            for face in self.faces
            for index in range(len(face))
        }
        return sorted(face_edges | set(self.lines))

    def boundary_edges(self) -> list[Edge]:
        counts = {edge: 0 for edge in self.edges()}
        for face in self.faces:
            for index, vertex in enumerate(face):
                counts[self.edge_key(vertex, face[(index + 1) % len(face)])] += 1
        return sorted(edge for edge, count in counts.items() if count == 1)

    def connected_components(self) -> list[list[int]]:
        if not self.faces:
            return []
        parents = list(range(len(self.vertices)))

        def root(index):
            while parents[index] != index:
                parents[index] = parents[parents[index]]
                index = parents[index]
            return index

        def union(first, second):
            first_root = root(first)
            second_root = root(second)
            if first_root != second_root:
                parents[second_root] = first_root

        for face in self.faces:
            for index in range(1, len(face)):
                union(face[0], face[index])
        components = {}
        for face_index, face in enumerate(self.faces):
            components.setdefault(root(face[0]), []).append(face_index)
        return list(components.values())

    def loop_vertex_indices(self):
        for face in self.faces:
            for vertex in face:
                yield vertex


@dataclass
class RetopoSettings:
    scale_gui: float = 1.0
    offset_3d_cloth: float = 0.0
    initialize_planes: bool = False
    angle_threshold: float = 0.55
    corners_threshold: float = 0.5
    median_corners_threshold: float = 0.5
    auto_fill_prepass: bool = False
    auto_fill: bool = False
    auto_fill_max_iterations: int = 5
    output_generated_planes: bool = False
    base_density: float = 25.0
    snapping_distance: float = 1.0
    seams_merge_distance: float = 0.003
    smooth_low_poly: int = 0
    auto_density: bool = True
    full_crease_control: bool = False
    density_add: int = 2
    density_add_threshold: float = 0.7
    subdiv_reproject: int = 0
    smooth_uvs: bool = True
    fit_uv: bool = True
    separate_patterns: bool = False
    separate_parts: bool = True
    thickness_enabled: bool = False
    thickness: float = 0.005
    thickness_offset: float = -1.0
    hide_debug: bool = False
    points_guide: bool = True
    mismatch_lines: bool = True
    only_patterns: bool = False
    only_seams: bool = False
    wireframe: bool = True
    seams: bool = True
    polyloops: bool = False
    debug_material: str = "Retopo Planes"
    hide_viewport_info: bool = False
    morph_2d_3d: float = 0.0
    sibling_contact: bool = True
    edge_count: bool = True
    manual_density: bool = True
    show_snapping_distance: bool = True
    preview_source_cloth: bool = False
    debug_material_seed: int = 0
    ui_hue: float = 0.0

    def validate(self):
        finite_nonnegative = {
            "base_density": self.base_density,
            "snapping_distance": self.snapping_distance,
            "seams_merge_distance": self.seams_merge_distance,
            "scale_gui": self.scale_gui,
        }
        for name, value in finite_nonnegative.items():
            if not math.isfinite(value) or value < 0:
                raise ValueError(f"{name} must be finite and non-negative")
        for name in ("smooth_low_poly", "subdiv_reproject", "auto_fill_max_iterations"):
            value = getattr(self, name)
            if isinstance(value, bool) or not isinstance(value, int) or value < 0:
                raise ValueError(f"{name} must be a non-negative integer")
        for name in ("density_add", "debug_material_seed"):
            value = getattr(self, name)
            if isinstance(value, bool) or not isinstance(value, int):
                raise ValueError(f"{name} must be an integer")
        if self.density_add < 0:
            raise ValueError("density_add must be non-negative")
        for name in (
            "angle_threshold", "corners_threshold", "median_corners_threshold",
            "density_add_threshold", "morph_2d_3d", "ui_hue",
        ):
            value = getattr(self, name)
            if not math.isfinite(value) or not 0 <= value <= 1:
                raise ValueError(f"{name} must be between 0 and 1")
        if not math.isfinite(self.thickness) or self.thickness < 0:
            raise ValueError("thickness must be finite and non-negative")
        if not math.isfinite(self.thickness_offset) or not -1 <= self.thickness_offset <= 1:
            raise ValueError("thickness_offset must be between -1 and 1")
        if self.debug_material not in {"Patterns", "Parts", "Retopo Planes", "None"}:
            raise ValueError("debug_material must be Patterns, Parts, Retopo Planes, or None")
