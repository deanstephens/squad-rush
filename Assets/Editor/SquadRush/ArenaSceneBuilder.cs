using System.Collections.Generic;
using SquadRush.Arena;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace SquadRush.EditorTools
{
    /// <summary>Builds Assets/Scenes/Arena.unity (the survivors mode) from primitives. Menu: SquadRush > Build Arena Scene.</summary>
    public static class ArenaSceneBuilder
    {
        const float HalfSize = 30f;

        [MenuItem("SquadRush/Build Arena Scene")]
        public static void BuildArena()
        {
            System.IO.Directory.CreateDirectory(SceneBuilder.MatDir);
            System.IO.Directory.CreateDirectory(SceneBuilder.PrefabDir);

            var matPlayer = SceneBuilder.Lit("ArenaPlayer", new Color(0.3f, 0.65f, 1f));
            var matGunBody = SceneBuilder.Lit("ArenaGun", new Color(0.12f, 0.14f, 0.2f));
            var matEnemy = SceneBuilder.Lit("ArenaEnemy", new Color(0.95f, 0.4f, 0.35f));
            var matBullet = SceneBuilder.Lit("ArenaBullet", Color.white, emissive: Color.white * 0.8f);
            var matGem = SceneBuilder.Lit("ArenaGem", new Color(0.35f, 1f, 0.7f), emissive: new Color(0.2f, 1f, 0.6f) * 1.2f);
            var matFloorA = SceneBuilder.Lit("ArenaFloorA", new Color(0.24f, 0.27f, 0.34f));
            var matFloorB = SceneBuilder.Lit("ArenaFloorB", new Color(0.21f, 0.24f, 0.31f));
            var matWall = SceneBuilder.Lit("ArenaWall", new Color(0.55f, 0.5f, 0.35f));
            var matDebris = AssetDatabase.LoadAssetAtPath<Material>(SceneBuilder.MatDir + "/Debris.mat") ?? SceneBuilder.Lit("Debris", Color.white);

            var gunVisual = BuildGunVisualPrefab(matGunBody, matBullet);
            var enemyPrefab = BuildEnemyPrefab(matEnemy);
            var bulletPrefab = BuildBulletPrefab(matBullet);
            var gemPrefab = BuildGemPrefab(matGem);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var cam = Camera.main;
            cam.transform.position = new Vector3(0f, 17f, -9f);
            cam.transform.rotation = Quaternion.Euler(62f, 0f, 0f);
            cam.fieldOfView = 55f;
            cam.farClipPlane = 150f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.08f, 0.12f);
            var follow = cam.gameObject.AddComponent<ArenaCameraFollow>();

            var light = Object.FindFirstObjectByType<Light>();
            if (light != null)
            {
                light.transform.rotation = Quaternion.Euler(60f, -30f, 0f);
                light.intensity = 1.25f;
                light.shadows = LightShadows.Soft;
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.52f);

            BuildFloor(matFloorA, matFloorB, matWall);

            // ---- player
            var playerGo = new GameObject("Player");
            var player = playerGo.AddComponent<ArenaPlayer>();
            var prb = playerGo.GetComponent<Rigidbody>();
            prb.isKinematic = true;
            prb.useGravity = false;
            var pcap = playerGo.GetComponent<CapsuleCollider>();
            pcap.center = new Vector3(0f, 0.6f, 0f);
            pcap.radius = 0.45f;
            pcap.height = 1.2f;
            var visual = SceneBuilder.Primitive(PrimitiveType.Capsule, "Visual", playerGo.transform, new Vector3(0f, 0.6f, 0f), new Vector3(0.9f, 0.6f, 0.9f), matPlayer, false);
            SceneBuilder.Primitive(PrimitiveType.Sphere, "Visor", visual.transform, new Vector3(0f, 0.55f, 0.35f), new Vector3(0.5f, 0.3f, 0.5f), matGunBody, false);
            player.visual = visual.transform;
            player.bodyRenderer = visual.GetComponent<Renderer>();
            player.gunVisualPrefab = gunVisual;
            player.arenaHalfSize = HalfSize - 1f;
            follow.target = playerGo.transform;

            // ---- systems
            var spawnerGo = new GameObject("EnemySpawner");
            var spawner = spawnerGo.AddComponent<EnemySpawner>();
            spawner.enemyPrefab = enemyPrefab;
            spawner.arenaHalfSize = HalfSize - 1f;

            var ui = BuildUI();

            var amGo = new GameObject("ArenaManager");
            var am = amGo.AddComponent<ArenaManager>();
            am.player = player;
            am.spawner = spawner;
            am.ui = ui;
            am.bulletPrefab = bulletPrefab;
            am.gemPrefab = gemPrefab;
            am.debrisMaterial = matDebris;

            EditorSceneManager.SaveScene(scene, SceneBuilder.ArenaScenePath);
            SceneBuilder.RegisterScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ArenaSceneBuilder] Built " + SceneBuilder.ArenaScenePath);
        }

        // ------------------------------------------------------------------ prefabs

        static GameObject BuildGunVisualPrefab(Material body, Material tip)
        {
            var root = new GameObject("GunVisual");
            root.AddComponent<Gun>();
            SceneBuilder.Primitive(PrimitiveType.Cube, "Body", root.transform, new Vector3(0f, 0f, 0.2f), new Vector3(0.16f, 0.16f, 0.55f), body, false);
            var t = SceneBuilder.Primitive(PrimitiveType.Cube, "Tip", root.transform, new Vector3(0f, 0f, 0.5f), new Vector3(0.12f, 0.12f, 0.12f), tip, false);
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root.transform, false);
            muzzle.localPosition = new Vector3(0f, 0f, 0.6f);
            string path = SceneBuilder.PrefabDir + "/GunVisual.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        static Enemy BuildEnemyPrefab(Material mat)
        {
            var root = new GameObject("Enemy");
            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.linearDamping = 0f;
            var cap = root.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, 0.55f, 0f);
            cap.radius = 0.42f;
            cap.height = 1.1f;
            var enemy = root.AddComponent<Enemy>();

            var body = SceneBuilder.Primitive(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.85f, 0.55f, 0.85f), mat, false);
            SceneBuilder.Primitive(PrimitiveType.Cube, "Eye", body.transform, new Vector3(0f, 0.5f, 0.4f), new Vector3(0.5f, 0.2f, 0.3f), SceneBuilder.Lit("ArenaGun", new Color(0.12f, 0.14f, 0.2f)), false);
            enemy.bodyRenderer = body.GetComponent<Renderer>();
            return SceneBuilder.SavePrefab<Enemy>(root, "Enemy");
        }

        static ArenaBullet BuildBulletPrefab(Material mat)
        {
            var go = SceneBuilder.Primitive(PrimitiveType.Sphere, "ArenaBullet", null, Vector3.zero, Vector3.one * 0.25f, mat, false);
            go.AddComponent<ArenaBullet>();
            return SceneBuilder.SavePrefab<ArenaBullet>(go, "ArenaBullet");
        }

        static XpGem BuildGemPrefab(Material mat)
        {
            var go = SceneBuilder.Primitive(PrimitiveType.Cube, "XpGem", null, Vector3.zero, Vector3.one * 0.3f, mat, false);
            go.transform.rotation = Quaternion.Euler(45f, 0f, 45f);
            go.AddComponent<XpGem>();
            return SceneBuilder.SavePrefab<XpGem>(go, "XpGem");
        }

        // ------------------------------------------------------------------ floor

        static void BuildFloor(Material a, Material b, Material wall)
        {
            var root = new GameObject("Floor");
            const float tile = 10f;
            int n = Mathf.CeilToInt(HalfSize * 2f / tile);
            for (int x = 0; x < n; x++)
            for (int z = 0; z < n; z++)
            {
                var pos = new Vector3(-HalfSize + tile * 0.5f + x * tile, -0.15f, -HalfSize + tile * 0.5f + z * tile);
                SceneBuilder.Primitive(PrimitiveType.Cube, "Tile", root.transform, pos, new Vector3(tile, 0.3f, tile), (x + z) % 2 == 0 ? a : b, true);
            }
            float w = HalfSize * 2f + 1f;
            SceneBuilder.Primitive(PrimitiveType.Cube, "WallN", root.transform, new Vector3(0f, 0.5f, HalfSize + 0.5f), new Vector3(w, 1f, 1f), wall, true);
            SceneBuilder.Primitive(PrimitiveType.Cube, "WallS", root.transform, new Vector3(0f, 0.5f, -HalfSize - 0.5f), new Vector3(w, 1f, 1f), wall, true);
            SceneBuilder.Primitive(PrimitiveType.Cube, "WallE", root.transform, new Vector3(HalfSize + 0.5f, 0.5f, 0f), new Vector3(1f, 1f, w), wall, true);
            SceneBuilder.Primitive(PrimitiveType.Cube, "WallW", root.transform, new Vector3(-HalfSize - 0.5f, 0.5f, 0f), new Vector3(1f, 1f, w), wall, true);
        }

        // ------------------------------------------------------------------ UI

        static ArenaUI BuildUI()
        {
            var canvasGo = new GameObject("UI");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var ui = canvasGo.AddComponent<ArenaUI>();

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            var root = canvasGo.transform;
            var dim = new Color(0.8f, 0.82f, 0.9f);

            // ---- Armory
            var loadout = SceneBuilder.Panel(root, "Loadout", new Color(0.05f, 0.06f, 0.1f, 0.96f));
            ui.loadoutPanel = loadout;
            SceneBuilder.Text(loadout.transform, "Title", "ARMORY", 80f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -85f), new Vector2(1000f, 110f), SceneBuilder.Gold, FontStyles.Bold);
            ui.scrapText = SceneBuilder.Text(loadout.transform, "Scrap", "SCRAP 0", 42f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(300f, -165f), new Vector2(540f, 60f), new Color(0.6f, 0.9f, 1f), FontStyles.Bold);
            ui.bestText = SceneBuilder.Text(loadout.transform, "Best", "BEST 0:00", 34f, TextAlignmentOptions.Right, new Vector2(1f, 1f), new Vector2(-300f, -165f), new Vector2(540f, 60f), dim);

            var guns = GunLibrary.All;
            ui.gunRows = new ArenaUI.GunRow[guns.Count];
            for (int i = 0; i < guns.Count; i++)
            {
                var rowGo = new GameObject("Row_" + guns[i].Id);
                rowGo.transform.SetParent(loadout.transform, false);
                SceneBuilder.Place(rowGo, new Vector2(0.5f, 1f), new Vector2(0f, -255f - i * 100f), new Vector2(1000f, 90f));
                var bg = rowGo.AddComponent<Image>();
                bg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                bg.type = Image.Type.Sliced;
                bg.pixelsPerUnitMultiplier = 0.5f;
                var btn = rowGo.AddComponent<Button>();
                btn.targetGraphic = bg;

                var row = new ArenaUI.GunRow { gunId = guns[i].Id, background = bg, selectButton = btn };
                row.nameText = SceneBuilder.Text(rowGo.transform, "Name", guns[i].Name, 40f, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(330f, 0f), new Vector2(600f, 80f), Color.white, FontStyles.Bold);
                row.stateText = SceneBuilder.Text(rowGo.transform, "State", "", 30f, TextAlignmentOptions.Right, new Vector2(1f, 0.5f), new Vector2(-250f, 0f), new Vector2(460f, 80f), Color.white, FontStyles.Bold);
                ui.gunRows[i] = row;
            }

            // detail panel for the viewed gun
            float dy = -255f - guns.Count * 100f - 10f;   // top of the detail block
            var detail = new GameObject("Detail");
            detail.transform.SetParent(loadout.transform, false);
            SceneBuilder.Place(detail, new Vector2(0.5f, 1f), new Vector2(0f, dy - 385f), new Vector2(1000f, 770f));
            var dbg = detail.AddComponent<Image>();
            dbg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            dbg.type = Image.Type.Sliced;
            dbg.pixelsPerUnitMultiplier = 0.5f;
            dbg.color = new Color(0.1f, 0.11f, 0.17f, 0.95f);
            dbg.raycastTarget = false;

            ui.detailName = SceneBuilder.Text(detail.transform, "Name", "Pistol", 46f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(490f, -40f), new Vector2(940f, 60f), Color.white, FontStyles.Bold);
            ui.detailDesc = SceneBuilder.Text(detail.transform, "Desc", "", 28f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(490f, -85f), new Vector2(940f, 40f), dim);
            ui.detailStats = SceneBuilder.Text(detail.transform, "Stats", "", 26f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(490f, -125f), new Vector2(940f, 40f), new Color(0.6f, 0.9f, 1f));
            ui.upgradeButton = SceneBuilder.Btn(detail.transform, "Upgrade", "UPGRADE", new Vector2(0.5f, 1f), new Vector2(0f, -205f), new Vector2(940f, 90f), SceneBuilder.Blue, 34f, out var upLabel);
            ui.upgradeLabel = upLabel;
            SceneBuilder.Text(detail.transform, "ModsLabel", "MODS", 30f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(490f, -280f), new Vector2(940f, 40f), dim, FontStyles.Bold);

            ui.modRows = new ArenaUI.ModRow[4];
            for (int j = 0; j < 4; j++)
            {
                var mrow = new GameObject("Mod" + j);
                mrow.transform.SetParent(detail.transform, false);
                SceneBuilder.Place(mrow, new Vector2(0.5f, 1f), new Vector2(0f, -350f - j * 105f), new Vector2(940f, 95f));
                var mbg = mrow.AddComponent<Image>();
                mbg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                mbg.type = Image.Type.Sliced;
                mbg.pixelsPerUnitMultiplier = 0.5f;
                mbg.raycastTarget = false;
                var m = new ArenaUI.ModRow { background = mbg };
                m.nameText = SceneBuilder.Text(mrow.transform, "Name", "Mod", 32f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(345f, -28f), new Vector2(650f, 44f), Color.white, FontStyles.Bold);
                m.descText = SceneBuilder.Text(mrow.transform, "Desc", "", 24f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(345f, -66f), new Vector2(650f, 40f), dim);
                m.buyButton = SceneBuilder.Btn(mrow.transform, "Buy", "BUY", new Vector2(1f, 0.5f), new Vector2(-105f, 0f), new Vector2(190f, 78f), SceneBuilder.Green, 26f, out var buyLabel);
                m.buyLabel = buyLabel;
                ui.modRows[j] = m;
            }

            ui.startButton = SceneBuilder.Btn(loadout.transform, "Start", "ENTER ARENA", new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(760f, 130f), SceneBuilder.Green, 52f, out var startLabel);
            ui.startLabel = startLabel;
            ui.hubButton = SceneBuilder.Btn(loadout.transform, "Hub", "BACK TO TREADMILL", new Vector2(0.5f, 0f), new Vector2(0f, 45f), new Vector2(760f, 60f), SceneBuilder.Grey, 28f, out _);

            // ---- HUD
            var hud = SceneBuilder.Panel(root, "HUD", Color.clear);
            ui.hudPanel = hud;
            ui.timerText = SceneBuilder.Text(hud.transform, "Timer", "0:00", 76f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -95f), new Vector2(500f, 110f), Color.white, FontStyles.Bold);
            ui.levelText = SceneBuilder.Text(hud.transform, "Level", "LV 1", 52f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(230f, -95f), new Vector2(400f, 110f), Color.white, FontStyles.Bold);
            ui.coinsText = SceneBuilder.Text(hud.transform, "Coins", "$0", 52f, TextAlignmentOptions.Right, new Vector2(1f, 1f), new Vector2(-230f, -95f), new Vector2(400f, 110f), SceneBuilder.Gold, FontStyles.Bold);
            ui.killsText = SceneBuilder.Text(hud.transform, "Kills", "0 KILLS", 36f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -165f), new Vector2(500f, 60f), dim);

            ui.xpFill = SceneBuilder.Bar(hud.transform, "XpBar", new Vector2(0.5f, 1f), new Vector2(0f, -215f), new Vector2(980f, 22f), new Color(0.35f, 1f, 0.7f));
            ui.hpFill = SceneBuilder.Bar(hud.transform, "HpBar", new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(760f, 40f), new Color(0.95f, 0.3f, 0.3f));
            ui.hpText = SceneBuilder.Text(hud.transform, "Hp", "100 / 100", 34f, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(760f, 40f), Color.white, FontStyles.Bold);

            // ---- Level up
            var lvl = SceneBuilder.Panel(root, "LevelUp", new Color(0.04f, 0.05f, 0.1f, 0.9f));
            ui.levelUpPanel = lvl;
            SceneBuilder.Text(lvl.transform, "Title", "LEVEL UP!\n<size=60%>CHOOSE AN UPGRADE</size>", 96f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -360f), new Vector2(1000f, 280f), SceneBuilder.Gold, FontStyles.Bold);
            ui.upgradeButtons = new Button[3];
            ui.upgradeTitles = new TMP_Text[3];
            ui.upgradeDescs = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                var b = SceneBuilder.Btn(lvl.transform, "Upgrade" + i, "", new Vector2(0.5f, 0.5f), new Vector2(0f, 260f - i * 330f), new Vector2(900f, 280f), SceneBuilder.Purple, 40f, out var label);
                label.gameObject.SetActive(false);
                ui.upgradeButtons[i] = b;
                ui.upgradeTitles[i] = SceneBuilder.Text(b.transform, "Title", "Upgrade", 62f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(860f, 110f), Color.white, FontStyles.Bold);
                ui.upgradeDescs[i] = SceneBuilder.Text(b.transform, "Desc", "Description", 42f, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(860f, 110f), new Color(0.85f, 0.85f, 0.95f));
            }

            // ---- Game over
            var over = SceneBuilder.Panel(root, "GameOver", new Color(0.16f, 0.03f, 0.06f, 0.92f));
            ui.gameOverPanel = over;
            SceneBuilder.Text(over.transform, "Title", "OVERRUN", 120f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -400f), new Vector2(1000f, 180f), Color.white, FontStyles.Bold);
            ui.resultText = SceneBuilder.Text(over.transform, "Result", "", 72f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(900f, 480f), SceneBuilder.Gold, FontStyles.Bold);
            ui.retryButton = SceneBuilder.Btn(over.transform, "Retry", "RETRY", new Vector2(0.5f, 0.5f), new Vector2(0f, -240f), new Vector2(760f, 180f), SceneBuilder.Green, 84f, out _);
            ui.armoryButton = SceneBuilder.Btn(over.transform, "Armory", "ARMORY", new Vector2(0.5f, 0.5f), new Vector2(0f, -460f), new Vector2(760f, 140f), SceneBuilder.Grey, 60f, out _);

            hud.SetActive(false);
            lvl.SetActive(false);
            over.SetActive(false);
            return ui;
        }
    }
}
