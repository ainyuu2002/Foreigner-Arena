using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using FA.Core;

namespace FA.EditorTools
{
    /// <summary>
    /// BUILDER VFX/EffectData cho Ultimate 2 "Excalibur" (tách khỏi UltimateExcaliburSetupTool giữ
    /// file &lt;500 dòng — §1.2). VŨ ĐẠO VFX theo user chốt (round 2, đúng vai trò anim MUGEN):
    ///  - 12015 (840) = HÀO QUANG vận khí bao quanh nhân vật (loop suốt charge, TẮT khi chém xuống;
    ///    thêm 1 bản xoay góc theo đường kiếm lúc vung — SlashArc).
    ///  - 12017 (851): user BỎ (khó chỉnh vị trí dọc kiếm) — slot 33 để TRỐNG.
    ///  - 12010 (15-100) = kiếm trắng glow ĐÍNH TRÙNG kiếm thật, chạy từ khi 12001 vào loop.
    ///  - 12016 (849) + 12018 (786) = tia bùng GẦN CUỐI vận, kết thúc ĐÚNG lúc loop vận khí xong.
    ///  - 12011 (15-101..107, LoopStart=5, loop 2 frame ×5t ĐỒNG PHA với loop 813/815 của clip) =
    ///    kiếm trắng theo hành động vung, chạy từ đầu anim 12002 tới recovery.
    ///  - 12012 (15-106..115) = kiếm trắng tan dần, chạy đầu recovery, hết frame ≈ hết đòn.
    ///  - 7145 (796) = VỆT SÁNG THEO TAY trên đường chém (không phải glow trên đầu!) — bake góc chéo.
    ///  - BỎ hẳn "cột sáng chiếu chéo trên đầu" (851 dựng đứng sai chỗ ở round 1).
    /// Rotation bake trên ROOT prefab — CharacterEffectPlayer đã giữ prefab rotation khi Instantiate.
    /// LoopStart giữa dãy = field mới FlipbookOneShot.loopStartIndex.
    /// </summary>
    internal static class UltimateExcaliburFxBuilder
    {
        private const string VfxDir = "Assets/_Project/Art/Characters/Emiya Archer/VFX";
        private const string AudioDir = "Assets/_Project/Audio/Emiya Archer";
        private const string EffectsDataDir = "Assets/_Project/Data/Emiya_Archer/Effects";
        private const string VfxPrefabDir = "Assets/_Project/Prefabs/VFX";

        private static string[] Range(int group, int start, int count) =>
            Enumerable.Range(start, count).Select(i => $"{group}-{i}").ToArray();

        // ---------------------------------------------------------------- Beam: lớp visual (GIỮ round 1)
        /// <summary>Thêm các lớp visual lên beam prefab (gọi khi root đang mở). Sprite 847 gốc
        /// 594×342px, axis trái (59,185) → sau pad_center pivot giữa, beam trải -0.6..+5.3u về phải.</summary>
        internal static void AddBeamVisualLayers(GameObject root)
        {
            string[] core = new[] { "847-0" }.Concat(Range(847, 9, 14)).ToArray();   // intro + thân.
            AddFlipbookChild(root, "Core", core, 20f, loop: true,
                localPos: new Vector3(0.2f, 0f, 0f), localScale: Vector3.one,
                tint: new Color(1f, 1f, 1f, 0.95f), order: 6);
            AddFlipbookChild(root, "Glow", Range(847, 14, 9), 16f, loop: true,
                localPos: new Vector3(0.3f, 0f, 0f), localScale: new Vector3(1.35f, 1.6f, 1f),
                tint: new Color(1f, 0.9f, 0.55f, 0.4f), order: 5);
            AddFlipbookChild(root, "Petals", Range(832, 0, 8), 18f, loop: true,
                localPos: new Vector3(0.8f, -0.5f, 0f), localScale: new Vector3(0.55f, 0.55f, 1f),
                tint: new Color(1f, 1f, 0.85f, 0.85f), order: 7);
            AddFlipbookChild(root, "PetalsFar", Range(832, 0, 8), 14f, loop: true,
                localPos: new Vector3(3.2f, -0.45f, 0f), localScale: new Vector3(0.8f, 0.8f, 1f),
                tint: new Color(1f, 1f, 0.85f, 0.7f), order: 7);
            AddFlipbookChild(root, "Rings", Range(711, 90, 8), 16f, loop: true,
                localPos: new Vector3(1.5f, 0f, 0f), localScale: new Vector3(0.9f, 0.9f, 1f),
                tint: new Color(1f, 0.95f, 0.7f, 0.8f), order: 7);
            AddFlipbookChild(root, "Sparkle", Range(745, 0, 25), 30f, loop: true,
                localPos: new Vector3(2.8f, 0.25f, 0f), localScale: new Vector3(0.8f, 0.8f, 1f),
                tint: new Color(1f, 1f, 1f, 0.9f), order: 8);
        }

        private static void AddFlipbookChild(GameObject root, string name, string[] frames, float fps,
                                             bool loop, Vector3 localPos, Vector3 localScale,
                                             Color tint, int order, float rotationZ = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            if (rotationZ != 0f) go.transform.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.color = tint;
            sr.sortingOrder = order;
            FillFlipbook(go, frames, fps, loop, 0f, 0);
        }

        /// <summary>Điền SpriteRenderer.sprite + FlipbookOneShot lên GameObject có sẵn SpriteRenderer.</summary>
        private static void FillFlipbook(GameObject go, string[] frames, float fps, bool loop,
                                         float life, int loopStart)
        {
            Sprite[] sprites = frames.Select(n => Special3SetupTool.LoadSprite(VfxDir, n))
                                     .Where(s => s != null).ToArray();
            if (sprites.Length != frames.Length)
                Debug.LogError($"[Ulti2Fx] {go.name}: thiếu sprite ({sprites.Length}/{frames.Length})");
            go.GetComponent<SpriteRenderer>().sprite = sprites.Length > 0 ? sprites[0] : null;
            var flip = go.AddComponent<FA.Effects.FlipbookOneShot>();
            var so = new SerializedObject(flip);
            SerializedProperty pFrames = so.FindProperty("frames");
            pFrames.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
                pFrames.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            so.FindProperty("framesPerSecond").floatValue = fps;
            so.FindProperty("loop").boolValue = loop;
            so.FindProperty("lifeSeconds").floatValue = life;
            so.FindProperty("loopStartIndex").intValue = loopStart;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Prefab flipbook 1 lớp đầy đủ tùy chọn: loop-từ-giữa (LoopStart AIR), life, rotation
        /// bake trên ROOT (CharacterEffectPlayer giữ prefab rotation), sortingOrder (ÂM = sau nhân vật).</summary>
        private static GameObject CreateFx(string name, string[] frames, float fps, int order,
                                           bool loop = false, float life = 0f, int loopStart = 0,
                                           float rotationZ = 0f)
        {
            string path = $"{VfxPrefabDir}/{name}.prefab";
            var root = new GameObject(name);
            try
            {
                if (rotationZ != 0f) root.transform.rotation = Quaternion.Euler(0f, 0f, rotationZ);
                var sr = root.AddComponent<SpriteRenderer>();
                sr.sortingOrder = order;
                FillFlipbook(root, frames, fps, loop, life, loopStart);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>Flash KẾT beam (MUGEN 12025/12023/12030) — GIỮ round 1: burst 820 root + flash 732
        /// + ring 711. Drill spawn tại vị trí beam khi vào pha nổ.</summary>
        private static GameObject CreateBeamEndPrefab()
        {
            string path = $"{VfxPrefabDir}/Ulti2BeamEnd_VFX.prefab";
            GameObject prefab = Special3SetupTool.CreateFlipbookPrefab("Ulti2BeamEnd_VFX",
                Range(820, 0, 18), 30f, 1.1f, 12);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                AddFlipbookChild(root, "Flash", new[] { "732-0" }, 3f, loop: false,
                    localPos: Vector3.zero, localScale: new Vector3(1.6f, 1.6f, 1f),
                    tint: new Color(1f, 0.95f, 0.8f, 0.9f), order: 13);
                AddFlipbookChild(root, "Rings", Range(711, 90, 8), 20f, loop: false,
                    localPos: Vector3.zero, localScale: new Vector3(1.3f, 1.3f, 1f),
                    tint: new Color(1f, 0.85f, 0.55f, 0.9f), order: 11);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>Dọn prefab/EffectData round 1 đã bị thay vai (tránh asset mồ côi gây nhầm).</summary>
        private static void DeleteStaleRound1Assets()
        {
            string[] stale =
            {
                $"{VfxPrefabDir}/Ulti2TraceGlow_VFX.prefab",
                $"{VfxPrefabDir}/Ulti2GroundBurst_VFX.prefab",
                $"{VfxPrefabDir}/Ulti2ChargePillar_VFX.prefab",
                $"{VfxPrefabDir}/Ulti2Rays_VFX.prefab",
                $"{EffectsDataDir}/Ulti2_TraceGlow.asset",
                $"{EffectsDataDir}/Ulti2_GroundBurst.asset",
                $"{EffectsDataDir}/Ulti2_ChargePillar.asset",
                // Round 3: user bỏ 12017 (cột sáng dọc kiếm — khó chỉnh).
                $"{VfxPrefabDir}/Ulti2SwordColumn_VFX.prefab",
                $"{EffectsDataDir}/Ulti2_SwordColumn.asset",
            };
            foreach (string p in stale) AssetDatabase.DeleteAsset(p);
        }

        // ---------------------------------------------------------------- EffectData
        /// <summary>Toàn bộ EffectData Ultimate 2 (CEP slot 24-37 + BeamEnd cho drill). Timing xem
        /// UltimateExcaliburSetupTool. LƯU Ý: sound trong EffectData chỉ phát khi có event PlaySfx —
        /// PlayVfx KHÔNG phát sfx (2 event tách bạch).</summary>
        internal static Dictionary<string, EffectData> CreateEffectAssets()
        {
            DeleteStaleRound1Assets();

            // Vệt sáng theo tay trên đường chém (7145/796) — chéo xuống theo lưỡi kiếm.
            GameObject slashTrail = CreateFx("Ulti2SlashTrail_VFX", Range(796, 6, 6), 30f, 4,
                rotationZ: -60f);
            // Hào quang vận khí (12015/840) bao quanh người: loop, SAU nhân vật, sống t88→t246 (2.64s).
            GameObject aura = CreateFx("Ulti2Aura_VFX", Range(840, 0, 25), 30f, -2,
                loop: true, life: 2.64f);
            // Kiếm trắng glow đính kiếm thật (12010/15-100): 1 frame giữ, sống hết charge loop (2.0s).
            GameObject swordGlow = CreateFx("Ulti2SwordGlow_VFX", new[] { "15-100" }, 30f, 3,
                loop: true, life: 2.0f);
            // Tia bùng cuối vận (12016/849): one-shot 23f×2t = 46t — spawn t185 KẾT ĐÚNG t231.
            GameObject rays = CreateFx("Ulti2PowerRays_VFX", Range(849, 0, 23), 30f, 4,
                rotationZ: 60f);
            // Sparkle cuối vận (12018/786): one-shot 6f×3t = 18t — spawn t213 kết đúng t231.
            GameObject spark = CreateFx("Ulti2ChargeSpark_VFX", Range(786, 0, 6), 20f, 4);
            // Kiếm trắng theo hành động vung (12011/15-101..107): LoopStart=5, fps 12 → loop 2 frame
            // ×5t = 10t ĐỒNG PHA với loop pose 813/815 của clip; sống t231→t358 (2.12s).
            GameObject swordLoop = CreateFx("Ulti2SwordLoop_VFX", Range(15, 101, 7), 12f, 3,
                loop: true, life: 2.12f, loopStart: 5);
            // Hào quang xoay theo đường kiếm vung xuống (user chốt): bản 840 chạy nhanh, nghiêng chéo.
            GameObject slashArc = CreateFx("Ulti2SlashArc_VFX", Range(840, 0, 25), 60f, 4,
                rotationZ: -55f);
            // Kiếm trắng tan (12012/15-106..115): one-shot fps 7 ≈ 86t — spawn đầu recovery t358,
            // hết frame ≈ hết đòn (t444/464).
            GameObject swordFade = CreateFx("Ulti2SwordFade_VFX", Range(15, 106, 10), 7f, 3);
            GameObject beamEnd = CreateBeamEndPrefab();

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
                    Debug.LogError($"[Ulti2Fx] {name}: thiếu AudioClip ({fx.sfxClips.Length}/{clips.Length})");
                fx.volume = vol;
                fx.scale = scale;
                EditorUtility.SetDirty(fx);
                result[name] = fx;
                return fx;
            }

            // Offset/scale bám số explod MUGEN (pos px/100, scale giữ nguyên) — CẦN TEST tinh chỉnh.
            Make("Ulti2_Announce", null, Vector2.zero, false, new[] { "Ulti2_Voice" }, 1f);
            Make("Ulti2_SlashTrail", slashTrail, new Vector2(0.3f, 0.35f), true, new[] { "S6000_0" }, 0.9f,
                 scale: 0.35f);
            Make("Ulti2_Aura", aura, Vector2.zero, true, new[] { "S404_2" }, 1f, scale: 0.5f);
            Make("Ulti2_SwordGlow", swordGlow, Vector2.zero, true, new[] { "S12005_0" }, 0.85f);
            Make("Ulti2_PowerRays", rays, new Vector2(-0.4f, 0.48f), true, new[] { "S12005_5" }, 0.95f,
                 scale: 0.25f);
            Make("Ulti2_PowerRise", null, Vector2.zero, false, new[] { "S12005_6" }, 0.95f);
            Make("Ulti2_BeamFire", null, Vector2.zero, false, new[] { "S12005_11" }, 1f);
            Make("Ulti2_BeamRoar", null, Vector2.zero, false, new[] { "S12005_12" }, 0.9f);
            Make("Ulti2_Sheathe", null, Vector2.zero, false, new[] { "S1005_10" }, 0.9f);
            result["Ulti2_SwordColumn"] = null;   // slot 33 TRỐNG (12017 bỏ — user chốt round 3).
            Make("Ulti2_ChargeSpark", spark, new Vector2(-0.55f, 0.56f), true, new string[0], 1f,
                 scale: 0.3f);
            Make("Ulti2_SwordLoop", swordLoop, Vector2.zero, true, new string[0], 1f);
            Make("Ulti2_SlashArc", slashArc, new Vector2(0.5f, 0.8f), true, new string[0], 1f,
                 scale: 0.5f);
            Make("Ulti2_SwordFade", swordFade, Vector2.zero, true, new string[0], 1f);
            Make("Ulti2_BeamEnd", beamEnd, Vector2.zero, false, new[] { "Super2_Boom" }, 1f);
            return result;
        }
    }
}
