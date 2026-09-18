using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace SquadRush.EditorTools
{
    /// <summary>
    /// Builds the whole playable scene from primitives: materials, prefabs, lane, squad,
    /// director, UI and project settings. Re-runnable: overwrites Assets/Scenes/Game.unity.
    /// Menu: SquadRush > Build Game Scene, or headless via -executeMethod SquadRush.EditorTools.SceneBuilder.Build
    /// </summary>
    public static class SceneBuilder
    {
        const string MatDir = "Assets/Materials";
        const string PrefabDir = "Assets/Prefabs";
        const string ScenePath = "Assets/Scenes/Game.unity";

        const float LaneHalfWidth = 2.6f;
        const int GroundSegments = 10;
        const float SegmentLength = 8f;

        static readonly Vector2 Ref = new Vector2(1080f, 1920f);

        [MenuItem("SquadRush/Build Game Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory("Assets/Scenes");

            ConfigureProject();

            // ---- materials
            var matUnit = Lit("Unit", new Color(0.25f, 0.6f, 1f));
            var matUnitDark = Lit("UnitDark", new Color(0.12f, 0.2f, 0.4f));
            var matProjectile = Lit("Projectile", new Color(1f, 0.9f, 0.3f), emissive: new Color(1f, 0.8f, 0.2f) * 1.5f);
            var matObstacle = Lit("Obstacle", new Color(0.9f, 0.4f, 0.3f));
            var matGroundA = Lit("GroundA", new Color(0.32f, 0.36f, 0.45f));
            var matGroundB = Lit("GroundB", new Color(0.28f, 0.31f, 0.40f));
            var matRail = Lit("Rail", new Color(0.9f, 0.85f, 0.6f));
            var matDebris = Lit("Debris", Color.white);
            var matGate = TransparentUnlit("Gate", new Color(0.25f, 0.55f, 1f, 0.55f));

            // ---- prefabs
            var unitPrefab = BuildUnitPrefab(matUnit, matUnitDark);
            var projectilePrefab = BuildProjectilePrefab(matProjectile);
            var obstaclePrefab = BuildObstaclePrefab(matObstacle);
            var gatePrefab = BuildGatePrefab(matGate);

            // ---- scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var cam = Camera.main;
            cam.transform.position = new Vector3(0f, 10f, -8f);
            cam.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 120f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.10f, 0.16f);

            var light = Object.FindFirstObjectByType<Light>();
            if (light != null)
            {
                light.transform.rotation = Quaternion.Euler(55f, -25f, 0f);
                light.intensity = 1.3f;
                light.shadows = LightShadows.Soft;
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.55f);

            BuildGround(matGroundA, matGroundB, matRail);

            var squadGo = new GameObject("Squad");
            var squad = squadGo.AddComponent<Squad>();
            var squadRb = squadGo.GetComponent<Rigidbody>();
            squadRb.isKinematic = true;
            squadRb.useGravity = false;
            var squadBox = squadGo.GetComponent<BoxCollider>();
            squadBox.isTrigger = true;
            squadBox.center = new Vector3(0f, 0.6f, 0f);
            squadBox.size = new Vector3(2f, 1.2f, 1f);
            squad.unitPrefab = unitPrefab;
            squad.projectilePrefab = projectilePrefab;
            squad.laneHalfWidth = LaneHalfWidth;

            var directorGo = new GameObject("LevelDirector");
            var director = directorGo.AddComponent<LevelDirector>();
            director.obstaclePrefab = obstaclePrefab;
            director.gatePrefab = gatePrefab;
            director.debrisMaterial = matDebris;
            director.laneHalfWidth = LaneHalfWidth;

            var ui = BuildUI();

            var gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();
            gm.squad = squad;
            gm.director = director;
            gm.ui = ui;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SceneBuilder] Built " + ScenePath);
        }

        // ------------------------------------------------------------------ project settings

        static void ConfigureProject()
        {
            PlayerSettings.productName = "Squad Rush";
            PlayerSettings.companyName = "DeanStephens";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.deanstephens.squadrush");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.deanstephens.squadrush");
        }

        // ------------------------------------------------------------------ materials

        static Material Lit(string name, Color color, Color? emissive = null)
        {
            string path = MatDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.25f);
            if (emissive.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissive.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material TransparentUnlit(string name, Color color)
        {
            string path = MatDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Surface", 1f); // transparent
            mat.SetFloat("_Blend", 0f);   // alpha
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f);    // double-sided so the gate reads from any angle
            BaseShaderGUI.SetupMaterialBlendMode(mat);
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ prefabs

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static TextMeshPro WorldLabel(Transform parent, Vector3 localPos, float size)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = TMP_Settings.defaultFontAsset;
            tmp.fontSize = size;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.color = Color.white;
            tmp.rectTransform.sizeDelta = new Vector2(8f, 2f);
            tmp.text = "0";
            go.AddComponent<Billboard>();
            return tmp;
        }

        static T SavePrefab<T>(GameObject go, string name) where T : Component
        {
            string path = PrefabDir + "/" + name + ".prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved.GetComponent<T>();
        }

        static Unit BuildUnitPrefab(Material body, Material dark)
        {
            var root = new GameObject("Unit");
            var unit = root.AddComponent<Unit>();

            var visual = Primitive(PrimitiveType.Capsule, "Visual", root.transform, new Vector3(0f, 0.28f, 0f), new Vector3(0.34f, 0.28f, 0.34f), body, false);
            Primitive(PrimitiveType.Cube, "Gun", visual.transform, new Vector3(0.45f, 0.3f, 0.9f), new Vector3(0.3f, 0.35f, 0.9f), dark, false);
            Primitive(PrimitiveType.Sphere, "Visor", visual.transform, new Vector3(0f, 1.05f, 0.6f), new Vector3(0.6f, 0.35f, 0.6f), dark, false);

            var fire = new GameObject("FirePoint").transform;
            fire.SetParent(root.transform, false);
            fire.localPosition = new Vector3(0f, 0.45f, 0.35f);

            unit.visual = visual.transform;
            unit.firePoint = fire;
            return SavePrefab<Unit>(root, "Unit");
        }

        static Projectile BuildProjectilePrefab(Material mat)
        {
            var go = Primitive(PrimitiveType.Sphere, "Projectile", null, Vector3.zero, Vector3.one * 0.22f, mat, false);
            go.AddComponent<Projectile>();
            return SavePrefab<Projectile>(go, "Projectile");
        }

        static Obstacle BuildObstaclePrefab(Material mat)
        {
            var root = new GameObject("Obstacle");
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            root.AddComponent<ScrollingObject>().despawnZ = -8f;
            var ob = root.AddComponent<Obstacle>();

            var body = Primitive(PrimitiveType.Cube, "Body", root.transform, new Vector3(0f, 0.5f, 0f), Vector3.one, mat, true);
            ob.body = body.transform;
            ob.bodyRenderer = body.GetComponent<Renderer>();
            ob.label = WorldLabel(root.transform, new Vector3(0f, 1.7f, 0f), 5f);
            return SavePrefab<Obstacle>(root, "Obstacle");
        }

        static PowerUpGate BuildGatePrefab(Material mat)
        {
            var root = new GameObject("Gate");
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            root.AddComponent<ScrollingObject>().despawnZ = -8f;
            var gate = root.AddComponent<PowerUpGate>();

            var panel = Primitive(PrimitiveType.Cube, "Panel", root.transform, new Vector3(0f, 0.95f, 0f), new Vector3(LaneHalfWidth, 1.9f, 0.15f), mat, true);
            panel.GetComponent<Collider>().isTrigger = true;
            gate.panel = panel.GetComponent<Renderer>();
            gate.label = WorldLabel(root.transform, new Vector3(0f, 1.05f, -0.25f), 4.5f);
            gate.label.rectTransform.sizeDelta = new Vector2(LaneHalfWidth, 2f);
            return SavePrefab<PowerUpGate>(root, "Gate");
        }

        // ------------------------------------------------------------------ ground

        static void BuildGround(Material a, Material b, Material rail)
        {
            var root = new GameObject("Ground");
            var scroller = root.AddComponent<GroundScroller>();
            scroller.segmentLength = SegmentLength;
            scroller.segments = new Transform[GroundSegments];

            for (int i = 0; i < GroundSegments; i++)
            {
                var seg = new GameObject("Segment" + i);
                seg.transform.SetParent(root.transform, false);
                seg.transform.localPosition = new Vector3(0f, 0f, -12f + i * SegmentLength);

                Primitive(PrimitiveType.Cube, "Slab", seg.transform, new Vector3(0f, -0.15f, 0f), new Vector3(LaneHalfWidth * 2f + 1.6f, 0.3f, SegmentLength), i % 2 == 0 ? a : b, true);
                Primitive(PrimitiveType.Cube, "RailL", seg.transform, new Vector3(-(LaneHalfWidth + 0.6f), 0.15f, 0f), new Vector3(0.35f, 0.3f, 1.2f), rail, false);
                Primitive(PrimitiveType.Cube, "RailR", seg.transform, new Vector3(LaneHalfWidth + 0.6f, 0.15f, 0f), new Vector3(0.35f, 0.3f, 1.2f), rail, false);

                scroller.segments[i] = seg.transform;
            }
        }

        // ------------------------------------------------------------------ UI

        static readonly Color Gold = new Color(1f, 0.84f, 0.3f);
        static readonly Color Green = new Color(0.2f, 0.75f, 0.38f);
        static readonly Color Blue = new Color(0.22f, 0.36f, 0.62f);
        static readonly Color Purple = new Color(0.36f, 0.28f, 0.62f);
        static readonly Color Grey = new Color(0.35f, 0.37f, 0.42f);

        static GameUI BuildUI()
        {
            var canvasGo = new GameObject("UI");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Ref;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var ui = canvasGo.AddComponent<GameUI>();

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            var root = canvasGo.transform;

            // ---- HUD
            var hud = Panel(root, "HUD", Color.clear);
            ui.hudPanel = hud;
            ui.unitsText = Text(hud.transform, "Units", "UNITS 5", 60f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(280f, -95f), new Vector2(500f, 110f), Color.white, FontStyles.Bold);
            ui.distanceText = Text(hud.transform, "Distance", "0 m", 72f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -95f), new Vector2(500f, 110f), Color.white, FontStyles.Bold);
            ui.coinsText = Text(hud.transform, "Coins", "$0", 60f, TextAlignmentOptions.Right, new Vector2(1f, 1f), new Vector2(-280f, -95f), new Vector2(500f, 110f), Gold, FontStyles.Bold);
            ui.statsText = Text(hud.transform, "Stats", "", 38f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(900f, 70f), new Color(0.8f, 0.82f, 0.9f));

            // ---- Menu
            var menu = Panel(root, "Menu", new Color(0.05f, 0.07f, 0.12f, 0.88f));
            ui.menuPanel = menu;
            Text(menu.transform, "Title", "SQUAD RUSH", 128f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -340f), new Vector2(1040f, 200f), Gold, FontStyles.Bold);
            Text(menu.transform, "Sub", "Drag to steer.  Shoot everything.\nBlue gates help.  Red gates hurt.", 42f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -520f), new Vector2(1000f, 130f), new Color(0.8f, 0.82f, 0.9f));
            ui.bestText = Text(menu.transform, "Best", "BEST 0 m", 50f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -660f), new Vector2(900f, 80f), Color.white, FontStyles.Bold);
            ui.bankText = Text(menu.transform, "Bank", "$0", 50f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -740f), new Vector2(900f, 80f), Gold, FontStyles.Bold);

            Text(menu.transform, "UpgradesLabel", "UPGRADES", 44f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(900f, 70f), new Color(0.8f, 0.82f, 0.9f), FontStyles.Bold);
            ui.upgradeButtons = new Button[3];
            ui.upgradeLabels = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                ui.upgradeButtons[i] = Btn(menu.transform, "Upgrade" + i, "Upgrade", new Vector2(0.5f, 0.5f), new Vector2(0f, 130f - i * 170f), new Vector2(880f, 150f), Blue, 44f, out var label);
                ui.upgradeLabels[i] = label;
            }
            ui.playButton = Btn(menu.transform, "Play", "PLAY", new Vector2(0.5f, 0f), new Vector2(0f, 270f), new Vector2(760f, 190f), Green, 92f, out _);

            // ---- Game over
            var over = Panel(root, "GameOver", new Color(0.16f, 0.03f, 0.06f, 0.9f));
            ui.gameOverPanel = over;
            Text(over.transform, "Title", "RUN OVER", 120f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -400f), new Vector2(1000f, 180f), Color.white, FontStyles.Bold);
            ui.resultText = Text(over.transform, "Result", "", 84f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(900f, 420f), Gold, FontStyles.Bold);
            ui.retryButton = Btn(over.transform, "Retry", "RETRY", new Vector2(0.5f, 0.5f), new Vector2(0f, -220f), new Vector2(760f, 180f), Green, 84f, out _);
            ui.menuButton = Btn(over.transform, "MenuBtn", "MENU", new Vector2(0.5f, 0.5f), new Vector2(0f, -440f), new Vector2(760f, 140f), Grey, 60f, out _);

            // ---- Perks
            var perks = Panel(root, "Perks", new Color(0.05f, 0.05f, 0.11f, 0.92f));
            ui.perkPanel = perks;
            Text(perks.transform, "Title", "BOSS DOWN!\n<size=60%>CHOOSE A PERK</size>", 96f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -360f), new Vector2(1000f, 280f), Gold, FontStyles.Bold);
            ui.perkButtons = new Button[3];
            ui.perkTitles = new TMP_Text[3];
            ui.perkDescs = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                var b = Btn(perks.transform, "Perk" + i, "", new Vector2(0.5f, 0.5f), new Vector2(0f, 260f - i * 330f), new Vector2(900f, 280f), Purple, 40f, out var label);
                label.gameObject.SetActive(false);
                ui.perkButtons[i] = b;
                ui.perkTitles[i] = Text(b.transform, "PerkTitle", "Perk", 62f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(860f, 110f), Color.white, FontStyles.Bold);
                ui.perkDescs[i] = Text(b.transform, "PerkDesc", "Description", 42f, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(860f, 110f), new Color(0.85f, 0.85f, 0.95f));
            }

            hud.SetActive(false);
            over.SetActive(false);
            perks.SetActive(false);
            return ui;
        }

        static RectTransform Place(GameObject go, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static GameObject Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            if (color.a > 0f)
            {
                var img = go.AddComponent<Image>();
                img.color = color;
            }
            return go;
        }

        static TextMeshProUGUI Text(Transform parent, string name, string text, float size, TextAlignmentOptions align, Vector2 anchor, Vector2 pos, Vector2 sizeDelta, Color color, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Place(go, anchor, pos, sizeDelta);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.raycastTarget = false;
            return tmp;
        }

        static Button Btn(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color color, float fontSize, out TextMeshProUGUI text)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Place(go, anchor, pos, size);

            var img = go.AddComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.5f;
            img.color = color;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
            btn.colors = colors;

            text = Text(go.transform, "Label", label, fontSize, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(40f, 20f), Color.white, FontStyles.Bold);
            return btn;
        }
    }
}
