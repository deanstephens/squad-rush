import bpy, sys
p = sys.argv[sys.argv.index("--") + 1]
bpy.ops.import_scene.gltf(filepath=p)
for a in bpy.data.actions:
    print("ACT", a.name, round((a.frame_range[1]-a.frame_range[0])/30.0, 2), "s")
