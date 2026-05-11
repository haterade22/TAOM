import bpy

class BoneMappingItem(bpy.types.PropertyGroup):
    target_bone_name: bpy.props.StringProperty(name="Target Bone")
    source_bone_name: bpy.props.StringProperty(name="Source Bone")

class BONE_UL_MappingList(bpy.types.UIList):
    def draw_item(self, context, layout, data, item, icon, active_data, active_propname):
        scene = context.scene
        if self.layout_type in {'DEFAULT', 'COMPACT'}:
            row = layout.row(align=True)
            row.label(text=item.target_bone_name, icon='BONE_DATA')
            row.label(text="←", translate=False)
            if scene.source_armature and scene.source_armature.type == 'ARMATURE':
                row.prop_search(item, "source_bone_name", scene.source_armature.data, "bones", text="")
            else:
                row.label(text="Set Source!")

class OT_PopulateBoneList(bpy.types.Operator):
    bl_idname = "armature.populate_bone_list"
    bl_label = "Scan Target Bones"
    
    def execute(self, context):
        scene = context.scene
        target = scene.target_armature
        source = scene.source_armature
        if not target:
            self.report({'ERROR'}, "Select Target Armature!")
            return {'CANCELLED'}
        scene.bone_mapping_list.clear()
        for bone in target.data.bones:
            item = scene.bone_mapping_list.add()
            item.target_bone_name = bone.name
            if source and bone.name in source.data.bones:
                item.source_bone_name = bone.name
        return {'FINISHED'}

class OT_ApplySplitConstraints(bpy.types.Operator):
    bl_idname = "armature.apply_split_constraints"
    bl_label = "Apply Loc/Rot Constraints"
    
    def execute(self, context):
        scene = context.scene
        src = scene.source_armature
        tgt = scene.target_armature
        if not src or not tgt:
            self.report({'ERROR'}, "Source or Target is missing!")
            return {'CANCELLED'}
        context.view_layer.objects.active = tgt
        bpy.ops.object.mode_set(mode='POSE')
        for item in scene.bone_mapping_list:
            if item.source_bone_name:
                pbone = tgt.pose.bones.get(item.target_bone_name)
                if pbone:
                    prefix = "Temp_Map_"
                    for c in list(pbone.constraints):
                        if c.name.startswith(prefix):
                            pbone.constraints.remove(c)
                    loc = pbone.constraints.new('COPY_LOCATION')
                    loc.name = f"{prefix}Location"
                    loc.target = src
                    loc.subtarget = item.source_bone_name
                    rot = pbone.constraints.new('COPY_ROTATION')
                    rot.name = f"{prefix}Rotation"
                    rot.target = src
                    rot.subtarget = item.source_bone_name
        return {'FINISHED'}

class OT_BakeAndCleanup(bpy.types.Operator):
    bl_idname = "armature.bake_and_cleanup"
    bl_label = "Bake & Cleanup"
    
    def execute(self, context):
        scene = context.scene
        bpy.ops.nla.bake('INVOKE_DEFAULT', visual_keying=True, clear_constraints=True, bake_types={'POSE'})
        if scene.delete_source_after_bake:
            src = scene.source_armature
            if src:
                bpy.data.objects.remove(src, do_unlink=True)
        return {'FINISHED'}

class ARMATURE_PT_MappingPanel(bpy.types.Panel):
    bl_label = "Advanced Bone Mapping (Loc/Rot)"
    bl_idname = "ARMATURE_PT_mapping_panel"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = 'Technical Art'

    def draw(self, context):
        layout = self.layout
        scene = context.scene
        col = layout.column(align=True)
        col.prop(scene, "source_armature", text="Source (Maya)")
        col.prop(scene, "target_armature", text="Target (Blender)")
        layout.separator()
        layout.operator("armature.populate_bone_list", icon='FILE_REFRESH')
        layout.template_list("BONE_UL_MappingList", "", scene, "bone_mapping_list", scene, "bone_mapping_index")
        col = layout.column(align=True)
        col.operator("armature.apply_split_constraints", icon='CONSTRAINT_BONE')
        layout.separator()
        box = layout.box()
        box.label(text="Bake Actions:", icon='REC')
        box.prop(scene, "delete_source_after_bake")
        box.operator("armature.bake_and_cleanup", text="Bake Animation")

classes = (
    BoneMappingItem,
    BONE_UL_MappingList,
    OT_PopulateBoneList,
    OT_ApplySplitConstraints,
    OT_BakeAndCleanup,
    ARMATURE_PT_MappingPanel,
)

def register():
    for cls in classes:
        bpy.utils.register_class(cls)
    bpy.types.Scene.source_armature = bpy.props.PointerProperty(type=bpy.types.Object, poll=lambda s, obj: obj.type == 'ARMATURE')
    bpy.types.Scene.target_armature = bpy.props.PointerProperty(type=bpy.types.Object, poll=lambda s, obj: obj.type == 'ARMATURE')
    bpy.types.Scene.bone_mapping_list = bpy.props.CollectionProperty(type=BoneMappingItem)
    bpy.types.Scene.bone_mapping_index = bpy.props.IntProperty()
    bpy.types.Scene.delete_source_after_bake = bpy.props.BoolProperty(name="Delete Source after Bake", default=False)

def unregister():
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
    del bpy.types.Scene.source_armature
    del bpy.types.Scene.target_armature
    del bpy.types.Scene.bone_mapping_list
    del bpy.types.Scene.bone_mapping_index
    del bpy.types.Scene.delete_source_after_bake

if __name__ == "__main__":
    register()