"""Legacy Blender particle-hair interchange for the independent engine."""

from pathlib import Path
import sys

import numpy as np

TOOLS = Path(__file__).resolve().parents[1]
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

from hair_engine.model import Hair, Strand


def particles_to_hair(obj):
    import bpy
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    if not evaluated.particle_systems:
        raise ValueError("object has no particle system")
    system = evaluated.particle_systems.active
    if system.settings.type != "HAIR":
        raise ValueError("active particle system is not hair")
    strands = []
    for particle in system.particles:
        positions = [tuple(key.co) for key in particle.hair_keys]
        if len(positions) >= 2:
            strands.append(Strand(positions))
    return Hair(strands)


def hair_to_particles(hair, obj):
    import bpy
    if not hair.strands:
        raise ValueError("cannot create particles from empty hair")
    point_count = max(len(strand.positions) for strand in hair.strands)
    strands = [strand.resampled(point_count) for strand in hair.strands]
    system = obj.particle_systems.active
    if system is None:
        obj.modifiers.new("TAOM_Hair_Particles", "PARTICLE_SYSTEM")
        system = obj.particle_systems.active
    system.settings.type = "HAIR"
    system.settings.count = len(strands)
    system.settings.hair_step = point_count - 1
    system.settings.display_step = max(2, min(6, point_count - 1))
    system.settings.hair_length = max(
        sum(np.linalg.norm(strand.positions[-1] - strand.positions[0])
            for strand in strands) / len(strands), 0.0001)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.particle.particle_edit_toggle()
    bpy.ops.particle.particle_edit_toggle()
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    evaluated_system = evaluated.particle_systems.active
    if len(evaluated_system.particles) != len(strands):
        raise ValueError("Blender did not create the requested particle count")
    for particle, strand in zip(evaluated_system.particles, strands):
        particle.location = strand.positions[0]
        for key, position in zip(particle.hair_keys, strand.positions):
            key.co = position
    bpy.ops.particle.particle_edit_toggle()
    bpy.ops.particle.particle_edit_toggle()
    return system
