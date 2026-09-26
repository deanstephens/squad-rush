"""
Converts the CC0 source packs in ThirdParty/Source into game-ready assets under Assets/Art.

  Characters  -> one joined skinned mesh + deform rig per character, no animation (FBX)
  Animations  -> one clip library per rig family (heroes / skeletons), armature only (FBX)
  Guns        -> one joined static mesh per gun (FBX) + a transparent icon render (PNG)

Run:  Blender -b --factory-startup --python Tools/blender/process_models.py [-- textures characters anims guns props]
"""
import bpy, os, math, shutil
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "ThirdParty", "Source")
ADV = os.path.join(SRC, "KayKit-Character-Pack-Adventures-1.0/addons/kaykit_character_pack_adventures/Characters/gltf")
SKL = os.path.join(SRC, "KayKit-Character-Pack-Skeletons-1.0/addons/kaykit_character_pack_skeletons/Characters/gltf")
SKL_ASSETS = os.path.join(SRC, "KayKit-Character-Pack-Skeletons-1.0/addons/kaykit_character_pack_skeletons/Assets/gltf")
GUNS = os.path.join(SRC, "kenney_blaster-kit/Models/GLB format")

OUT_MODELS = os.path.join(ROOT, "Assets/Art/Models")
OUT_TEX = os.path.join(ROOT, "Assets/Art/Textures")
OUT_ICONS = os.path.join(ROOT, "Assets/Art/Icons")
for d in (OUT_MODELS + "/Characters", OUT_MODELS + "/Animations", OUT_MODELS + "/Guns", OUT_MODELS + "/Props", OUT_TEX, OUT_ICONS):
    os.makedirs(d, exist_ok=True)

# name in game -> (source file, mesh-name prefix to keep)
CHARACTERS = {
    "Hero_Knight":      (os.path.join(ADV, "Knight.glb"),      "Knight_"),
    "Hero_Barbarian":   (os.path.join(ADV, "Barbarian.glb"),   "Barbarian_"),
    "Hero_Rogue":       (os.path.join(ADV, "Rogue.glb"),       "Rogue_"),
    "Hero_Mage":        (os.path.join(ADV, "Mage.glb"),        "Mage_"),
    "Hero_RogueHooded": (os.path.join(ADV, "Rogue_Hooded.glb"), "Rogue"),
    "Skeleton_Minion":  (os.path.join(SKL, "Skeleton_Minion.glb"),  "Skeleton_Minion_"),
    "Skeleton_Rogue":   (os.path.join(SKL, "Skeleton_Rogue.glb"),   "Skeleton_Rogue_"),
    "Skeleton_Warrior": (os.path.join(SKL, "Skeleton_Warrior.glb"), "Skeleton_Warrior_"),
    "Skeleton_Mage":    (os.path.join(SKL, "Skeleton_Mage.glb"),    "Skeleton_Mage_"),
}

ANIM_SETS = {
    "Anim_Heroes": (os.path.join(ADV, "Rogue.glb"), [
        "Idle", "Running_A", "Running_B", "1H_Ranged_Shooting", "2H_Ranged_Shooting",
        "1H_Ranged_Aiming", "2H_Ranged_Aiming", "Cheer", "Hit_A", "Death_A"]),
    "Anim_Skeletons": (os.path.join(SKL, "Skeleton_Minion.glb"), [
        "Walking_D_Skeletons", "Walking_A", "Running_C", "Running_A", "Idle_Combat",
        "1H_Melee_Attack_Chop", "2H_Melee_Attack_Chop", "Spellcast_Shoot", "Hit_A", "Taunt"]),
}

GUN_MAP = {
    "pistol": "blaster-b", "smg": "blaster-n", "shotgun": "blaster-h",
    "rifle": "blaster-f", "minigun": "blaster-q", "rocket": "blaster-a",
}

PROPS = {   # weapons for the skeleton enemies (static meshes attached to hand slots in Unity)
    "Skel_Axe": os.path.join(SKL_ASSETS, "Skeleton_Axe.gltf"),
    "Skel_Blade": os.path.join(SKL_ASSETS, "Skeleton_Blade.gltf"),
    "Skel_Staff": os.path.join(SKL_ASSETS, "Skeleton_Staff.gltf"),
    "Skel_Shield": os.path.join(SKL_ASSETS, "Skeleton_Shield_Large_A.gltf"),
}


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_any(path):
    bpy.ops.import_scene.gltf(filepath=path)


def objs(kind):
    return [o for o in bpy.data.objects if o.type == kind]


def select_only(objects, active=None):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = active or (objects[0] if objects else None)


def copy_textures():
    for folder in (ADV, SKL):
        for f in os.listdir(folder):
            if f.endswith(".png"):
                shutil.copy2(os.path.join(folder, f), os.path.join(OUT_TEX, f))
    shutil.copy2(os.path.join(GUNS, "Textures", "colormap.png"), os.path.join(OUT_TEX, "blaster_colormap.png"))


def prep_rig(arm):
    # Keep deform bones plus the hand slots (used to attach weapons in Unity).
    for b in arm.data.bones:
        if b.name.startswith("handslot"):
            b.use_deform = True


def bind_bone_parented(mesh, arm):
    """Meshes parented straight to a bone (helmets, hats) become skinned to that bone so they survive the join."""
    if mesh.parent != arm or mesh.parent_type != 'BONE':
        return
    bone = mesh.parent_bone
    mw = mesh.matrix_world.copy()
    mesh.parent_type = 'OBJECT'
    mesh.parent_bone = ""
    mesh.matrix_world = mw
    vg = mesh.vertex_groups.get(bone) or mesh.vertex_groups.new(name=bone)
    vg.add(range(len(mesh.data.vertices)), 1.0, 'REPLACE')
    if not any(m.type == 'ARMATURE' for m in mesh.modifiers):
        mod = mesh.modifiers.new("Armature", 'ARMATURE')
        mod.object = arm


def export_fbx(path, objects, with_anim):
    select_only(objects)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH', 'EMPTY'},
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        bake_space_transform=False, use_armature_deform_only=True, add_leaf_bones=False,
        mesh_smooth_type='FACE', use_mesh_modifiers=True, path_mode='STRIP',
        bake_anim=with_anim, bake_anim_use_all_actions=with_anim, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.5)


def process_character(name, path, prefix):
    reset()
    import_any(path)
    arm = objs('ARMATURE')[0]
    arm.name = "Rig"
    prep_rig(arm)
    if arm.animation_data:
        arm.animation_data.action = None
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    bpy.context.scene.frame_set(0)

    keep, drop = [], []
    for o in objs('MESH'):
        (keep if o.name.startswith(prefix) and o.name != "Icosphere" else drop).append(o)
    for o in drop:
        bpy.data.objects.remove(o, do_unlink=True)
    for o in keep:
        bind_bone_parented(o, arm)

    select_only(keep, keep[0])
    bpy.ops.object.join()
    body = bpy.context.view_layer.objects.active
    body.name = name + "_Mesh"
    tris = sum(len(p.vertices) - 2 for p in body.data.polygons)
    zs = [(body.matrix_world @ v.co).z for v in body.data.vertices]
    print(f"CHAR {name}: parts={len(keep)} tris={tris} height={max(zs)-min(zs):.2f} minZ={min(zs):.2f}")
    export_fbx(os.path.join(OUT_MODELS, "Characters", name + ".fbx"), [arm, body], with_anim=False)


def process_anims(name, path, wanted):
    reset()
    import_any(path)
    arm = objs('ARMATURE')[0]
    arm.name = "Rig"
    prep_rig(arm)
    for o in objs('MESH'):
        bpy.data.objects.remove(o, do_unlink=True)
    missing = [w for w in wanted if w not in bpy.data.actions]
    for a in list(bpy.data.actions):
        if a.name not in wanted:
            bpy.data.actions.remove(a)
    if not arm.animation_data:
        arm.animation_data_create()
    arm.animation_data.action = bpy.data.actions[wanted[0]]
    print(f"ANIM {name}: {len(bpy.data.actions)} clips, missing={missing}")
    # A second top-level object stops Unity collapsing "Rig" into the file root, so clip paths
    # stay "Rig/root/..." exactly like the character files.
    anchor = bpy.data.objects.new("AnimAnchor", None)
    bpy.context.scene.collection.objects.link(anchor)
    export_fbx(os.path.join(OUT_MODELS, "Animations", name + ".fbx"), [arm, anchor], with_anim=True)


def join_static(name):
    meshes = objs('MESH')
    for o in meshes:
        mw = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = mw
    for o in [o for o in bpy.data.objects if o.type == 'EMPTY']:
        bpy.data.objects.remove(o, do_unlink=True)
    select_only(meshes, meshes[0])
    if len(meshes) > 1:
        bpy.ops.object.join()
    m = bpy.context.view_layer.objects.active
    m.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return m


def render_icon(obj, out_png):
    """Side-on icon with transparent background: barrel (+Y) to the right."""
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'TEXTURE'
    scene.display.shading.show_cavity = True
    scene.display.shading.show_object_outline = True
    scene.display.shading.object_outline_color = (0.05, 0.05, 0.08)
    scene.render.film_transparent = True
    scene.render.resolution_x = scene.render.resolution_y = 256
    scene.view_settings.view_transform = 'Standard'
    bb = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    center = sum(bb, Vector()) / 8.0
    size = max((max(v[i] for v in bb) - min(v[i] for v in bb)) for i in range(3))
    cam = bpy.data.cameras.new("IconCam"); cam.type = 'ORTHO'; cam.ortho_scale = size * 1.15
    co = bpy.data.objects.new("IconCam", cam); scene.collection.objects.link(co)
    co.location = center + Vector((size * 3, 0, size * 0.35))   # looking from +X, slightly above
    co.rotation_euler = (math.radians(84), 0, math.radians(90))
    scene.camera = co
    scene.render.filepath = out_png
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(co, do_unlink=True)


def process_gun(game_id, src_name):
    reset()
    import_any(os.path.join(GUNS, src_name + ".glb"))
    gun = join_static("Gun_" + game_id)
    bb = [gun.matrix_world @ Vector(c) for c in gun.bound_box]
    print(f"GUN {game_id} ({src_name}): tris={sum(len(p.vertices)-2 for p in gun.data.polygons)} "
          f"x[{min(v.x for v in bb):.2f},{max(v.x for v in bb):.2f}] y[{min(v.y for v in bb):.2f},{max(v.y for v in bb):.2f}] "
          f"z[{min(v.z for v in bb):.2f},{max(v.z for v in bb):.2f}]")
    render_icon(gun, os.path.join(OUT_ICONS, "Icon_" + game_id + ".png"))
    export_fbx(os.path.join(OUT_MODELS, "Guns", "Gun_" + game_id + ".fbx"), [gun], with_anim=False)


def process_prop(name, path):
    reset()
    import_any(path)
    prop = join_static(name)
    export_fbx(os.path.join(OUT_MODELS, "Props", name + ".fbx"), [prop], with_anim=False)


import sys
stages = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["all"]
run = lambda s: "all" in stages or s in stages
if run("textures"):
    copy_textures()
if run("characters"):
    for n, (p, pre) in CHARACTERS.items():
        process_character(n, p, pre)
if run("anims"):
    for n, (p, wanted) in ANIM_SETS.items():
        process_anims(n, p, wanted)
if run("guns"):
    for gid, src in GUN_MAP.items():
        process_gun(gid, src)
if run("props"):
    for n, p in PROPS.items():
        process_prop(n, p)
print("DONE", stages)
