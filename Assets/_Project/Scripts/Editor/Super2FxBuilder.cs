using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using FA.Core;

namespace FA.EditorTools
{
    /// <summary>
    /// BUILDER VFX/EffectData cho Super 2 (tách khỏi Super2SetupTool giữ file &lt;500 dòng — §1.2).
    /// Feedback user 2026-07-10: hiệu ứng phải DÀY LỚP hơn:
    ///  - Mũi tên: 3 lớp (beam chính + glow bao ngoài + vệt đuôi 760) + trail rộng.
    ///  - Vụ nổ: 3 lớp (cầu lửa 758 + trụ sáng 760 + vòng năng lượng 798) + tiếng boom composite.
    ///  - Kéo cung: ParticleSystem tụ lực (gió hút từ trước mặt về + chùm sáng ở đầu cung) — đúng
    ///    bảng §1.1: hiệu ứng hạt = ParticleSystem, KHÔNG code chuyển động tay. Chạy UNSCALED
    ///    (main.useUnscaledTime) để sống trong super flash freeze.
    /// </summary>
    internal static class Super2FxBuilder
    {
        private const string VfxDir = "Assets/_Project/Art/Characters/Emiya Archer/VFX";
        private const string AudioDir = "Assets/_Project/Audio/Emiya Archer";
        private const string EffectsDataDir = "Assets/_Project/Data/Emiya_Archer/Effects";
        private const string VfxPrefabDir = "Assets/_Project/Prefabs/VFX";

        internal static string[] Range(int group, int start, int count) =>
            Enumerable.Range(start, count).Select(i => $"{group}-{i}").ToArray();

        /// <summary>Flipbook one-shot/loop (bọc Special3SetupTool.CreateFlipbookPrefab + set loop/life).</summary>
        internal static GameObject CreateFlipbook(string name, string[] sprites, float fps, float scale,
                                                  int order, bool loop = false, float life = 0f)
        {
            GameObject prefab = Special3SetupTool.CreateFlipbookPrefab(name, sprites, fps, scale, order);
            if (!loop && life <= 0f) return prefab;
            GameObject contents = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));
            try
            {
                var so = new SerializedObject(contents.GetComponent<FA.Effects.FlipbookOneShot>());
                so.FindProperty("loop").boolValue = loop;
                so.FindProperty("lifeSeconds").floatValue = life;
                so.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(contents, AssetDatabase.GetAssetPath(prefab));
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        // ---------------------------------------------------------------- Mũi tên: lớp phụ
        /// <summary>Thêm 2 lớp visual lên arrow prefab (gọi khi root đang mở): GLOW 815 phóng to mờ bao
        /// ngoài + VỆT ĐUÔI 760 phía sau — beam trông dày/lực hơn (feedback user).</summary>
        internal static void AddArrowVisualLayers(GameObject root)
        {
            AddFlipbookChild(root, "Glow", Range(815, 0, 10), 30f, loop: true,
                localPos: new Vector3(-0.12f, 0f, 0f), localScale: new Vector3(1.7f, 1.9f, 1f),
                tint: new Color(1f, 0.45f, 0.45f, 0.5f), order: 5);
            AddFlipbookChild(root, "Tail", Range(760, 10, 14), 24f, loop: true,
                localPos: new Vector3(-1.1f, 0f, 0f), localScale: new Vector3(1.9f, 0.9f, 1f),
                tint: new Color(1f, 0.4f, 0.4f, 0.65f), order: 4);
        }

        private static void AddFlipbookChild(GameObject root, string name, string[] frames, float fps,
                                             bool loop, Vector3 localPos, Vector3 localScale,
                                             Color tint, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var sr = go.AddComponent<SpriteRenderer>();
            Sprite[] sprites = frames.Select(n => Special3SetupTool.LoadSprite(VfxDir, n))
                                     .Where(s => s != null).ToArray();
            sr.sprite = sprites.Length > 0 ? sprites[0] : null;
            sr.color = tint;
            sr.sortingOrder = order;
            var flip = go.AddComponent<FA.Effects.FlipbookOneShot>();
            var so = new SerializedObject(flip);
            SerializedProperty pFrames = so.FindProperty("frames");
            pFrames.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
                pFrames.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            so.FindProperty("framesPerSecond").floatValue = fps;
            so.FindProperty("loop").boolValue = loop;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- Vụ nổ 3 lớp
        /// <summary>Vụ nổ MUGEN 2131 đủ lớp: CẦU LỬA 1418 (758) + TRỤ SÁNG 2138 (760) + VÒNG năng
        /// lượng (798 tint cam). Root = cầu lửa (one-shot tự hủy kéo cả children theo).</summary>
        internal static GameObject CreateExplosionPrefab()
        {
            string path = $"{VfxPrefabDir}/Super2Explosion_VFX.prefab";
            GameObject prefab = Special3SetupTool.CreateFlipbookPrefab("Super2Explosion_VFX",
                Range(758, 1, 12), 30f, 1f, 13);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // Idempotent: xóa children cũ (kể cả _Layer2 của bản 2-lớp trước) rồi dựng lại.
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                AddFlipbookChild(root, "Pillar", Range(760, 10, 14), 24f, loop: false,
                    localPos: Vector3.zero, localScale: new Vector3(1.15f, 1.6f, 1f),
                    tint: new Color(1f, 0.75f, 0.6f, 0.9f), order: 12);
                AddFlipbookChild(root, "Ring", Range(798, 0, 7), 18f, loop: false,
                    localPos: Vector3.zero, localScale: new Vector3(1.7f, 1.7f, 1f),
                    tint: new Color(1f, 0.6f, 0.35f, 0.85f), order: 11);
                root.transform.localScale = new Vector3(0.7f, 0.7f, 1f);   // canvas 758 = 10u → ~7u. CẦN TEST.
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ---------------------------------------------------------------- Tụ lực khi kéo cung
        /// <summary>Prefab GIÓ TỤ LỰC: 2 ParticleSystem — (a) vệt gió hút từ TRƯỚC MẶT về thân,
        /// (b) chùm sáng nhỏ tỏa từ đầu cung. Texture hạt = mảnh năng lượng MUGEN 772. Root PS
        /// stopAction=Destroy tự dọn; unscaled để chạy trong flash freeze. Facing: CharacterEffectPlayer
        /// lật localScale.x → simulation Local mirror theo.</summary>
        internal static GameObject CreateChargeWindPrefab()
        {
            string path = $"{VfxPrefabDir}/Super2Wind_VFX.prefab";
            Material mat = CreateParticleMaterial();
            var root = new GameObject("Super2Wind_VFX");
            try
            {
                ParticleSystem wind = AddParticles(root, "Wind", mat, order: 12);
                var wMain = wind.main;
                wMain.duration = 2.6f;                       // phủ trọn draw t0→t95 + hold 1s. CẦN TEST.
                wMain.startLifetime = 0.35f;
                wMain.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
                wMain.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.55f, 0.85f, 1f, 0.75f), new Color(1f, 1f, 1f, 0.55f));
                wMain.stopAction = ParticleSystemStopAction.Destroy;   // hết duration tự Destroy root.
                var wEmit = wind.emission; wEmit.rateOverTime = 34f;
                var wShape = wind.shape;
                wShape.shapeType = ParticleSystemShapeType.Box;
                wShape.position = new Vector3(0.9f, 0.42f, 0f);        // vùng TRƯỚC MẶT.
                wShape.scale = new Vector3(0.5f, 0.85f, 0.1f);
                var wVel = wind.velocityOverLifetime;
                wVel.enabled = true;
                // CẢ 3 TRỤC phải cùng MinMaxCurve mode ("Particle Velocity curves must all be in the
                // same mode") — z cũng khai dạng khoảng (0,0) như x/y, không để mặc định constant.
                wVel.x = new ParticleSystem.MinMaxCurve(-3.4f, -2.2f); // hút VỀ SAU (thân/cung).
                wVel.y = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
                wVel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
                FadeOut(wind);
                Stretch(wind, mat, velocityScale: 0.14f, lengthScale: 2.2f, order: 12);

                var glintGo = new GameObject("BowGlint");
                glintGo.transform.SetParent(root.transform, false);
                glintGo.transform.localPosition = new Vector3(0.28f, 0.42f, 0f);   // đầu cung/mũi tên.
                ParticleSystem glint = AddParticles(glintGo, null, mat, order: 13);
                var gMain = glint.main;
                gMain.duration = 2.6f;
                gMain.startLifetime = 0.2f;
                gMain.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);     // tỏa ra từ 1 điểm.
                gMain.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
                gMain.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(1f, 0.95f, 0.7f, 0.95f), new Color(0.6f, 0.9f, 1f, 0.9f));
                var gEmit = glint.emission; gEmit.rateOverTime = 20f;
                var gShape = glint.shape;
                gShape.shapeType = ParticleSystemShapeType.Sphere;
                gShape.radius = 0.03f;
                FadeOut(glint);
                Stretch(glint, mat, velocityScale: 0.1f, lengthScale: 3f, order: 13);

                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static ParticleSystem AddParticles(GameObject host, string childName, Material mat, int order)
        {
            GameObject go = host;
            if (childName != null)
            {
                go = new GameObject(childName);
                go.transform.SetParent(host.transform, false);
            }
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.useUnscaledTime = true;    // sống trong super flash (Time.timeScale = 0).
            main.loop = false;
            main.startSpeed = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            return ps;
        }

        private static void FadeOut(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        private static void Stretch(ParticleSystem ps, Material mat, float velocityScale,
                                    float lengthScale, int order)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = velocityScale;
            r.lengthScale = lengthScale;
            r.material = mat;
            r.sortingOrder = order;
        }

        private static Material CreateParticleMaterial()
        {
            string path = $"{VfxPrefabDir}/Super2Particle.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{VfxDir}/772-13.png");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------- EffectData
        /// <summary>Toàn bộ EffectData Super 2 (slot 9-15 + vụ nổ cho drill). Boom = WAV composite
        /// Super2_Boom (S2105-10 trầm + S7-35 đanh, ghép offline bằng Tools/mugen_extract pipeline).</summary>
        internal static Dictionary<string, EffectData> CreateEffectAssets()
        {
            GameObject orb = CreateFlipbook("Super2ChargeOrb_VFX", Range(802, 0, 8), 15f, 0.2f, 12,
                                            loop: true, life: 0.95f);
            GameObject ring = CreateFlipbook("Super2Ring_VFX", Range(798, 0, 7), 15f, 0.45f, 12);
            GameObject muzzle = Special3SetupTool.CreateLayeredSparkPrefab("Super2Muzzle_VFX",
                Range(711, 20, 13), 20f, Range(789, 0, 8), 24f, 0.7f);   // 2 lớp: 8007 + burst 2137.
            GameObject explosion = CreateExplosionPrefab();
            GameObject wind = CreateChargeWindPrefab();
            GameObject flash = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{VfxPrefabDir}/Super1Flash_VFX.prefab");   // anim 6106 dùng chung mọi super.

            AudioClip Clip(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{n}.wav");
            var result = new Dictionary<string, EffectData>();
            EffectData Make(string name, GameObject vfx, Vector2 offset, bool attach, string[] clips,
                            float vol, float scale = 1f)
            {
                string path = $"{EffectsDataDir}/{name}.asset";
                var fx = AssetDatabase.LoadAssetAtPath<EffectData>(path);
                if (fx == null)
                {
                    fx = ScriptableObject.CreateInstance<EffectData>();
                    AssetDatabase.CreateAsset(fx, path);
                }
                fx.vfxPrefab = vfx;
                fx.offset = offset;
                fx.attachToOwner = attach;
                fx.sfxClips = clips.Select(Clip).Where(c => c != null).ToArray();
                if (fx.sfxClips.Length != clips.Length)
                    Debug.LogError($"[Super2Fx] {name}: thiếu AudioClip ({fx.sfxClips.Length}/{clips.Length})");
                fx.volume = vol;
                fx.scale = scale;
                EditorUtility.SetDirty(fx);
                result[name] = fx;
                return fx;
            }

            Make("Super2_Cast", flash, new Vector2(0f, 0.4f), true, new[] { "S6100_2" }, 1f);
            Make("Super2_Draw", null, Vector2.zero, false, new[] { "S2100_0" }, 0.95f);
            Make("Super2_Charge", orb, new Vector2(-0.16f, 0.38f), true, new[] { "S2105_0" }, 0.95f);
            Make("Super2_Release", ring, new Vector2(0f, 0.4f), true, new[] { "S404_2" }, 0.9f);
            Make("Super2_Shot", muzzle, new Vector2(0.35f, 0.38f), false, new[] { "S2100_5" }, 1f);
            Make("Super2_ArrowWhoosh", null, Vector2.zero, false, new[] { "S2105_6" }, 0.9f);
            Make("Super2_Wind", wind, Vector2.zero, true, new string[0], 1f);
            Make("Super2_HitExplosion", explosion, Vector2.zero, false, new[] { "Super2_Boom" }, 1f);
            return result;
        }
    }
}
