using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using FA.Core;

namespace FA.EditorTools
{
    /// <summary>
    /// BUILDER asset trình diễn cho Ultimate UBW (tách khỏi UltimateSetupTool giữ file &lt;500 dòng §1.2):
    /// background Reality Marble (sprite nhóm 25), bộ EffectData slot 16-23, aura lửa đỏ Rage Mode.
    /// </summary>
    internal static class UltimateFxBuilder
    {
        private const string BgDir = "Assets/_Project/Art/Characters/Emiya Archer/UBW_Background";
        private const string AudioDir = "Assets/_Project/Audio/Emiya Archer";
        private const string EffectsDataDir = "Assets/_Project/Data/Emiya_Archer/Effects";
        private const string BgPrefabPath = "Assets/_Project/Prefabs/VFX/UBW_Background.prefab";

        /// <summary>Background Reality Marble: trời + bánh răng + layer + đồi kiếm. Order RẤT ÂM — nằm
        /// DƯỚI toàn bộ tilemap (0): map thật giữ nguyên, chỉ nền trống lộ UBW. KÍCH THƯỚC LỚN (trời
        /// 50u) phủ vùng camera/death-zone kẻo lộ khoảng xanh (user chốt). Vị trí/scale CẦN TEST.</summary>
        internal static GameObject CreateBackgroundPrefab()
        {
            var root = new GameObject("UBW_Background");
            try
            {
                void Layer(string sprite, Vector2 pos, float scale, int order)
                {
                    var go = new GameObject(sprite);
                    go.transform.SetParent(root.transform, false);
                    go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
                    go.transform.localScale = new Vector3(scale, scale, 1f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = Special3SetupTool.LoadSprite(BgDir, sprite);
                    sr.sortingOrder = order;
                    if (sr.sprite == null) Debug.LogError($"[UltimateFx] Thiếu bg sprite {sprite}");
                }
                Layer("25-0", new Vector2(0f, 4f), 5f, -60);        // bầu trời cam (50×47u)
                Layer("25-60", new Vector2(2.2f, 3f), 2.2f, -58);   // bánh răng lớn
                Layer("25-7", new Vector2(0f, 1f), 4f, -56);        // layer giữa
                Layer("25-50", new Vector2(0f, -2.4f), 2.6f, -54);  // đồi kiếm cắm (52u)
                return PrefabUtility.SaveAsPrefabAsset(root, BgPrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>EffectData slot 16-23. Voice chant bản MUGEN này CHỈ THU 2 câu (S3000-0/1) + 2 câu
        /// kết tách file (S3000-5 "So as I pray," + S3000-6 "Unlimited Blade Works."); S3002-0 là NHẠC
        /// dâng từ giữa chant (user xác nhận), S3002-5 là BGM loop trong UBW (UBWInstallController).</summary>
        internal static Dictionary<string, EffectData> CreateEffectAssets()
        {
            GameObject wave = Super2FxBuilder.CreateFlipbook("UBWInstallWave_VFX",
                Enumerable.Range(0, 10).Select(i => $"844-{i}").ToArray(), 20f, 0.6f, 12);

            AudioClip Clip(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{n}.wav");
            var result = new Dictionary<string, EffectData>();
            EffectData Make(string name, GameObject vfx, Vector2 offset, bool attach, string[] clips, float vol)
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
                    Debug.LogError($"[UltimateFx] {name}: thiếu AudioClip ({fx.sfxClips.Length}/{clips.Length})");
                fx.volume = vol;
                fx.scale = 1f;
                EditorUtility.SetDirty(fx);
                result[name] = fx;
                return fx;
            }

            // Voice từng dòng chant đi qua ChantTextOverlay (đồng bộ chữ+tiếng), nhạc qua
            // MusicSequencePlayer — CEP chỉ còn: announce, install boom, "Blade Works.", Rage_Ignite.
            Make("Ulti_Announce", null, Vector2.zero, false, new[] { "S6100_10" }, 1f);
            Make("Ulti_Install", wave, new Vector2(0f, 0.4f), false, new[] { "S3105_0" }, 1f);
            Make("Ulti_BladeWorks", null, Vector2.zero, false, new[] { "S3000_6" }, 1f);    // "…Blade Works."
            Make("Rage_Ignite", null, Vector2.zero, false, new[] { "S505_1" }, 0.9f);       // char khác dùng.
            return result;
        }

        /// <summary>Aura LỬA ĐỎ Rage Mode (feedback: "ấn nộ lần 1 không thấy gì"): ParticleSystem child,
        /// RageModeController Play/Stop theo mode (hook auraParticles P10 sẵn có). playOnAwake OFF.</summary>
        internal static ParticleSystem BuildRageAura(GameObject root)
        {
            Transform old = root.transform.Find("RageAura");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject("RageAura");
            go.transform.SetParent(root.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 0.5f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.35f, 0.1f, 0.85f), new Color(1f, 0.75f, 0.2f, 0.7f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = ps.emission; emission.rateOverTime = 42f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0f, 0.3f, 0f);
            shape.scale = new Vector3(0.35f, 0.55f, 0.1f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.x = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);     // CẢ 3 TRỤC cùng mode (gotcha PS).
            vel.y = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Prefabs/VFX/Super2Particle.mat");
            r.sortingOrder = 8;
            ps.Stop();
            return ps;
        }
    }
}
