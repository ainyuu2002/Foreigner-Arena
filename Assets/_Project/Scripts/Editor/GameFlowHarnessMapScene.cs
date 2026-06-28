using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FA.Core;
using FA.GameFlow;
using FA.Stage;

namespace FA.EditorTools
{
    /// <summary>
    /// PHẦN MAP-SCENE của tool "Build Test_Phase12 (GameFlow harness)" (xem <see cref="GameFlowHarnessSetup"/>).
    /// 1 chức năng: TẠO scene map nhỏ <c>Test_Phase12_Map.unity</c> mà MatchManager load ADDITIVE khi vào Match
    /// (MatchManager P12 thiết kế map = scene riêng, KHÔNG nhúng vào scene menu — research §3.1). Scene này
    /// CHỈ chứa: 1 sàn (BoxCollider2D Layer Ground), 4 DeathZone (BoxCollider2D isTrigger Layer DeathZone +
    /// <see cref="DeathZone"/> wire OnPlayerDiedChannel, debugSelfRespawn=FALSE để LivesSystem xử), và
    /// <see cref="StageSpawnPoints"/> (SpawnP1/P2 + RespawnPoint giữa).
    ///
    /// Scene phải có trong Build Settings để LoadSceneAsync(name) chạy → tool tự thêm (GameFlowHarnessSetup).
    /// EDITOR-ONLY. KHÔNG sửa code gameplay; chỉ dựng GameObject + wire qua SerializedObject.
    /// </summary>
    internal static class GameFlowHarnessMapScene
    {
        public const string ScenesFolder = "Assets/_Project/Scenes";
        public const string MapSceneName = "Test_Phase12_Map";
        public static string MapScenePath => $"{ScenesFolder}/{MapSceneName}.unity";

        /// <summary>Tạo (ghi đè) scene map harness. Trả path để thêm Build Settings. b dùng kênh OnPlayerDied.</summary>
        public static string BuildMapScene(GameFlowHarnessAssets.Bundle b)
        {
            // Scene mới, single (tách khỏi scene menu đang mở — sẽ mở lại scene menu sau).
            Scene map = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildFloor();
            BuildDeathZones(b);
            BuildSpawnPoints();
            BuildSimpleCamera();

            EditorSceneManager.MarkSceneDirty(map);
            if (!System.IO.Directory.Exists(ScenesFolder))
                System.IO.Directory.CreateDirectory(ScenesFolder);
            EditorSceneManager.SaveScene(map, MapScenePath);
            return MapScenePath;
        }

        // ───────────────────── sàn ─────────────────────

        private static void BuildFloor()
        {
            var floor = new GameObject("Floor", typeof(BoxCollider2D), typeof(SpriteRenderer));
            floor.layer = GameLayers.Ground;
            floor.transform.position = new Vector3(0f, -2f, 0f);
            floor.transform.localScale = new Vector3(20f, 1f, 1f);

            var sr = floor.GetComponent<SpriteRenderer>();
            sr.sprite = BuiltinQuad();
            sr.color = new Color(0.3f, 0.32f, 0.36f, 1f);
            // BoxCollider2D mặc định size 1x1 → scale 20x1 cho sàn rộng (Rigidbody2D player đứng được).
        }

        // ───────────────────── 4 death zone ─────────────────────

        private static void BuildDeathZones(GameFlowHarnessAssets.Bundle b)
        {
            var parent = new GameObject("DeathZones");
            // Bao quanh XA rìa (Smash blast-zone). playerLayers = Player1|Player2.
            int mask = GameLayers.Player1Mask | GameLayers.Player2Mask;
            MakeZone(parent.transform, "DZ_Bottom", new Vector3(0f, -12f, 0f), new Vector3(60f, 4f, 1f), b, mask);
            MakeZone(parent.transform, "DZ_Top", new Vector3(0f, 14f, 0f), new Vector3(60f, 4f, 1f), b, mask);
            MakeZone(parent.transform, "DZ_Left", new Vector3(-24f, 0f, 0f), new Vector3(4f, 40f, 1f), b, mask);
            MakeZone(parent.transform, "DZ_Right", new Vector3(24f, 0f, 0f), new Vector3(4f, 40f, 1f), b, mask);
        }

        private static void MakeZone(Transform parent, string name, Vector3 pos, Vector3 scale,
                                     GameFlowHarnessAssets.Bundle b, int playerMask)
        {
            var go = new GameObject(name, typeof(BoxCollider2D), typeof(DeathZone));
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.layer = GameLayers.DeathZone;

            var col = go.GetComponent<BoxCollider2D>();
            col.isTrigger = true;

            var dz = go.GetComponent<DeathZone>();
            var so = new SerializedObject(dz);
            SetObj(so, "onPlayerDied", b.PlayerDied);
            SetLayerMask(so, "playerLayers", playerMask);
            SetBool(so, "debugSelfRespawn", false);  // LivesSystem/RespawnSystem xử — KHÔNG self-respawn.
            SetBool(so, "logOnEnter", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(dz); // flush ref/flags vào scene map khi SaveScene.
        }

        // ───────────────────── spawn points ─────────────────────

        private static void BuildSpawnPoints()
        {
            var root = new GameObject("StageSpawnPoints", typeof(StageSpawnPoints));
            var p1 = new GameObject("SpawnP1").transform;
            var p2 = new GameObject("SpawnP2").transform;
            var respawn = new GameObject("RespawnPoint").transform;
            p1.SetParent(root.transform, false); p1.position = new Vector3(-4f, 0f, 0f);
            p2.SetParent(root.transform, false); p2.position = new Vector3(4f, 0f, 0f);
            respawn.SetParent(root.transform, false); respawn.position = new Vector3(0f, 2f, 0f);

            var ssp = root.GetComponent<StageSpawnPoints>();
            var so = new SerializedObject(ssp);
            SetObj(so, "spawnP1", p1);
            SetObj(so, "spawnP2", p2);
            SetObj(so, "respawnPoint", respawn);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ssp); // flush spawn-point refs vào scene map khi SaveScene.
        }

        private static void BuildSimpleCamera()
        {
            // Scene menu giữ Camera chính (qua DontDestroyOnLoad? Không — menu camera đủ render UI Overlay).
            // Map additive KHÔNG cần camera riêng: UI Canvas Overlay render độc lập, player thấy qua camera menu.
            // Thêm 1 camera 2D nhẹ để nhìn arena khi load map (nếu scene menu không có camera world).
            if (Camera.main != null) return;
            var camGo = new GameObject("MapCamera", typeof(Camera));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.transform.position = new Vector3(0f, 1f, -10f);
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f, 1f);
        }

        // ───────────────────── helpers ─────────────────────

        private static Sprite _quad;
        private static Sprite BuiltinQuad()
        {
            if (_quad == null)
            {
                // sprite trắng 1x1 built-in để SpriteRenderer hiện khối (Unity-native).
                var tex = Texture2D.whiteTexture;
                _quad = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                      new Vector2(0.5f, 0.5f), 1f);
                _quad.name = "HarnessQuad";
            }
            return _quad;
        }

        private static void SetObj(SerializedObject so, string prop, Object val)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = val;
        }

        private static void SetBool(SerializedObject so, string prop, bool val)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.boolValue = val;
        }

        private static void SetLayerMask(SerializedObject so, string prop, int mask)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.intValue = mask;
        }
    }
}
