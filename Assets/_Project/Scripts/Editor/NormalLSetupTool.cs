using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FA.Core;
using FA.Effects;

namespace FA.EditorTools
{
    /// <summary>
    /// TOOL DỰNG LẠI CHUỖI ĐÒN L (nL1/nL2/nL3) cho Emiya Archer THEO CHUẨN MUGEN (idempotent):
    /// state 200/201/202 = anim 200/201/202 (L1/L2 mỗi đòn 2 HIT, L3 = nhảy chém kết chuỗi).
    ///  1. Import settings sprite Attack_L (đã re-extract pad axis — sprite 142-149 pad thêm 31-32px
    ///     theo offset .air, KHÔNG cần nhảy vật lý) + VFX mới (768 vệt xoay, 725 bụi bước).
    ///  2. 3 clip 60fps đúng tick .air: sprite curve + event HitboxOn/Off (cửa sổ Clsn1), MoveImpulse
    ///     (VelSet), PlayVfx/PlaySfx (vệt chém 7130/7135, bụi 8005/8030, whoosh S205, voice S200/S5),
    ///     CancelWindowOpen/Close, KnockbackPoint (hit CUỐI — P16), RecoveryEnd.
    ///  3. Hitbox child MỚI theo Clsn1 union: index 3 = Hitbox_L (L1 cả 2 hit + L2 hit1),
    ///     4 = Hitbox_L2b (hit 2 của L2 — với dài 0.66u), 5 = Hitbox_L3 (vòng cung qua đầu).
    ///  4. EffectData slot CEP 38-49 (GIỮ slot 0-37 cũ — chỉ set arraySize + ghi index mới).
    ///  5. MoveData: motionImpulses theo VelSet (px/tick × 0.6 = u/s), hitstun/hitstop theo
    ///     HitDef (pausetime/ground.hittime), guardflag MA → hitsLow+hitsHigh (L3 HA → high-only).
    /// CLI: Unity.exe -batchmode -quit -executeMethod FA.EditorTools.NormalLSetupTool.Run
    /// Góc VFX: Unity z = -angle_MUGEN (y-down → y-up). CẦN TEST hướng vệt chém — sai thì đảo dấu.
    /// </summary>
    public static class NormalLSetupTool
    {
        private const string SpriteDir = "Assets/_Project/Art/Characters/Emiya Archer/Attack_L";
        private const string VfxDir = "Assets/_Project/Art/Characters/Emiya Archer/VFX";
        private const string AudioDir = "Assets/_Project/Audio/Emiya Archer";
        private const string AnimDir = "Assets/_Project/Animations/SampleFighter";
        private const string ControllerPath = AnimDir + "/SampleFighter.controller";
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/Emiya Archer.prefab";
        private const string DataDir = "Assets/_Project/Data/Emiya_Archer/Light_Attack";
        private const string EffectsDataDir = "Assets/_Project/Data/Emiya_Archer/Effects";
        private const string VfxPrefabDir = "Assets/_Project/Prefabs/VFX";
        private const string HitLandedChannelPath = "Assets/_Project/Data/OnHitLandedChannel.asset";

        private const float Fps = 60f;      // 1 tick MUGEN = 1 frame 60fps.
        private const int CepSlotBase = 38; // slot CEP tiếp theo (0-37 đã dùng — xem memory pipeline).

        // ---------------- frame data .air (tick tích lũy, sprite 12-xxx) ----------------
        private static readonly (int tick, int sprite)[] L1Frames =
        {
            (0, 100), (1, 101), (2, 102), (3, 103), (5, 105), (9, 106), (12, 107), (13, 108),
            (14, 109), (16, 110), (18, 111), (22, 112), (25, 113), (27, 114), (29, 115), (31, 116),
        };
        private const int L1Len = 33;

        private static readonly (int tick, int sprite)[] L2Frames =
        {
            (0, 117), (1, 118), (2, 119), (4, 120), (6, 121), (9, 122), (12, 123), (16, 124),
            (20, 125), (22, 126), (24, 127), (27, 128), (31, 129), (35, 130), (39, 131),
            (43, 132), (46, 133), (49, 134),
        };
        private const int L2Len = 52;

        private static readonly (int tick, int sprite)[] L3Frames =
        {
            (0, 135), (3, 136), (5, 137), (7, 138), (9, 139), (11, 140), (14, 141), (16, 142),
            (18, 143), (19, 144), (20, 145), (22, 146), (24, 147), (27, 148), (31, 149),
            (33, 150), (35, 151), (37, 152), (39, 153), (41, 154), (43, 155), (45, 156),
            (47, 157), (49, 158), (51, 159), (53, 160), (55, 161), (57, 162), (59, 163),
            (61, 164), (63, 165), (65, 136),
        };
        private const int L3Len = 67;

        [MenuItem("FA/Tools/Setup Archer Attack L theo MUGEN (anim + VFX/SFX)")]
        public static void Run()
        {
            ConfigureSpriteImports();
            Dictionary<string, EffectData> fx = CreateEffectAssets();
            AnimationClip l1 = CreateL1Clip();
            AnimationClip l2 = CreateL2Clip();
            AnimationClip l3 = CreateL3Clip();
            UpdateMoveData(l1, l2, l3);
            UpdateAnimatorStates(l1, l2, l3);
            WireCharacterPrefab(fx);
            AssetDatabase.SaveAssets();
            Debug.Log("[NormalLSetup] DONE — nL1/nL2 2-hit + nL3 kết chuỗi theo MUGEN 200/201/202, " +
                      "hitbox Clsn1 (idx 3/4/5), CEP slot 38-49.");
        }

        // ---------------------------------------------------------------- 1. Import settings
        private static void ConfigureSpriteImports()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteDir, VfxDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                if (imp == null) continue;
                bool isVfx = path.StartsWith(VfxDir);
                var settings = new TextureImporterSettings();
                imp.ReadTextureSettings(settings);
                settings.textureType = TextureImporterType.Sprite;
                settings.spriteMode = (int)SpriteImportMode.Single;
                settings.spritePixelsPerUnit = 100f;
                settings.spriteAlignment = (int)(isVfx ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
                settings.mipmapEnabled = false;
                imp.SetTextureSettings(settings);
                imp.SaveAndReimport();
            }
            AssetDatabase.Refresh();
        }

        // ---------------------------------------------------------------- 2. VFX prefab + EffectData
        /// <summary>Prefab VFX root RỖNG + child sprite bake rotation/scale/tint. Root để identity cho
        /// CharacterEffectPlayer flip theo facing (s.x root bị ghi đè dấu — bake trên CHILD mới giữ).
        /// internal: NormalMSetupTool (cùng assembly) tái dùng.</summary>
        internal static GameObject CreateTrailPrefab(string name, string[] frames, float fps,
                                                    Vector2 scale, float rotZ, Color tint, int order)
        {
            string path = $"{VfxPrefabDir}/{name}.prefab";
            var root = new GameObject(name);
            try
            {
                var child = new GameObject("Sprite");
                child.transform.SetParent(root.transform, false);
                child.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
                child.transform.localScale = new Vector3(scale.x, scale.y, 1f);
                var sr = child.AddComponent<SpriteRenderer>();
                sr.color = tint;
                sr.sortingOrder = order;
                Sprite[] sprites = frames.Select(n => Special3SetupTool.LoadSprite(VfxDir, n))
                                         .Where(s => s != null).ToArray();
                if (sprites.Length != frames.Length)
                    Debug.LogError($"[NormalLSetup] {name}: thiếu sprite VFX ({sprites.Length}/{frames.Length})");
                sr.sprite = sprites.Length > 0 ? sprites[0] : null;
                var flip = child.AddComponent<FlipbookOneShot>();
                var so = new SerializedObject(flip);
                SerializedProperty pFrames = so.FindProperty("frames");
                pFrames.arraySize = sprites.Length;
                for (int i = 0; i < sprites.Length; i++)
                    pFrames.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
                so.FindProperty("framesPerSecond").floatValue = fps;
                so.ApplyModifiedPropertiesWithoutUndo();
                // Root tự hủy theo child: child FlipbookOneShot chỉ Destroy child → thêm 1 flipbook
                // "rỗng sprite" trên root cùng nhịp để hủy CẢ CỤM khi xong.
                var rootSr = root.AddComponent<SpriteRenderer>();
                rootSr.sprite = null;
                var rootFlip = root.AddComponent<FlipbookOneShot>();
                var soR = new SerializedObject(rootFlip);
                SerializedProperty pF = soR.FindProperty("frames");
                pF.arraySize = sprites.Length;
                for (int i = 0; i < sprites.Length; i++)
                    pF.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
                soR.FindProperty("framesPerSecond").floatValue = fps;
                soR.ApplyModifiedPropertiesWithoutUndo();
                rootSr.enabled = false;   // chỉ làm đồng hồ hủy — không vẽ.
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static string[] Range(int group, int from, int count) =>
            Enumerable.Range(from, count).Select(i => $"{group}-{i}").ToArray();

        private static Dictionary<string, EffectData> CreateEffectAssets()
        {
            Color white = Color.white;
            // 7130 vệt chém (764, 2t/frame ≈ 33fps, explod scale .35,.23). GÓC (user test round 3):
            // sprite vệt 764/768 gốc hướng VỀ TRƯỚC ở 0° → bake z = 180 − angle_MUGEN (convert -angle
            // cũ làm vệt quay RA SAU lưng cả 2 hướng). Chỉnh theo mắt nếu còn lệch — CẦN TEST.
            GameObject trailA = CreateTrailPrefab("L_TrailA_VFX", Range(764, 0, 6), 33f,
                new Vector2(0.35f, 0.23f), -10f, white, 7);                       // L1 swing1/L2 swing1 (MUGEN 190)
            GameObject trailB = CreateTrailPrefab("L_TrailB_VFX", Range(764, 0, 6), 33f,
                new Vector2(0.35f, 0.23f), 20f, white, 7);                        // L1 swing2 (MUGEN 160)
            // L2 swing2 (MUGEN angle 5 + facing -1): mirror làm vệt quay ra sau → BỎ mirror, giữ tilt nhẹ.
            GameObject trailC = CreateTrailPrefab("L2_TrailC_VFX", Range(764, 0, 6), 33f,
                new Vector2(0.45f, 0.15f), -5f, white, 7);
            // 7135 vệt xoay L3 (768, 1t/frame = 60fps, explod .21, addalpha 120/256 → alpha mờ).
            GameObject spin = CreateTrailPrefab("L3_SpinTrail_VFX", Range(768, 0, 10), 60f,
                new Vector2(0.21f, 0.21f), -50f, new Color(1f, 1f, 1f, 0.55f), 7);  // MUGEN 230
            // 8005 bụi bước (725, 1t/frame, air scale .9 × explod .2,.3) — sau lưng nhân vật.
            GameObject stepDust = CreateTrailPrefab("L_StepDust_VFX", Range(725, 0, 16), 60f,
                new Vector2(0.18f, 0.27f), 0f, white, -3);
            // 8030 bụi nhảy (745, explod scale .3).
            GameObject jumpDust = CreateTrailPrefab("L3_JumpDust_VFX", Range(745, 0, 25), 60f,
                new Vector2(0.3f, 0.3f), 0f, white, 6);

            AudioClip Clip(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{n}.wav");
            var result = new Dictionary<string, EffectData>();
            EffectData Make(string name, GameObject vfx, Vector2 offset, bool attach,
                            string[] clips, float vol)
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
                    Debug.LogError($"[NormalLSetup] {name}: thiếu AudioClip ({fx.sfxClips.Length}/{clips.Length})");
                fx.volume = vol;
                fx.scale = 1f;
                EditorUtility.SetDirty(fx);
                result[name] = fx;
                return fx;
            }

            // Vị trí theo explod MUGEN (px/100, y đảo dấu). bindtime 9999 → attach theo người đánh.
            Make("L_Swing1", trailA, new Vector2(0.08f, 0.40f), true, new[] { "S205_0" }, 1f);   // slot 38
            // S205-1/S205-2 đã trích từ trước dưới tên S3_Whoosh_0/1 (extractor 1 tên/key) — tái dùng.
            Make("L_Swing2", trailB, new Vector2(0.01f, 0.34f), true, new[] { "S3_Whoosh_1" }, 1f); // 39 (205-2)
            Make("L_Voice1", null, Vector2.zero, false, new[] { "S200_0" }, 0.9f);               // 40
            Make("L_Voice2", null, Vector2.zero, false, new[] { "S200_2" }, 0.9f);               // 41
            Make("L_StepDust", stepDust, Vector2.zero, false, new string[0], 1f);                // 42
            Make("L2_Swing1", trailA, new Vector2(0.08f, 0.35f), true, new[] { "S3_Whoosh_0" }, 1f); // 43 (205-1)
            Make("L2_Swing2", trailC, new Vector2(0.01f, 0.43f), true, new[] { "S205_3" }, 1f);  // 44
            Make("L2_Voice1", null, Vector2.zero, false, new[] { "S200_1" }, 0.9f);              // 45
            Make("L2_Voice2", null, Vector2.zero, false, new[] { "S200_5" }, 0.9f);              // 46
            Make("L3_Voice", null, Vector2.zero, false, new[] { "S200_8" }, 0.9f);               // 47
            Make("L3_JumpDust", jumpDust, Vector2.zero, false, new[] { "S5_6" }, 0.9f);          // 48
            Make("L3_SpinSlash", spin, new Vector2(0.05f, 0.35f), true, new[] { "S5_2" }, 0.9f); // 49
            return result;
        }

        // ---------------------------------------------------------------- 3. Clips
        private static AnimationEvent Ev(int tick, string fn, int i = 0) =>
            new AnimationEvent { time = tick / Fps, functionName = fn, intParameter = i };

        private static AnimationClip BuildClip(string clipName, (int tick, int sprite)[] frames,
                                               int lengthTicks, AnimationEvent[] events)
        {
            var clip = new AnimationClip { frameRate = Fps };
            var keys = new List<ObjectReferenceKeyframe>();
            foreach ((int tick, int spriteIdx) in frames)
            {
                Sprite s = Special3SetupTool.LoadSprite(SpriteDir, $"12-{spriteIdx}");
                if (s == null) { Debug.LogError($"[NormalLSetup] Thiếu sprite 12-{spriteIdx}"); continue; }
                keys.Add(new ObjectReferenceKeyframe { time = tick / Fps, value = s });
            }
            // Giữ đúng độ dài anim MUGEN: key cuối lặp lại tại (length-0.5)/Fps để clip dài đủ tick.
            keys.Add(new ObjectReferenceKeyframe { time = (lengthTicks - 0.5f) / Fps, value = keys[keys.Count - 1].value });
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
            AnimationUtility.SetAnimationEvents(clip, events.OrderBy(e => e.time).ToArray());

            string path = $"{AnimDir}/{clipName}.anim";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static AnimationClip CreateL1Clip() => BuildClip("Attack_L1", L1Frames, L1Len, new[]
        {
            Ev(2, "MoveImpulse", 0),                                   // VelSet x=-1 (e3) — nhún lùi
            Ev(5, "MoveImpulse", 1),                                   // VelSet x=3 (e5) — bước tới chém
            Ev(5, "HitboxOn", 3), Ev(9, "HitboxOff", 3),               // hit 1 (Clsn1 e5)
            Ev(5, "PlayVfx", 38), Ev(5, "PlaySfx", 38),                // vệt chém + whoosh S205-0
            Ev(5, "PlaySfx", 40),                                      // voice S200-0
            Ev(5, "PlayVfx", 42),                                      // bụi bước 8005
            Ev(14, "MoveImpulse", 0), Ev(18, "MoveImpulse", 1),        // nhịp 2 (e9/e11)
            Ev(18, "HitboxOn", 3),                                     // hit 2 (Clsn1 e11)
            Ev(18, "PlayVfx", 39), Ev(18, "PlaySfx", 39),              // vệt 2 (góc 160) + whoosh S205-2
            Ev(18, "PlaySfx", 41), Ev(18, "PlayVfx", 42),              // voice S200-2 + bụi
            Ev(20, "CancelWindowOpen"),
            Ev(22, "HitboxOff", 3),
            Ev(27, "CancelWindowClose"),
            // KnockbackPoint SAU CancelWindowClose (fix vùng-chết: KP nằm trong window thì ấn nối sau
            // KP là địch đã văng trước khi mắt kế ra). Không nối tới hạn chót → mới văng.
            Ev(28, "KnockbackPoint"),
            Ev(32, "RecoveryEnd"),
        });

        private static AnimationClip CreateL2Clip() => BuildClip("Attack_L2", L2Frames, L2Len, new[]
        {
            // MUGEN velset SUSTAINED (x=4→2→3→4→2 suốt đòn) → DRIFT (impulse decay qua damping quá yếu,
            // attacker không đuổi kịp → hit 2 tầm-với-0.66u trượt — user test round 1).
            Ev(2, "MoveDriftOn"),                                      // lao tới suốt đòn (driftSpeed MoveData)
            Ev(6, "PlaySfx", 45),                                      // voice S200-1 (e5)
            Ev(9, "HitboxOn", 3), Ev(12, "HitboxOff", 3),              // hit 1 (Clsn1 e6)
            Ev(9, "PlayVfx", 43), Ev(9, "PlaySfx", 43),                // vệt chém + whoosh S205-1
            Ev(9, "PlayVfx", 42),                                      // bụi bước
            Ev(26, "PlayVfx", 44), Ev(26, "PlaySfx", 44),              // vệt ngang (góc 5) + whoosh S205-3
            Ev(27, "PlaySfx", 46),                                     // voice S200-5 (e12)
            Ev(27, "HitboxOn", 4),                                     // hit 2 — hitbox DÀI (Clsn1 e12, 0.66u)
            Ev(29, "CancelWindowOpen"),
            Ev(31, "HitboxOff", 4), Ev(31, "MoveDriftOff"),            // dừng lao sau active hit 2
            Ev(36, "CancelWindowClose"),
            Ev(37, "KnockbackPoint"),                                  // SAU close — fix vùng-chết nối đòn
            Ev(51, "RecoveryEnd"),
        });

        private static AnimationClip CreateL3Clip() => BuildClip("Attack_L3", L3Frames, L3Len, new[]
        {
            Ev(0, "MoveDriftOn"),                                      // MUGEN x=2 sustained + bay tới — DRIFT
            Ev(27, "MoveDriftOff"),                                    // dừng sau active (đáp đất)
            Ev(5, "PlaySfx", 47),                                      // voice S200-8 (e3)
            Ev(11, "PlayVfx", 48), Ev(11, "PlaySfx", 48),              // bụi nhảy 8030 + S5-6 (e6 — rời đất
                                                                       // VISUAL: sprite 142-149 đã pad +32px)
            Ev(18, "PlaySfx", 49),                                     // hự S5-2 (e9)
            Ev(19, "PlayVfx", 49),                                     // vệt xoay 7135 (e9+1t)
            Ev(22, "HitboxOn", 5), Ev(27, "HitboxOff", 5),             // hit duy nhất (Clsn1 e12-13)
            Ev(28, "KnockbackPoint"),                                  // ender chuỗi — văng khi hết hitstop
            Ev(66, "RecoveryEnd"),
        });                                                            // KHÔNG cancel window (không followUps)

        // ---------------------------------------------------------------- 4. MoveData
        private static void UpdateMoveData(AnimationClip l1, AnimationClip l2, AnimationClip l3)
        {
            void Update(string file, AnimationClip clip, List<Vector2> impulses, float drift,
                        int hitstun, int hitstop, float dmg, bool hitsLow)
            {
                var move = AssetDatabase.LoadAssetAtPath<MoveData>($"{DataDir}/{file}");
                if (move == null) { Debug.LogError($"[NormalLSetup] Không thấy {file}"); return; }
                move.clip = clip;
                move.motionImpulses = impulses;
                move.driftSpeed = drift;           // MUGEN velset sustained → drift (impulse decay quá yếu)
                move.hitstunFrames = hitstun;      // MUGEN ground.hittime
                move.hitstopFrames = hitstop;      // MUGEN pausetime
                move.baseDamagePercent = dmg;      // giữ thang balance hiện tại, tỉ lệ theo MUGEN 15/15/30
                move.hitsLow = hitsLow;            // guardflag MA = đỡ được cả đứng lẫn ngồi
                move.hitsHigh = true;
                EditorUtility.SetDirty(move);
            }
            // px/tick MUGEN × 0.6 = u/s (60 tick/s, PPU 100). CẦN TEST cảm giác bước/tầm đuổi.
            Update("EmiyaArcher_nL.asset", l1,
                new List<Vector2> { new Vector2(-0.6f, 0f), new Vector2(1.8f, 0f) }, 0f, 17, 6, 0.4f, true);
            Update("EmiyaArcher_nL2.asset", l2, new List<Vector2>(), 1.8f, 35, 6, 0.4f, true);
            // L3: guardflag HA (KHÔNG đỡ ngồi được — hitsLow=false), dmg gấp đôi, hitstop 14 (pausetime).
            Update("EmiyaArcher_nL3.asset", l3, new List<Vector2>(), 1.2f, 15, 14, 0.8f, false);
        }

        // ---------------------------------------------------------------- 5. Animator
        private static void UpdateAnimatorStates(AnimationClip l1, AnimationClip l2, AnimationClip l3)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) { Debug.LogError("[NormalLSetup] Không thấy controller"); return; }
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            void Point(string stateName, AnimationClip clip)
            {
                AnimatorState st = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == stateName);
                if (st == null) { Debug.LogError($"[NormalLSetup] Không thấy state {stateName}"); return; }
                st.motion = clip;
            }
            Point("Attack_L", l1);    // StateId 8
            Point("Attack_L2", l2);   // StateId 14
            Point("Attack_L3", l3);   // StateId 15
            EditorUtility.SetDirty(controller);
        }

        // ---------------------------------------------------------------- 6. Prefab: hitbox + CEP
        private static void WireCharacterPrefab(Dictionary<string, EffectData> fx)
        {
            var hitChannel = AssetDatabase.LoadAssetAtPath<HitEventChannelSO>(HitLandedChannelPath);
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                // Hitbox child theo Clsn1 MUGEN (union, px/100). Idempotent: tìm theo tên.
                FA.Combat.HitboxAuthoring MakeHitbox(string name, Vector2 offset, Vector2 size)
                {
                    Transform t = root.transform.Find(name);
                    GameObject go = t != null ? t.gameObject : new GameObject(name);
                    if (t == null) go.transform.SetParent(root.transform, false);
                    go.layer = LayerMask.NameToLayer(GameLayers.HitboxName);
                    var box = go.GetComponent<BoxCollider2D>();
                    if (box == null) box = go.AddComponent<BoxCollider2D>();
                    box.isTrigger = true;
                    box.offset = offset;
                    box.size = size;
                    box.enabled = false;   // bật bởi HitboxOn (Animation Event).
                    var hb = go.GetComponent<FA.Combat.HitboxAuthoring>();
                    if (hb == null) hb = go.AddComponent<FA.Combat.HitboxAuthoring>();
                    var so = new SerializedObject(hb);
                    so.FindProperty("hitChannel").objectReferenceValue = hitChannel;
                    so.FindProperty("box").objectReferenceValue = box;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    return hb;
                }
                // Union Clsn1: L1 hit1 (8..42,-70..-8) + hit2 (6..44,-64..-8) + L2 hit1 (7..48,-60..-6).
                var hbL = MakeHitbox("Hitbox_L", new Vector2(0.27f, 0.38f), new Vector2(0.42f, 0.64f));
                // L2 hit2: (14..66, -57..-24) — chém ngang với DÀI. Nới +0.06u (fix khó trúng round 1). CẦN TEST.
                var hbL2b = MakeHitbox("Hitbox_L2b", new Vector2(0.43f, 0.405f), new Vector2(0.58f, 0.33f));
                // L3: (-5..42, -63..-10) — vòng cung qua đầu. Nới tới +0.08u (fix khó trúng round 1). CẦN TEST.
                var hbL3 = MakeHitbox("Hitbox_L3", new Vector2(0.225f, 0.365f), new Vector2(0.55f, 0.53f));

                var hbCtrl = root.GetComponentInChildren<FA.Combat.HitboxController>();
                if (hbCtrl == null) { Debug.LogError("[NormalLSetup] Prefab thiếu HitboxController!"); }
                else
                {
                    var so = new SerializedObject(hbCtrl);
                    SerializedProperty arr = so.FindProperty("hitboxes");
                    if (arr.arraySize < 6) arr.arraySize = 6;   // GIỮ index 0-2 cũ.
                    arr.GetArrayElementAtIndex(3).objectReferenceValue = hbL;
                    arr.GetArrayElementAtIndex(4).objectReferenceValue = hbL2b;
                    arr.GetArrayElementAtIndex(5).objectReferenceValue = hbL3;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                // CEP slot 38-49 — GIỮ slot 0-37 (chỉ nới arraySize + ghi index mới).
                var cep = root.GetComponent<CharacterEffectPlayer>();
                if (cep == null) { Debug.LogError("[NormalLSetup] Prefab thiếu CharacterEffectPlayer!"); }
                else
                {
                    var so = new SerializedObject(cep);
                    SerializedProperty arr = so.FindProperty("effects");
                    string[] slots =
                    {
                        "L_Swing1", "L_Swing2", "L_Voice1", "L_Voice2", "L_StepDust",
                        "L2_Swing1", "L2_Swing2", "L2_Voice1", "L2_Voice2",
                        "L3_Voice", "L3_JumpDust", "L3_SpinSlash",
                    };
                    if (arr.arraySize < CepSlotBase + slots.Length) arr.arraySize = CepSlotBase + slots.Length;
                    for (int i = 0; i < slots.Length; i++)
                        arr.GetArrayElementAtIndex(CepSlotBase + i).objectReferenceValue = fx[slots[i]];
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
