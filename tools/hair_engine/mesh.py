"""Indexed polygon mesh with per-corner UVs and explicit attribute ownership."""

from dataclasses import dataclass, field

import numpy as np


@dataclass
class MeshData:
    vertices: np.ndarray
    faces: list[tuple[int, ...]]
    uv_layers: dict[str, np.ndarray] = field(default_factory=dict)
    materials: list[str] = field(default_factory=lambda: ["TAOM_Hair"])
    material_indices: np.ndarray | None = None
    point_attributes: dict[str, np.ndarray] = field(default_factory=dict)
    edges: list[tuple[int, int]] = field(default_factory=list)

    def __post_init__(self):
        self.vertices = np.asarray(self.vertices, dtype=np.float64)
        if self.vertices.ndim != 2 or self.vertices.shape[1] != 3 or not np.isfinite(self.vertices).all():
            raise ValueError("mesh vertices must be a finite Nx3 array")
        self.faces = [tuple(int(index) for index in face) for face in self.faces]
        for face in self.faces:
            if len(face) < 3 or min(face) < 0 or max(face) >= len(self.vertices):
                raise ValueError("mesh face references an invalid vertex")
        self.edges = [(int(a), int(b)) for a, b in self.edges]
        self.loop_vertices = np.asarray([index for face in self.faces for index in face], dtype=np.int32)
        for name, values in list(self.uv_layers.items()):
            values = np.asarray(values, dtype=np.float64)
            if values.shape != (len(self.loop_vertices), 2) or not np.isfinite(values).all():
                raise ValueError(f"UV layer {name!r} must have two finite values per face corner")
            self.uv_layers[name] = values
        if self.material_indices is None:
            self.material_indices = np.zeros(len(self.faces), dtype=np.int32)
        self.material_indices = np.asarray(self.material_indices, dtype=np.int32)
        if self.material_indices.shape != (len(self.faces),):
            raise ValueError("material_indices must have one value per face")
        if len(self.material_indices) and (self.material_indices.min() < 0
                                           or self.material_indices.max() >= len(self.materials)):
            raise ValueError("material index is outside the material list")
        for name, values in list(self.point_attributes.items()):
            values = np.asarray(values)
            if len(values) != len(self.vertices):
                raise ValueError(f"point attribute {name!r} must have one value per vertex")
            self.point_attributes[name] = values.copy()

    def copy(self):
        return MeshData(
            self.vertices.copy(),
            list(self.faces),
            {name: values.copy() for name, values in self.uv_layers.items()},
            list(self.materials),
            self.material_indices.copy(),
            {name: values.copy() for name, values in self.point_attributes.items()},
            list(self.edges),
        )
