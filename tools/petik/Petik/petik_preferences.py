import bpy
from bpy.props import BoolProperty, IntProperty
from bpy.app.translations import contexts as i18n_contexts
import rna_keymap_ui

# Preferences
def km_operators():
    return (
        "Mesh",
        {"space_type": 'EMPTY', "region_type": 'WINDOW'},
        {"items": [
            ("mdutils.petik", {"type": 'X', "value": 'PRESS', "ctrl": True, "alt": True, "shift": True},
             {"properties": [("wait_for_input", True)],
              "active": True}),

            ("mdutils.petik_line", {"type": 'Z', "value": 'PRESS', "ctrl": True, "alt": True, "shift": True},
             {"properties": [("wait_for_input", True)],
              "active": True}),
        ]},
    )

def utilities_global_keymaps():
    return [
        km_operators(),
    ]

class PetikAddonPreferences(bpy.types.AddonPreferences):
    bl_idname = __package__


    bpy.types.Scene.Info = bpy.props.BoolProperty(default=True)
    switch_Q_Space: BoolProperty(
        name="Switch Q and Space keys in the modal operator",
        default=False,
    )
    ini_line_verts: IntProperty(name="Initial line divisions", default=4)

    def draw(self, context):
        layout = self.layout

        layout.prop(context.scene, "Info", text="Info", icon="QUESTION")
        if context.scene.Info:
            row = layout.row()
            layout.label(
                text="This addon is designed to help fill or create variable side patches in a retopology context.")

        layout.prop(self, "switch_Q_Space")
        layout.prop(self, "ini_line_verts")

        wm = context.window_manager
        kc = wm.keyconfigs.user

        layout.label(text="KeyMap:")

        for km_name, km_args, km_content in utilities_global_keymaps():
            km = kc.keymaps.get(km_name)
            if km:
                self.draw_km(kc, km, layout)


    @staticmethod
    def draw_km(kc, km, layout):
        layout.context_pointer_set("keymap", km)

        row = layout.row()
        row.prop(km, "show_expanded_items", text="", emboss=False)
        row.label(text=km.name, text_ctxt=i18n_contexts.id_windowmanager)

        if km.show_expanded_items:
            col = layout.column()

            for kmi in km.keymap_items:
                if "mdutil.multi_petik" in kmi.idname:
                    rna_keymap_ui.draw_kmi(["ADDON", "USER", "DEFAULT"], kc, km, kmi, col, 0)
                if "mdutil.petik_line" in kmi.idname:
                    rna_keymap_ui.draw_kmi(["ADDON", "USER", "DEFAULT"], kc, km, kmi, col, 0)