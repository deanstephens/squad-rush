using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SquadRush.EditorTools
{
    /// <summary>
    /// Builds game visuals from the processed art in Assets/Art: materials, animator controllers,
    /// character instances (scaled, turned to face +Z, animated) and guns/props attached to hand slots.
    /// </summary>
    public static class ModelKit
    {
        const string Models = "Assets/Art/Models/";
        const string MatDir = "Assets/Art/Materials";
        const string CtrlDir = "Assets/Art/Animators";

        public static readonly Dictionary<string, string> TextureFor = new Dictionary<string, string>
        {
            { "Hero_Knight", "knight_texture" }, { "Hero_Barbarian", "barbarian_texture" },
            { "Hero_Rogue", "rogue_texture" }, { "Hero_Mage", "mage_texture" }, { "Hero_RogueHooded", "rogue_texture" },
            { "Skeleton_Minion", "skeleton_texture" }, { "Skeleton_Rogue", "skeleton_texture" },
            { "Skeleton_Warrior", "skeleton_texture" }, { "Skeleton_Mage", "skeleton_texture" },
        };

        public static readonly HashSet<string> TwoHandedGuns = new HashSet<string> { "smg", "shotgun", "rifle", "minigun", "rocket" };

        // ------------------------------------------------------------------ materials

        public static Material TexturedMaterial(string textureName)
        {
            Directory.CreateDirectory(MatDir);
            string path = MatDir + "/M_" + textureName + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/" + textureName + ".png"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.12f);
            mat.SetFloat("_Metallic", 0f);
            // Emission stays black; hit flashes drive _EmissionColor through a MaterialPropertyBlock.
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ clips + controllers

        public static AnimationClip Clip(string library, string name)
        {
            return AssetDatabase.LoadAllAssetsAtPath(Models + "Animations/" + library + ".fbx")
                .OfType<AnimationClip>().FirstOrDefault(c => c.name == name);
        }

        static AnimatorController NewController(string name)
        {
            Directory.CreateDirectory(CtrlDir);
            string path = CtrlDir + "/" + name + ".controller";
            AssetDatabase.DeleteAsset(path);
            return AnimatorController.CreateAnimatorControllerAtPath(path);
        }

        static AnimatorStateTransition Link(AnimatorState from, AnimatorState to, float duration = 0.15f)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            return t;
        }

        static AnimatorController Existing(string name) =>
            AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlDir + "/" + name + ".controller");

        /// <summary>Returns the saved controller, building it on first use. Prefabs keep references across scene rebuilds.</summary>
        public static AnimatorController HeroController(bool twoHanded) =>
            Existing(twoHanded ? "AC_Hero_2H" : "AC_Hero_1H") ?? BuildHeroController(twoHanded);

        public static AnimatorController SkeletonController() => Existing("AC_Skeleton") ?? BuildSkeletonController();

        /// <summary>Regenerates every controller. Run before rebuilding all scenes (SquadRush > Build All Scenes does this).</summary>
        public static void RebuildControllers()
        {
            BuildHeroController(false);
            BuildHeroController(true);
            BuildSkeletonController();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Heroes: Idle / Shoot (bool Shooting) / Run (float Speed) / Cheer (trigger).</summary>
        static AnimatorController BuildHeroController(bool twoHanded)
        {
            var ac = NewController(twoHanded ? "AC_Hero_2H" : "AC_Hero_1H");
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("Shooting", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Cheer", AnimatorControllerParameterType.Trigger);
            var sm = ac.layers[0].stateMachine;

            var idle = sm.AddState("Idle"); idle.motion = Clip("Anim_Heroes", "Idle");
            var shoot = sm.AddState("Shoot"); shoot.motion = Clip("Anim_Heroes", twoHanded ? "2H_Ranged_Shooting" : "1H_Ranged_Shooting");
            var run = sm.AddState("Run"); run.motion = Clip("Anim_Heroes", "Running_A");
            var cheer = sm.AddState("Cheer"); cheer.motion = Clip("Anim_Heroes", "Cheer");
            sm.defaultState = idle;

            Link(idle, run).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            Link(shoot, run).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            var runToShoot = Link(run, shoot);
            runToShoot.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            runToShoot.AddCondition(AnimatorConditionMode.If, 0f, "Shooting");
            var runToIdle = Link(run, idle);
            runToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            runToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Shooting");
            Link(idle, shoot).AddCondition(AnimatorConditionMode.If, 0f, "Shooting");
            Link(shoot, idle).AddCondition(AnimatorConditionMode.IfNot, 0f, "Shooting");

            var any = sm.AddAnyStateTransition(cheer);
            any.hasExitTime = false; any.duration = 0.1f; any.canTransitionToSelf = false;
            any.AddCondition(AnimatorConditionMode.If, 0f, "Cheer");
            var back = cheer.AddTransition(idle);
            back.hasExitTime = true; back.exitTime = 0.95f; back.duration = 0.15f;

            EditorUtility.SetDirty(ac);
            return ac;
        }

        /// <summary>Skeletons: Walk (default) / Run (bool Running) / Attack (bool Attacking).</summary>
        static AnimatorController BuildSkeletonController()
        {
            var ac = NewController("AC_Skeleton");
            ac.AddParameter("Running", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Attacking", AnimatorControllerParameterType.Bool);
            var sm = ac.layers[0].stateMachine;

            var walk = sm.AddState("Walk"); walk.motion = Clip("Anim_Skeletons", "Walking_D_Skeletons");
            var run = sm.AddState("Run"); run.motion = Clip("Anim_Skeletons", "Running_C");
            var attack = sm.AddState("Attack"); attack.motion = Clip("Anim_Skeletons", "2H_Melee_Attack_Chop");
            sm.defaultState = walk;

            Link(walk, run).AddCondition(AnimatorConditionMode.If, 0f, "Running");
            Link(run, walk).AddCondition(AnimatorConditionMode.IfNot, 0f, "Running");
            Link(walk, attack).AddCondition(AnimatorConditionMode.If, 0f, "Attacking");
            Link(run, attack).AddCondition(AnimatorConditionMode.If, 0f, "Attacking");
            Link(attack, walk).AddCondition(AnimatorConditionMode.IfNot, 0f, "Attacking");

            EditorUtility.SetDirty(ac);
            return ac;
        }

        // ------------------------------------------------------------------ characters

        /// <summary>
        /// Adds a "Visual" child holding the character model, turned to face +Z and scaled to <paramref name="height"/>.
        /// </summary>
        public static GameObject Character(string model, Transform parent, float height, RuntimeAnimatorController controller)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "Characters/" + model + ".fbx");
            var holder = new GameObject("Visual");
            holder.transform.SetParent(parent, false);

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, holder.transform);
            inst.name = model;
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // KayKit characters face -Z after import

            var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>();
            smr.sharedMaterial = TexturedMaterial(TextureFor[model]);
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            smr.updateWhenOffscreen = false;
            smr.quality = SkinQuality.Bone2;

            float raw = RawHeight(model);
            float s = height / Mathf.Max(0.01f, raw);
            inst.transform.localScale = Vector3.one * s;

            var anim = inst.GetComponent<Animator>();
            if (anim == null) anim = inst.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            return inst;
        }

        static readonly Dictionary<string, float> heightCache = new Dictionary<string, float>();

        static float RawHeight(string model)
        {
            if (heightCache.TryGetValue(model, out var h)) return h;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "Characters/" + model + ".fbx");
            var mesh = asset.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
            // Height axis is the longest vertical extent of the bind pose; meshes are stored Z-down before the importer's X rotation.
            var e = mesh.bounds.size;
            h = Mathf.Max(e.y, e.z);
            heightCache[model] = h;
            return h;
        }

        public static Transform FindBone(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var f = FindBone(c, name);
                if (f != null) return f;
            }
            return null;
        }

        // ------------------------------------------------------------------ guns + props

        public static Material GunMaterial() => TexturedMaterial("blaster_colormap");

        /// <summary>
        /// Puts a gun in the character's right hand. The aiming pose is sampled so the barrel can be pointed along the
        /// character's forward axis, then the gun is parented to the hand slot so it follows every animation.
        /// Returns the muzzle transform (barrel tip).
        /// </summary>
        public static Transform AttachGun(GameObject character, string gunId, float gunScale)
        {
            bool twoHanded = TwoHandedGuns.Contains(gunId);
            var aim = Clip("Anim_Heroes", twoHanded ? "2H_Ranged_Aiming" : "1H_Ranged_Aiming");
            if (aim != null) aim.SampleAnimation(character, 0f);

            var slot = FindBone(character.transform, "handslot.r");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "Guns/Gun_" + gunId + ".fbx");
            var gun = (GameObject)PrefabUtility.InstantiatePrefab(asset, slot);
            gun.name = "Gun_" + gunId;
            foreach (var r in gun.GetComponentsInChildren<MeshRenderer>())
            {
                r.sharedMaterial = GunMaterial();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // The mesh keeps Blender's axes (barrel +Y, top +Z); the imported root rotation turns that into
            // barrel +Z / top +Y, so aiming that frame along the character's forward points the barrel forward.
            Quaternion uprightFix = asset.transform.localRotation;
            // The model instance is turned 180 degrees inside its "Visual" holder, so the holder carries the facing.
            Transform facing = character.transform.parent != null ? character.transform.parent : character.transform;
            Vector3 fwd = facing.forward, up = facing.up;
            gun.transform.rotation = Quaternion.LookRotation(fwd, up) * uprightFix;
            gun.transform.localScale = Vector3.one * gunScale;   // relative to the hand bone, which carries the character scale

            // Measure the barrel and up axes in mesh space instead of assuming import conventions.
            var mesh = gun.GetComponentInChildren<MeshFilter>().sharedMesh;
            Bounds b = mesh.bounds;
            Vector3 barrel = SnapAxis(gun.transform.InverseTransformDirection(fwd));
            Vector3 top = SnapAxis(gun.transform.InverseTransformDirection(up));
            float length = Mathf.Abs(Vector3.Dot(b.size, barrel));
            float height = Mathf.Abs(Vector3.Dot(b.size, top));

            // Grip: a third of the way back from the centre, at the underside.
            Vector3 gripLocal = b.center - barrel * (0.3f * length) - top * (0.35f * height);
            gun.transform.position += slot.position - gun.transform.TransformPoint(gripLocal);

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(gun.transform, false);
            muzzle.localPosition = b.center + barrel * (0.5f * length);
            muzzle.rotation = Quaternion.LookRotation(fwd, up);
            return muzzle;
        }

        static Vector3 SnapAxis(Vector3 v)
        {
            Vector3 a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            if (a.x >= a.y && a.x >= a.z) return new Vector3(Mathf.Sign(v.x), 0f, 0f);
            if (a.y >= a.z) return new Vector3(0f, Mathf.Sign(v.y), 0f);
            return new Vector3(0f, 0f, Mathf.Sign(v.z));
        }

        public static GameObject AttachProp(GameObject character, string prop, string bone, float scale = 1f)
        {
            var slot = FindBone(character.transform, bone);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "Props/" + prop + ".fbx");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, slot);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one * scale;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                r.sharedMaterial = TexturedMaterial("skeleton_texture");
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return go;
        }

        /// <summary>All model renderers under a root (world-space labels excluded) for tinting and hit flashes.</summary>
        public static Renderer[] ModelRenderers(GameObject root) =>
            root.GetComponentsInChildren<Renderer>(true).Where(r => r is SkinnedMeshRenderer || r.GetComponent<MeshFilter>() != null && r.GetComponent<TMPro.TMP_Text>() == null).ToArray();

        public static AudioHub AudioHubFor(string musicName)
        {
            var go = new GameObject("Audio");
            var hub = go.AddComponent<AudioHub>();
            hub.clips = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio/Sfx" })
                .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null).OrderBy(c => c.name).ToArray();
            hub.music = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/" + musicName + ".wav");
            return hub;
        }

        // ------------------------------------------------------------------ preview

        [MenuItem("SquadRush/Art Preview Scene")]
        public static void BuildPreview()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects, UnityEditor.SceneManagement.NewSceneMode.Single);
            var hero1 = HeroController(false);
            var hero2 = HeroController(true);
            var skel = SkeletonController();

            string[] heroes = { "Hero_Knight", "Hero_Barbarian", "Hero_Rogue", "Hero_Mage", "Hero_RogueHooded" };
            string[] guns = { "pistol", "smg", "shotgun", "rifle", "minigun", "rocket" };
            for (int i = 0; i < 6; i++)
            {
                var root = new GameObject("H" + i);
                root.transform.position = new Vector3(-5f + i * 2f, 0f, 0f);
                string g = guns[i];
                var c = Character(heroes[i % heroes.Length], root.transform, 1.6f, TwoHandedGuns.Contains(g) ? hero2 : hero1);
                AttachGun(c, g, 1.15f);
            }
            string[] skels = { "Skeleton_Minion", "Skeleton_Rogue", "Skeleton_Warrior", "Skeleton_Mage" };
            string[] props = { "Skel_Blade", "Skel_Blade", "Skel_Axe", "Skel_Staff" };
            for (int i = 0; i < 4; i++)
            {
                var root = new GameObject("S" + i);
                root.transform.position = new Vector3(-3f + i * 2f, 0f, 4f);
                root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                var c = Character(skels[i], root.transform, 1.6f, skel);
                Clip("Anim_Skeletons", "Walking_D_Skeletons").SampleAnimation(c, 0.3f);
                AttachProp(c, props[i], "handslot.r");
            }
            var cam = Camera.main;
            cam.transform.position = new Vector3(0f, 3.2f, 9.5f);
            cam.transform.rotation = Quaternion.Euler(12f, 180f, 0f);
            cam.fieldOfView = 55f;
            AssetDatabase.SaveAssets();
        }
    }
}
