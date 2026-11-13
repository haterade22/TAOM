bl_info = {
    "name": "TAOM Retopology",
    "category": "Mesh",
    "author": "TAOM",
    "description": "Independent UV-guided garment retopology and editable quad workflows",
    "version": (1, 0, 0),
    "location": "View3D > Sidebar > TAOM",
    "blender": (4, 5, 0),
}

try:
    from tools.blender import retopo_operators
except ImportError:
    from . import retopo_operators


def register():
    retopo_operators.register()


def unregister():
    retopo_operators.unregister()
