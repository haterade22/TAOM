"""Legacy particle-hair interchange with independent strand data."""

import numpy as np

from .model import Hair, Strand


def curves_to_particles(hair):
    output = []
    for strand in hair.strands:
        output.append({
            "positions": strand.positions.copy(),
            "radius": strand.radius.copy(),
            "tilt": strand.tilt.copy(),
            "profile_id": strand.profile_id,
            "uv_region": strand.uv_region,
            "group_id": strand.group_id,
            "tags": strand.tags,
            "point_attributes": {name: values.copy() for name, values in strand.point_attributes.items()},
        })
    return output


def particles_to_curves(particles):
    strands = []
    for particle in particles:
        if isinstance(particle, dict):
            strands.append(Strand(
                np.asarray(particle["positions"]).copy(),
                particle.get("radius"),
                particle.get("tilt"),
                particle.get("profile_id", 0),
                particle.get("uv_region", -1),
                particle.get("group_id", 0),
                particle.get("tags", frozenset()),
                particle.get("point_attributes", {}),
            ))
        else:
            strands.append(Strand(np.asarray(particle).copy()))
    return Hair(strands)


def scale_particles(particles, factor):
    if factor <= 0 or not np.isfinite(factor):
        raise ValueError("particle scale must be finite and positive")
    output = []
    for particle in particles:
        positions = np.asarray(particle["positions"] if isinstance(particle, dict) else particle)
        scaled = positions[0] + (positions - positions[0]) * factor
        if isinstance(particle, dict):
            clone = dict(particle)
            clone["positions"] = scaled
            output.append(clone)
        else:
            output.append(scaled)
    return output
