"""Ribbon rigs, normalized weights, and deterministic spring simulation."""

import numpy as np

from .curves import resample_arc_length


def ribbon_bones(strand, count):
    if int(count) != count or count < 1:
        raise ValueError("bone count must be a positive integer")
    points = resample_arc_length(strand.positions, int(count) + 1)
    bones = []
    for index in range(int(count)):
        bones.append({
            "name": f"hair_bone_{index:03d}",
            "parent": f"hair_bone_{index - 1:03d}" if index else None,
            "head": points[index].copy(),
            "tail": points[index + 1].copy(),
        })
    return bones


def ribbon_weights(strand, count):
    bones = ribbon_bones(strand, count)
    output = []
    for t in np.linspace(0.0, 1.0, len(strand.positions)):
        position = t * (len(bones) - 1)
        if position >= len(bones) - 1:
            output.append({bones[-1]["name"]: 1.0})
            continue
        lower = int(np.floor(position))
        fraction = position - lower
        row = {}
        if fraction < 1.0:
            row[bones[lower]["name"]] = 1.0 - fraction
        if fraction > 0:
            row[bones[lower + 1]["name"]] = fraction
        output.append(row)
    return output


class SpringSimulation:
    def __init__(self, strands, gravity=None, damping=0.98, collision_spheres=()):
        self.strands = [strand.copy() for strand in strands]
        self.gravity = np.zeros(3) if gravity is None else np.asarray(gravity, dtype=np.float64)
        if self.gravity.shape != (3,) or not np.isfinite(self.gravity).all():
            raise ValueError("gravity must be a finite 3D vector")
        if not 0 <= damping <= 1:
            raise ValueError("damping must be in [0, 1]")
        self.damping = float(damping)
        self.previous = [strand.positions.copy() for strand in self.strands]
        self.rest_lengths = [np.linalg.norm(np.diff(strand.positions, axis=0), axis=1)
                             for strand in self.strands]
        self.collision_spheres = []
        for center, radius in collision_spheres:
            center = np.asarray(center, dtype=np.float64)
            if center.shape != (3,) or radius <= 0 or not np.isfinite(radius):
                raise ValueError("collision spheres need a finite center and positive radius")
            self.collision_spheres.append((center, float(radius)))

    def step(self, dt, iterations=4):
        if dt <= 0 or not np.isfinite(dt):
            raise ValueError("simulation dt must be finite and positive")
        if int(iterations) != iterations or iterations < 1:
            raise ValueError("iterations must be a positive integer")
        for index, strand in enumerate(self.strands):
            current = strand.positions.copy()
            velocity = (current - self.previous[index]) * self.damping
            strand.positions += velocity + self.gravity * (dt * dt)
            self.previous[index] = current
            for _ in range(int(iterations)):
                strand.positions[0] = self.previous[index][0]
                for segment, rest_length in enumerate(self.rest_lengths[index]):
                    a, b = segment, segment + 1
                    delta = strand.positions[b] - strand.positions[a]
                    length = np.linalg.norm(delta)
                    if length == 0:
                        continue
                    correction = delta * ((length - rest_length) / length)
                    if a == 0:
                        strand.positions[b] -= correction
                    else:
                        strand.positions[a] += correction * 0.5
                        strand.positions[b] -= correction * 0.5
                for center, radius in self.collision_spheres:
                    offsets = strand.positions - center
                    distances = np.linalg.norm(offsets, axis=1)
                    inside = (distances < radius) & (distances > 0)
                    strand.positions[inside] = center + offsets[inside] / distances[inside, np.newaxis] * radius
            strand.positions[0] = self.previous[index][0]
        return [strand.positions.copy() for strand in self.strands]


def shape_transfer(source, target):
    if len(source.strands) != 1 or len(target.strands) != 1:
        raise ValueError("shape transfer currently requires one source and one target strand")
    source_positions = source.strands[0].positions
    target_positions = target.strands[0].positions
    if source_positions.shape != target_positions.shape:
        raise ValueError("shape transfer requires matching point topology")
    return {"Basis": source_positions.copy(), "Shape_001": source_positions.copy()}


def apply_shape(hair, shapes, name, weight=1.0):
    if name not in shapes:
        raise ValueError(f"unknown shape {name!r}")
    if not 0 <= weight <= 1:
        raise ValueError("shape weight must be in [0, 1]")
    result = hair.copy()
    if len(result.strands) != 1 or result.strands[0].positions.shape != shapes[name].shape:
        raise ValueError("shape topology does not match the hair")
    result.strands[0].positions = (result.strands[0].positions * (1.0 - weight)
                                   + shapes[name] * weight)
    return result


def sample_surface_weights(vertices, faces, weights, points):
    from .surface import nearest_triangles
    if len(weights) != len(vertices):
        raise ValueError("weights must have one dictionary per surface vertex")
    nearest, face_ids, barycentrics = nearest_triangles(points, vertices, faces)
    output = []
    for face_index, shares in zip(face_ids, barycentrics):
        mixed = {}
        for vertex_index, share in zip(faces[face_index], shares):
            for bone, weight in weights[vertex_index].items():
                mixed[bone] = mixed.get(bone, 0.0) + float(weight) * float(share)
        total = sum(mixed.values())
        if total <= 0:
            raise ValueError("sampled surface weights are empty")
        ordered = sorted(mixed.items(), key=lambda item: (-item[1], item[0]))[:4]
        output.append({bone: weight / total for bone, weight in ordered})
    return output
