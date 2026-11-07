"""Pure, deterministic grooming transforms over independent strand data."""

import numpy as np

from .curves import parallel_transport
from .model import Hair, Strand


def _strand(strand):
    if not isinstance(strand, Strand):
        raise TypeError("expected a Strand")
    return strand.copy()


def adjust_length(strand, factor):
    if factor <= 0 or not np.isfinite(factor):
        raise ValueError("length factor must be finite and positive")
    result = _strand(strand)
    result.positions = result.positions[0] + (result.positions - result.positions[0]) * factor
    return result


def trim(strand, fraction):
    if not 0 < fraction <= 1 or not np.isfinite(fraction):
        raise ValueError("trim fraction must be in (0, 1]")
    return adjust_length(strand, fraction)


def taper(strand, root=1.0, tip=0.0, power=1.0):
    if power <= 0 or root < 0 or tip < 0:
        raise ValueError("taper scales must be nonnegative and power positive")
    result = _strand(strand)
    factors = np.linspace(0.0, 1.0, len(result.positions)) ** power
    result.radius *= root * (1.0 - factors) + tip * factors
    return result


def twist(strand, angle):
    if not np.isfinite(angle):
        raise ValueError("twist angle must be finite")
    result = _strand(strand)
    result.tilt += np.linspace(0.0, angle, len(result.positions))
    return result


def smooth(strand, iterations=1, factor=0.5):
    if int(iterations) != iterations or iterations < 0 or not 0 <= factor <= 1:
        raise ValueError("iterations must be nonnegative and factor in [0, 1]")
    result = _strand(strand)
    for _ in range(int(iterations)):
        previous = result.positions.copy()
        result.positions[1:-1] = previous[1:-1] * (1.0 - factor)
        result.positions[1:-1] += (previous[:-2] + previous[2:]) * (0.5 * factor)
    return result


def curl(strand, radius, turns, phase=0.0):
    if radius < 0 or not np.isfinite(radius + turns + phase):
        raise ValueError("curl parameters must be finite and radius nonnegative")
    result = _strand(strand)
    _, normals, binormals = parallel_transport(result.positions)
    t = np.linspace(0.0, 1.0, len(result.positions))
    angle = phase + turns * 2.0 * np.pi * t
    envelope = np.sin(np.pi * t)
    offset = normals * np.cos(angle)[:, np.newaxis] + binormals * np.sin(angle)[:, np.newaxis]
    result.positions = strand.positions + radius * envelope[:, np.newaxis] * offset
    return result


def noise(strand, amplitude, frequency=1.0, seed=0):
    if amplitude < 0 or not np.isfinite(amplitude + frequency):
        raise ValueError("noise amplitude must be nonnegative and frequency finite")
    result = _strand(strand)
    rng = np.random.default_rng(seed)
    phases = rng.uniform(0.0, 2.0 * np.pi, 3)
    t = np.linspace(0.0, 1.0, len(result.positions))
    envelope = np.sin(np.pi * t)
    offset = np.column_stack([np.sin(2.0 * np.pi * frequency * t + phase) for phase in phases])
    result.positions += amplitude * envelope[:, np.newaxis] * offset
    return result


def clump(strands, guide_indices, strength):
    if not 0 <= strength <= 1:
        raise ValueError("clump strength must be in [0, 1]")
    guides = [strands[index] for index in guide_indices]
    if not guides:
        raise ValueError("at least one clump guide is required")
    guide_set = set(guide_indices)
    output = []
    for index, strand in enumerate(strands):
        result = _strand(strand)
        if index not in guide_set:
            nearest = min(guides, key=lambda guide: np.linalg.norm(guide.positions[0] - strand.positions[0]))
            target = nearest.resampled(len(strand.positions))
            result.positions = strand.positions * (1.0 - strength) + target.positions * strength
        output.append(result)
    return output


def mirror(hair, axis=0, center=0.0):
    if axis not in (0, 1, 2) or not np.isfinite(center):
        raise ValueError("mirror axis must be 0, 1, or 2 and center finite")
    result = hair.copy()
    for strand in result.strands:
        strand.positions[:, axis] = 2.0 * center - strand.positions[:, axis]
    return result


def select(hair, groups=None, tags=None, profile_ids=None):
    groups = set(groups) if groups is not None else None
    tags = set(tags) if tags is not None else None
    profile_ids = set(profile_ids) if profile_ids is not None else None
    strands = []
    for strand in hair.strands:
        if groups is not None and strand.group_id not in groups:
            continue
        if tags is not None and not tags <= strand.tags:
            continue
        if profile_ids is not None and strand.profile_id not in profile_ids:
            continue
        strands.append(strand.copy())
    return Hair(strands, dict(hair.attributes))


def attach_to_surface(strand, vertices, faces, strength=1.0):
    from .surface import nearest_triangles
    if not 0 <= strength <= 1 or not np.isfinite(strength):
        raise ValueError("surface attachment strength must be in [0, 1]")
    result = _strand(strand)
    projected, _, _ = nearest_triangles(result.positions, vertices, faces)
    result.positions = result.positions * (1.0 - strength) + projected * strength
    return result


def grid_deform(hair, controls, radius):
    if radius <= 0 or not np.isfinite(radius):
        raise ValueError("grid deformation radius must be finite and positive")
    controls = [(np.asarray(center, dtype=np.float64), np.asarray(delta, dtype=np.float64))
                for center, delta in controls]
    if not controls or any(center.shape != (3,) or delta.shape != (3,) for center, delta in controls):
        raise ValueError("grid deformation requires at least one 3D control")
    result = hair.copy()
    for strand in result.strands:
        for point_index, point in enumerate(strand.positions):
            offsets = []
            weights = []
            for center, delta in controls:
                distance = np.linalg.norm(point - center)
                if distance < 1e-12:
                    offsets = [delta]
                    weights = [1.0]
                    break
                weight = max(1.0 - distance / radius, 0.0)
                if weight:
                    offsets.append(delta)
                    weights.append(weight)
            if weights:
                strand.positions[point_index] += np.sum(
                    np.asarray(offsets) * np.asarray(weights)[:, np.newaxis], axis=0)
    return result
