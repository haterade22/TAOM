"""Blender-independent strand data with explicit metadata ownership."""

from dataclasses import dataclass, field

import numpy as np

from .curves import resample_arc_length


@dataclass
class Strand:
    positions: np.ndarray
    radius: np.ndarray | None = None
    tilt: np.ndarray | None = None
    profile_id: int = 0
    uv_region: int = -1
    group_id: int = 0
    tags: frozenset[str] = frozenset()
    point_attributes: dict[str, np.ndarray] = field(default_factory=dict)

    def __post_init__(self):
        self.positions = np.asarray(self.positions, dtype=np.float64)
        if self.positions.ndim != 2 or self.positions.shape[1] != 3 or len(self.positions) < 2:
            raise ValueError("strand positions must be an Nx3 array with at least two rows")
        if not np.isfinite(self.positions).all():
            raise ValueError("strand positions must all be finite")
        count = len(self.positions)
        self.radius = np.ones(count) if self.radius is None else np.asarray(self.radius, dtype=np.float64)
        self.tilt = np.zeros(count) if self.tilt is None else np.asarray(self.tilt, dtype=np.float64)
        if self.radius.shape != (count,) or self.tilt.shape != (count,):
            raise ValueError("radius and tilt must have one value per point")
        if not np.isfinite(self.radius).all() or not np.isfinite(self.tilt).all():
            raise ValueError("radius and tilt must all be finite")
        if np.any(self.radius < 0):
            raise ValueError("radius cannot be negative")
        self.tags = frozenset(self.tags)
        for name, values in list(self.point_attributes.items()):
            values = np.asarray(values)
            if len(values) != count:
                raise ValueError(f"point attribute {name!r} must have one value per point")
            self.point_attributes[name] = values.copy()

    def copy(self):
        return Strand(
            self.positions.copy(),
            self.radius.copy(),
            self.tilt.copy(),
            self.profile_id,
            self.uv_region,
            self.group_id,
            self.tags,
            {name: values.copy() for name, values in self.point_attributes.items()},
        )

    def resampled(self, count):
        if int(count) != count or count < 2:
            raise ValueError("count must be an integer of at least two")
        count = int(count)
        positions = resample_arc_length(self.positions, count)
        source = np.linspace(0.0, 1.0, len(self.positions))
        target = np.linspace(0.0, 1.0, count)
        attributes = {
            name: np.asarray([np.interp(target, source, values[:, column])
                              for column in range(values.shape[1])]).T
            if values.ndim == 2 else np.interp(target, source, values)
            for name, values in self.point_attributes.items()
        }
        result = Strand(
            positions,
            np.interp(target, source, self.radius),
            np.interp(target, source, self.tilt),
            self.profile_id,
            self.uv_region,
            self.group_id,
            self.tags,
            attributes,
        )
        return result


@dataclass
class Hair:
    strands: list[Strand] = field(default_factory=list)
    attributes: dict = field(default_factory=dict)

    def __post_init__(self):
        if not all(isinstance(strand, Strand) for strand in self.strands):
            raise TypeError("hair strands must be Strand instances")

    def copy(self):
        return Hair([strand.copy() for strand in self.strands], dict(self.attributes))
