import bpy, sys
paths = sys.argv[sys.argv.index("--") + 1:]
for p in paths:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=p)
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    arms = [o for o in bpy.data.objects if o.type == 'ARMATURE']
    tris = sum(sum(len(poly.vertices) - 2 for poly in o.data.polygons) for o in meshes)
    print("=== ", p.split('/')[-1], "meshes:", len(meshes), "tris:", tris)
    print("   mesh names:", [o.name for o in meshes][:20])
    for a in arms:
        bones = [b.name for b in a.data.bones]
        print("   armature", a.name, "bones", len(bones), bones[:40])
    print("   actions:", len(bpy.data.actions), [a.name for a in bpy.data.actions][:80])
    print("   images:", [(i.name, tuple(i.size)) for i in bpy.data.images])
    xs = [ (o.matrix_world @ v.co) for o in meshes for v in o.data.vertices ]
    if xs:
        mn = [min(v[i] for v in xs) for i in range(3)]; mx = [max(v[i] for v in xs) for i in range(3)]
        print("   bounds min", [round(x,3) for x in mn], "max", [round(x,3) for x in mx])
