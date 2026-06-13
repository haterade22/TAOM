# Spider config layer over the elephant harness. exec this instead of harness.py for spider work:
#   exec(open(r'E:\LOTRAOMAssets\_auto_workspace\_spider_refine\spider_cfg.py').read(), globals())
import os
exec(open(r'E:\LOTRAOMAssets\Elephant\_refine_tools\harness.py').read(), globals())

ARM_NAME = 'spider_skeleton'
BODY_NAME = 'sk_spider_forest_a'
ROOT = 'root_m'
ARMOR = []
RENDER_DIR = r'E:\LOTRAOMAssets\_auto_workspace\_spider_refine\renders'
os.makedirs(RENDER_DIR, exist_ok=True)
SPIDER_BODIES = ['sk_spider_forest_a', 'sk_spider_forest_b', 'sk_spider_forest_c']

# Tentative leg-tip "feet" (4 weight-bearing pairs). VERIFY against rig geometry before gait analysis.
FEET = {'L1': 'joint38_l', 'R1': 'joint38_r', 'L2': 'joint44_l', 'R2': 'joint44_r',
        'L3': 'joint21_l', 'R3': 'joint21_r', 'L4': 'joint32_l', 'R4': 'joint32_r'}

def hide_lods():
    for o in bpy.data.objects:
        if o.type == 'MESH':
            o.hide_render = o.hide_viewport = ('.lod' in o.name.lower())

def _saim(loc, target, margin=1.3):
    aim_cam(loc, target, margin=margin)

def _bb():
    mn, mx = world_bbox(SPIDER_BODIES)
    c = (mn + mx) / 2
    span = max(mx.x - mn.x, mx.y - mn.y, mx.z - mn.z)
    return mn, mx, c, span

def view_side():
    mn, mx, c, span = _bb(); aim_cam((c.x - span * 3, c.y, c.z), c)

def view_front():
    mn, mx, c, span = _bb(); aim_cam((c.x, c.y - span * 3, c.z), c)

def view_top():
    mn, mx, c, span = _bb(); aim_cam((c.x, c.y - span * 0.01, c.z + span * 3), c)  # top-down: 8-leg phasing

def view_3q():
    mn, mx, c, span = _bb(); aim_cam((c.x - span * 2.2, c.y - span * 2.2, c.z + span * 1.6), c, margin=1.5)
