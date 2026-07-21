using System.Collections.Generic;
using System.Linq;
using FA.Core;
using FA.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FA.EditorTools
{
    /// <summary>Creates and wires the Rage Orb assets without hand-editing scene YAML.</summary>
    [InitializeOnLoad]
    public static class RageOrbSetupTool
    {
        private const string SpritePath = "Assets/_Project/Art/Items/RageOrb.png";
        private const string OrbPrefabPath = "Assets/_Project/Prefabs/Items/RageOrb.prefab";
        private const string SystemPrefabPath = "Assets/_Project/Prefabs/Items/RageOrbSystem.prefab";
        private const string ConfigPath = "Assets/_Project/Data/Items/RageOrbConfig.asset";
        private const string HitChannelPath = "Assets/_Project/Data/OnHitLandedChannel.asset";
        private const string MatchStartPath = "Assets/_Project/Data/GameFlow/OnMatchStartChannel.asset";
        private const string MatchEndedPath = "Assets/_Project/Data/GameFlow/OnMatchEndedChannel.asset";
        private const string PoolConfigPath = "Assets/_Project/Data/GameFlow/PoolConfig.asset";
        private const string ParticleMaterialPath = "Assets/_Project/Prefabs/VFX/Emiya_Archer/Super2Particle.mat";
        private const string SetupVersion = "rage-orb-vfx-v2";

        private static readonly string[] TargetScenes =
        {
            "Assets/_Project/Scenes/Lobby.unity",
            "Assets/_Project/Scenes/Items.unity"
        };

        static RageOrbSetupTool()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        [MenuItem("FA/Tools/Setup Rage Orb")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Sprite sprite = ImportSprite();
            if (sprite == null)
            {
                Debug.LogError($"[RageOrbSetup] Missing sprite: {SpritePath}");
                return;
            }

            RageOrb orbPrefab = BuildOrbPrefab(sprite);
            GameObject systemPrefab = BuildSystemPrefab(orbPrefab);
            AddToPoolConfig(orbPrefab.gameObject);
            foreach (string scenePath in TargetScenes) WireScene(scenePath, systemPrefab);

            AssetImporter orbImporter = AssetImporter.GetAtPath(OrbPrefabPath);
            if (orbImporter != null)
            {
                orbImporter.userData = SetupVersion;
                AssetDatabase.WriteImportSettingsIfDirty(OrbPrefabPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[RageOrbSetup] DONE — prefab, pool and Lobby/Items wiring are ready.");
        }

        private static void AutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
                return;
            }
            GameObject orb = AssetDatabase.LoadAssetAtPath<GameObject>(OrbPrefabPath);
            if (orb != null && orb.GetComponent<RageOrbVfx>() != null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(SystemPrefabPath) != null &&
                AssetImporter.GetAtPath(OrbPrefabPath)?.userData == SetupVersion)
                return;
            Run();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.delayCall += AutoSetup;
        }

        private static Sprite ImportSprite()
        {
            AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
            if (importer == null) return null;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 512f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }

        private static RageOrb BuildOrbPrefab(Sprite sprite)
        {
            var root = new GameObject("RageOrb");
            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            RageOrb orb = root.AddComponent<RageOrb>();

            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(root.transform, false);
            var visual = visualObject.AddComponent<SpriteRenderer>();
            visual.sprite = sprite;
            visual.sortingOrder = 10;

            var glowObject = new GameObject("GlowPulse");
            glowObject.transform.SetParent(root.transform, false);
            glowObject.transform.localScale = Vector3.one * 1.16f;
            var glow = glowObject.AddComponent<SpriteRenderer>();
            glow.sprite = sprite;
            glow.sortingOrder = 9;
            glow.color = new Color(0.55f, 0.12f, 1f, 0.18f);

            Material particleMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
            ParticleSystem aura = CreateParticles(root.transform, "Aura", particleMaterial, true,
                0.9f, -0.22f, 0.2f, 16f, 0.92f, new Color(0.5f, 0.08f, 1f, 0.62f), 8, 0.8f, false);
            ParticleSystem sparks = CreateParticles(root.transform, "OrbitingSparks", particleMaterial, true,
                0.7f, 0.08f, 0.075f, 7f, 0.75f, new Color(1f, 0.5f, 0.08f, 0.9f), 12, 2.4f, true);
            ParticleSystem hit = CreateParticles(root.transform, "HitBurst", particleMaterial, false,
                0.24f, 1.8f, 0.11f, 0f, 0.35f, new Color(1f, 0.7f, 0.12f, 1f), 13, 0f, false);
            ParticleSystem broken = CreateParticles(root.transform, "BreakBurst", particleMaterial, false,
                0.42f, 3.4f, 0.16f, 0f, 0.28f, new Color(1f, 0.18f, 0.04f, 1f), 14, 0f, true);

            RageOrbVfx vfx = root.AddComponent<RageOrbVfx>();
            var vfxSerialized = new SerializedObject(vfx);
            vfxSerialized.FindProperty("glow").objectReferenceValue = glow;
            vfxSerialized.FindProperty("ambientAura").objectReferenceValue = aura;
            vfxSerialized.FindProperty("orbitingSparks").objectReferenceValue = sparks;
            vfxSerialized.FindProperty("hitBurst").objectReferenceValue = hit;
            vfxSerialized.FindProperty("breakBurst").objectReferenceValue = broken;
            vfxSerialized.ApplyModifiedPropertiesWithoutUndo();

            var hurtboxObject = new GameObject("Hurtbox");
            hurtboxObject.layer = LayerMask.NameToLayer("Hurtbox");
            hurtboxObject.transform.SetParent(root.transform, false);
            var hurtbox = hurtboxObject.AddComponent<CircleCollider2D>();
            hurtbox.isTrigger = true;
            hurtbox.radius = 0.78f;

            var serialized = new SerializedObject(orb);
            serialized.FindProperty("config").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<RageOrbConfigSO>(ConfigPath);
            serialized.FindProperty("onHitLanded").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<HitEventChannelSO>(HitChannelPath);
            serialized.FindProperty("visual").objectReferenceValue = visual;
            serialized.FindProperty("vfx").objectReferenceValue = vfx;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, OrbPrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<RageOrb>();
        }

        private static ParticleSystem CreateParticles(Transform parent, string name, Material material, bool loop,
            float lifetime, float speed, float size, float rate, float radius, Color color, int sortingOrder,
            float orbitalSpeed, bool trailsEnabled)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            ParticleSystem particles = go.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = loop;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.maxParticles = loop ? 80 : 64;

            var emission = particles.emission;
            emission.rateOverTime = rate;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = 0.2f;

            if (!Mathf.Approximately(orbitalSpeed, 0f))
            {
                var velocity = particles.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.orbitalZ = orbitalSpeed;
            }

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            if (trailsEnabled)
            {
                var trails = particles.trails;
                trails.enabled = true;
                trails.ratio = loop ? 0.65f : 1f;
                trails.lifetime = 0.22f;
                trails.dieWithParticles = true;
                trails.sizeAffectsWidth = true;
            }

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.trailMaterial = material;
            renderer.sortingOrder = sortingOrder;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return particles;
        }

        private static GameObject BuildSystemPrefab(RageOrb orbPrefab)
        {
            var root = new GameObject("RageOrbSystem");
            RageOrbSpawner spawner = root.AddComponent<RageOrbSpawner>();
            var serialized = new SerializedObject(spawner);
            serialized.FindProperty("orbPrefab").objectReferenceValue = orbPrefab;
            serialized.FindProperty("config").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<RageOrbConfigSO>(ConfigPath);
            serialized.FindProperty("onMatchStart").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<VoidEventChannelSO>(MatchStartPath);
            serialized.FindProperty("onMatchEnded").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<VoidEventChannelSO>(MatchEndedPath);
            serialized.FindProperty("autoStartOnEnable").boolValue = false;
            serialized.FindProperty("earliestSeconds").floatValue = 45f;
            serialized.FindProperty("latestSeconds").floatValue = 60f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, SystemPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void AddToPoolConfig(GameObject orbPrefab)
        {
            PoolConfigSO config = AssetDatabase.LoadAssetAtPath<PoolConfigSO>(PoolConfigPath);
            if (config == null || config.entries.Any(e => e.prefab == orbPrefab)) return;

            var entries = new List<PoolConfigSO.Entry>(config.entries ?? new PoolConfigSO.Entry[0])
            {
                new PoolConfigSO.Entry { prefab = orbPrefab, prewarm = 1, maxSize = 1 }
            };
            config.entries = entries.ToArray();
            EditorUtility.SetDirty(config);
        }

        private static void WireScene(string scenePath, GameObject systemPrefab)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            bool exists = scene.GetRootGameObjects().Any(go => go.GetComponentInChildren<RageOrbSpawner>(true) != null);
            if (!exists)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(systemPrefab, scene);
                instance.name = "RageOrbSystem";
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
