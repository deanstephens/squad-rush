import bpy, sys, math, os
out = sys.argv[sys.argv.index("--") + 1]
src = "ThirdParty/Source/kenney_blaster-kit/Models/GLB format"
bpy.ops.wm.read_factory_settings(use_empty=True)
names = [f"blaster-{c}" for c in "abcdefghijklmnopqr"]
for i, n in enumerate(names):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(src, n + ".glb"))
    new = [o for o in bpy.data.objects if o not in before]
    root = bpy.data.objects.new(n, None); bpy.context.scene.collection.objects.link(root)
    for o in new:
        if o.parent is None: o.parent = root
    col, row = i % 6, i // 6
    # lay each gun on its side: barrel (+Y) points to screen-right, viewed from -Y... camera looks down -Z
    root.rotation_euler = (0, math.radians(90), math.radians(-90))
    root.location = (col * 1.8, -row * 1.5, 0)
    txt = bpy.data.curves.new(n + "_t", 'FONT'); txt.body = n.split('-')[1].upper(); txt.size = 0.3
    t = bpy.data.objects.new(n + "_label", txt); bpy.context.scene.collection.objects.link(t)
    t.location = (col * 1.8 - 0.1, -row * 1.5 - 0.6, 1)
cam = bpy.data.cameras.new("cam"); cam.type = 'ORTHO'; cam.ortho_scale = 11
co = bpy.data.objects.new("cam", cam); bpy.context.scene.collection.objects.link(co)
co.location = (4.5, -1.5, 10); co.rotation_euler = (0, 0, 0)
bpy.context.scene.camera = co
s = bpy.context.scene
s.render.engine = 'BLENDER_WORKBENCH'
s.display.shading.light = 'STUDIO'; s.display.shading.color_type = 'TEXTURE'
s.render.resolution_x, s.render.resolution_y = 1200, 700
s.render.filepath = out
bpy.ops.render.render(write_still=True)
print("RENDERED", out)
