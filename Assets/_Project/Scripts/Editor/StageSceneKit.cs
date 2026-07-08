using System.Collections.Generic;
using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using FA.Core;
using FA.GameFlow;
using FA.Stage;

namespace FA.EditorTools
{
    /// <summary>
    /// Bộ helper dùng chung cho builder 3 map Phase 11 (Editor-only): tilemap+composite, one-way platform,
    /// death zone, spawn points, camera Cinemachine 3 + Confiner, Light2D, particle, parallax backdrop,
    /// Build Settings + StageRegistry. Mirror pattern GameFlowHarness* (SerializedObject + SetDirty).
    /// </summary>
    internal static class StageSceneKit
    {
        public const string ScenesFolder = "Assets/_Project/Scenes";
        public const string DataFolder = "Assets/_Project/Data";
        public const string PlayerDiedChannelPath = DataFolder + "/OnPlayerDiedChannel.asset";
        public const string StageRegistryPath = DataFolder + "/GameFlow/Harness_StageRegistry.asset";
        public const string IceMaterialPath = DataFolder + "/Stage/IceSurface.physicsMaterial2D";

        // ---------- SerializedObject helpers (pattern GameFlowHarnessMapScene) ----------

        public static void SetObj(Component c, string field, Object val)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[StageSceneKit] Không thấy field '{field}' trên {c.GetType().Name}"); return; }
            p.objectReferenceValue = val;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(c);
        }

        public static void SetBool(Component c, string field, bool val)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p == null) return;
            p.boolValue = val;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(c);
        }

        public static void SetFloat(Component c, string field, float val)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p == null) return;
            p.floatValue = val;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(c);
        }

        public static void SetMask(Component c, string field, int mask)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p == null) return;
            p.intValue = mask;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(c);
        }

        // ---------- Scene ----------

        public static Scene NewScene()
        {
            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        public static void SaveScene(Scene s, string sceneName)
        {
            if (!Directory.Exists(ScenesFolder)) Directory.CreateDirectory(ScenesFolder);
            string path = $"{ScenesFolder}/{sceneName}.unity";
            EditorSceneManager.MarkSceneDirty(s);
            EditorSceneManager.SaveScene(s, path);
            AddSceneToBuild(path);
        }

        public static void AddSceneToBuild(string path)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!list.Exists(x => x.path == path))
            {
                list.Add(new EditorBuildSettingsScene(path, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
        }

        // ---------- Sprite / backdrop ----------

        public static Sprite LoadSprite(string path)
        {
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp == null) Debug.LogError($"[StageSceneKit] Thiếu sprite '{path}' — chạy menu Generate Art trước.");
            return sp;
        }

        public static SpriteRenderer MakeSpriteGO(string name, Sprite sprite, Vector3 pos, int order,
                                                  Transform parent, Vector3? scale = null)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            if (scale.HasValue) go.transform.localScale = scale.Value;
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>Backdrop parallax: sprite + ParallaxLayer (followFactor set qua SerializedObject).</summary>
        public static void MakeBackdrop(string name, string spritePath, Vector3 pos, int order,
                                        float factor, Transform parent, float scale = 1f)
        {
            var sr = MakeSpriteGO(name, LoadSprite(spritePath), pos, order, parent, new Vector3(scale, scale, 1f));
            var px = sr.gameObject.AddComponent<ParallaxLayer>();
            var so = new SerializedObject(px);
            var p = so.FindProperty("followFactor");
            if (p != null) { p.vector2Value = new Vector2(factor, factor); so.ApplyModifiedPropertiesWithoutUndo(); }
            EditorUtility.SetDirty(px);
        }

        // ---------- Tilemap ground ----------

        public static Tile MakeTileAsset(string spritePath, string tilePath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (existing != null) { existing.sprite = LoadSprite(spritePath); EditorUtility.SetDirty(existing); return existing; }
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = LoadSprite(spritePath);
            tile.colliderType = Tile.ColliderType.Grid;
            AssetDatabase.CreateAsset(tile, tilePath);
            return tile;
        }

        /// <summary>Grid + Tilemap Ground (TilemapCollider2D + CompositeCollider2D Polygons + RB Static).</summary>
        public static Tilemap MakeGroundTilemap(Transform parent, PhysicsMaterial2D surfaceMaterial, int order)
        {
            var gridGo = new GameObject("Grid", typeof(Grid));
            gridGo.transform.SetParent(parent, false);
            var tmGo = new GameObject("Tilemap (Ground)", typeof(Tilemap), typeof(TilemapRenderer));
            tmGo.transform.SetParent(gridGo.transform, false);
            tmGo.layer = GameLayers.Ground;

            var tmCol = tmGo.AddComponent<TilemapCollider2D>();
            var rb = tmGo.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var comp = tmGo.AddComponent<CompositeCollider2D>();
            comp.geometryType = CompositeCollider2D.GeometryType.Polygons;
#pragma warning disable 0618
            tmCol.usedByComposite = true;
#pragma warning restore 0618
            if (surfaceMaterial != null) comp.sharedMaterial = surfaceMaterial;

            tmGo.GetComponent<TilemapRenderer>().sortingOrder = order;
            return tmGo.GetComponent<Tilemap>();
        }

        /// <summary>Đổ khối đất: hàng trên cùng = tileTop, các hàng dưới = tileInner.</summary>
        public static void FillGround(Tilemap tm, Tile top, Tile inner, int xMin, int xMax, int topY, int depth)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                tm.SetTile(new Vector3Int(x, topY, 0), top);
                for (int d = 1; d < depth; d++)
                    tm.SetTile(new Vector3Int(x, topY - d, 0), inner);
            }
        }

        // ---------- One-way platform ----------

        public static GameObject MakeOneWayPlatform(string name, string spritePath, Vector2 pos,
                                                    Transform parent, int order, float scaleX = 1f)
        {
            var sr = MakeSpriteGO(name, LoadSprite(spritePath), pos, order, parent, new Vector3(scaleX, 1f, 1f));
            var go = sr.gameObject;
            go.layer = GameLayers.OneWayPlatform;
            var col = go.AddComponent<BoxCollider2D>();
            // Collider dày hẳn (0.55) chống tunneling khi rơi nhanh; mặt trên khớp lớp tuyết/cỏ của sprite.
            col.size = new Vector2(4f, 0.55f);
            col.offset = new Vector2(0f, 0.32f);
            col.usedByEffector = true;
            var eff = go.AddComponent<PlatformEffector2D>();
            eff.useOneWay = true;
            eff.surfaceArc = 180f;
            return go;
        }

        // ---------- Death zone / spawn points ----------

        public static void MakeDeathZones(Transform parent, float halfW, float topY, float bottomY,
                                          Transform debugSpawn)
        {
            var channel = AssetDatabase.LoadAssetAtPath<ScriptableObject>(PlayerDiedChannelPath);
            MakeDeathZone("DeathZone_Left", new Vector2(-halfW, (topY + bottomY) * 0.5f),
                          new Vector2(8f, topY - bottomY + 20f), parent, channel, debugSpawn);
            MakeDeathZone("DeathZone_Right", new Vector2(halfW, (topY + bottomY) * 0.5f),
                          new Vector2(8f, topY - bottomY + 20f), parent, channel, debugSpawn);
            MakeDeathZone("DeathZone_Top", new Vector2(0f, topY), new Vector2(halfW * 2f + 20f, 8f),
                          parent, channel, debugSpawn);
            MakeDeathZone("DeathZone_Bottom", new Vector2(0f, bottomY), new Vector2(halfW * 2f + 20f, 8f),
                          parent, channel, debugSpawn);
        }

        public static DeathZone MakeDeathZone(string name, Vector2 pos, Vector2 size, Transform parent,
                                              ScriptableObject channel, Transform debugSpawn)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.layer = GameLayers.DeathZone;
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = size;
            var dz = go.AddComponent<DeathZone>();
            if (channel != null) SetObj(dz, "onPlayerDied", channel);
            SetMask(dz, "playerLayers", GameLayers.Player1Mask | GameLayers.Player2Mask);
            SetBool(dz, "debugSelfRespawn", false); // production-ready cho P12; tool test fighters sẽ bật lại.
            if (debugSpawn != null) SetObj(dz, "debugSpawnPoint", debugSpawn);
            return dz;
        }

        public static Transform MakeSpawnPoints(Transform parent, Vector2 p1, Vector2 p2, Vector2 respawn)
        {
            var root = new GameObject("StageSpawnPoints");
            root.transform.SetParent(parent, false);
            var sp = root.AddComponent<StageSpawnPoints>();

            Transform Mk(string n, Vector2 pos)
            {
                var t = new GameObject(n).transform;
                t.SetParent(root.transform, false);
                t.position = pos;
                return t;
            }

            var t1 = Mk("SpawnP1", p1);
            var t2 = Mk("SpawnP2", p2);
            var tr = Mk("RespawnPoint", respawn);
            SetObj(sp, "spawnP1", t1);
            SetObj(sp, "spawnP2", t2);
            SetObj(sp, "respawnPoint", tr);
            return tr;
        }

        // ---------- Camera (Cinemachine 3 + Confiner) ----------

        public static CinemachineTargetGroup MakeCameraRig(Transform parent, Rect bounds, float orthoSize,
                                                           Color bgColor)
        {
            // Main Camera + Brain.
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(CinemachineBrain));
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(parent, false);
            camGo.transform.position = new Vector3(0f, (bounds.yMin + bounds.yMax) * 0.5f, -10f);
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bgColor;

            // Bounds cho Confiner.
            var boundsGo = new GameObject("CameraBounds", typeof(PolygonCollider2D));
            boundsGo.transform.SetParent(parent, false);
            var poly = boundsGo.GetComponent<PolygonCollider2D>();
            poly.isTrigger = true;
            poly.SetPath(0, new[]
            {
                new Vector2(bounds.xMin, bounds.yMin), new Vector2(bounds.xMax, bounds.yMin),
                new Vector2(bounds.xMax, bounds.yMax), new Vector2(bounds.xMin, bounds.yMax)
            });

            // Target group (fighter add vào lúc test/spawn).
            var groupGo = new GameObject("CameraTargetGroup", typeof(CinemachineTargetGroup));
            groupGo.transform.SetParent(parent, false);
            var group = groupGo.GetComponent<CinemachineTargetGroup>();

            // CinemachineCamera + composer + confiner.
            var cmGo = new GameObject("CinemachineCamera", typeof(CinemachineCamera),
                                      typeof(CinemachinePositionComposer), typeof(CinemachineConfiner2D));
            cmGo.transform.SetParent(parent, false);
            cmGo.transform.position = new Vector3(0f, (bounds.yMin + bounds.yMax) * 0.5f, -10f);
            var cm = cmGo.GetComponent<CinemachineCamera>();
            cm.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            cm.Lens.OrthographicSize = orthoSize;
            cm.Follow = groupGo.transform;
            var confiner = cmGo.GetComponent<CinemachineConfiner2D>();
            confiner.BoundingShape2D = poly;

            // Camera khung CỐ ĐỊNH, chỉ bám theo nhóm player (nhìn bao quát map, không auto-zoom).
            // Muốn bật lại auto-zoom kiểu Smash: AddComponent<CinemachineGroupFraming> trong Inspector.
            EditorUtility.SetDirty(cm);
            EditorUtility.SetDirty(confiner);
            return group;
        }

        // ---------- Light2D (qua reflection — không cần asmdef ref URP) ----------

        private static readonly System.Type Light2DType = System.Type.GetType(
            "UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.Runtime");

        public static Component MakeGlobalLight(Transform parent, Color color, float intensity)
            => MakeLight("GlobalLight2D", parent, Vector2.zero, 4 /*Global*/, color, intensity, 0f);

        public static Component MakePointLight(Transform parent, string name, Vector2 pos, Color color,
                                               float intensity, float radius)
            => MakeLight(name, parent, pos, 3 /*Point*/, color, intensity, radius);

        private static Component MakeLight(string name, Transform parent, Vector2 pos, int lightTypeValue,
                                           Color color, float intensity, float radius)
        {
            if (Light2DType == null)
            {
                Debug.LogWarning("[StageSceneKit] Không tìm thấy URP Light2D — bỏ qua ánh sáng (map vẫn dựng bình thường).");
                return null;
            }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var comp = go.AddComponent(Light2DType);
            var so = new SerializedObject(comp);
            var pType = so.FindProperty("m_LightType");
            if (pType != null) pType.intValue = lightTypeValue;
            var pColor = so.FindProperty("m_Color");
            if (pColor != null) pColor.colorValue = color;
            var pInt = so.FindProperty("m_Intensity");
            if (pInt != null) pInt.floatValue = intensity;
            if (radius > 0f)
            {
                var pOut = so.FindProperty("m_PointLightOuterRadius");
                if (pOut != null) pOut.floatValue = radius;
                var pIn = so.FindProperty("m_PointLightInnerRadius");
                if (pIn != null) pIn.floatValue = radius * 0.25f;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(comp);
            return comp;
        }

        // ---------- Particles (pixel-square, material Sprites-Default) ----------

        public static ParticleSystem MakeParticles(Transform parent, string name, Vector2 pos, Vector2 boxSize,
                                                   Color colA, Color colB, float rate, float lifetime,
                                                   Vector2 velocity, float sizeMin, float sizeMax,
                                                   float gravity = 0f, bool playOnAwake = true)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.playOnAwake = playOnAwake;
            main.loop = true;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(colA, colB);
            main.gravityModifier = gravity;
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(boxSize.x, boxSize.y, 1f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            // Cả 3 trục PHẢI cùng mode (TwoConstants) — lệch mode Unity báo lỗi mỗi frame.
            vel.x = new ParticleSystem.MinMaxCurve(velocity.x * 0.7f, velocity.x * 1.3f);
            vel.y = new ParticleSystem.MinMaxCurve(velocity.y * 0.7f, velocity.y * 1.3f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            renderer.sortingOrder = 35;

            if (!playOnAwake) ps.Stop();
            return ps;
        }

        // ---------- Stage registry ----------

        public static void AddStageToRegistry(string displayName, string sceneName, Sprite icon)
        {
            var reg = AssetDatabase.LoadAssetAtPath<StageRegistrySO>(StageRegistryPath);
            if (reg == null)
            {
                reg = ScriptableObject.CreateInstance<StageRegistrySO>();
                string dir = Path.GetDirectoryName(StageRegistryPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                AssetDatabase.CreateAsset(reg, StageRegistryPath);
            }

            var so = new SerializedObject(reg);
            var entries = so.FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                var e = entries.GetArrayElementAtIndex(i);
                if (e.FindPropertyRelative("sceneName").stringValue == sceneName)
                    return; // đã có
            }
            int idx = entries.arraySize;
            entries.arraySize = idx + 1;
            var ne = entries.GetArrayElementAtIndex(idx);
            ne.FindPropertyRelative("displayName").stringValue = displayName;
            ne.FindPropertyRelative("sceneName").stringValue = sceneName;
            var iconProp = ne.FindPropertyRelative("icon");
            if (iconProp != null && icon != null) iconProp.objectReferenceValue = icon;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(reg);
        }

        /// <summary>StageConfig asset: load sẵn có hoặc tạo mới (dùng cho StageConfig_Swamp).</summary>
        public static StageConfigSO EnsureStageConfig(string path)
        {
            var cfg = AssetDatabase.LoadAssetAtPath<StageConfigSO>(path);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<StageConfigSO>();
                AssetDatabase.CreateAsset(cfg, path);
            }
            return cfg;
        }
    }
}
