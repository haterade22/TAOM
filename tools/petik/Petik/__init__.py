
bl_info = {
    "name": "PetiK",
    "category": "Mesh",
    "author": "Mihai Dobrin",
    "description": "Creates quad mesh patches",
    "version": (2, 0, 7),
    "location": "Edit > Mesh > Petik",
    "blender": (4, 5, 0),
    "tracker_url": "",
    "wiki_url": "",
}


from . import (
    op_md_petik,
    petik_preferences,
)

# TAOM compatibility: import both modules explicitly before reloading them.
if "bpy" in locals():
    import importlib

    importlib.reload(op_md_petik)
    importlib.reload(petik_preferences)

import bpy
import rna_keymap_ui
from bpy.types import Menu

addon_keymaps = []

def menu_func(self, context):
    layout = self.layout
    layout.separator()
    layout.operator_context = "INVOKE_DEFAULT"
    layout.operator(op_md_petik.MdUtilMultiPetik.bl_idname, text='Petik')


classes = [
    petik_preferences.PetikAddonPreferences,
    op_md_petik.MdUtilMultiPetik,
    op_md_petik.MdUtilPetikLine,
    op_md_petik.PetikAddonPreferences,
]


def register():
    global addon_keymaps
    for c in classes:
        bpy.utils.register_class(c)

    #bpy.types.VIEW3D_MT_edit_mesh.append(menu_func)
    # add keymap entry
    kcfg = bpy.context.window_manager.keyconfigs.addon
    if kcfg:
        km = kcfg.keymaps.new(name='Mesh', space_type='EMPTY')
        kmi = km.keymap_items.new('mdutil.multi_petik', 'X', 'PRESS', ctrl=True, alt=True, shift=True)
        addon_keymaps.append((km, kmi))
        km = kcfg.keymaps.new(name='Mesh', space_type='EMPTY')
        kmi = km.keymap_items.new('mdutil.petik_line', 'Z', 'PRESS', ctrl=True, alt=True, shift=True)
        addon_keymaps.append((km, kmi))


def unregister():

    for c in classes:
        bpy.utils.unregister_class(c)

    #bpy.types.VIEW3D_MT_edit_mesh.remove(menu_func)

    for km, kmi in addon_keymaps:
        km.keymap_items.remove(kmi)
    addon_keymaps.clear()




