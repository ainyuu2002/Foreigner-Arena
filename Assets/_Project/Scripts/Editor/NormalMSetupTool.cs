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
    /// TOOL DỰNG LẠI CHUỖI ĐÒN M (nM1-nM4) cho Emiya Archer THEO CHUẨN MUGEN (idempotent):
    /// state 300/301/302/303 = anim 300-303. M1 = chém lao (VelSet x=9!), M2 = bổ dọc overhead,
    /// M3 = lao đâm MULTI-HIT (persistent=2 → port 2 hit, 2 cặp HitboxOn/Off), M4 = ender charge
    /// với fall=1 → causesLaunch=true (LAUNCHER, văng ngay trên hit, KHÔNG cần KnockbackPoint).
    /// Theo trọn bài học port L (memory mugen-move-pipeline + phase16):
    ///  - Movement sustained → DRIFT (M3/M4 charge); burst tại-hit → impulse (M1/M2).
    ///  - Vệt chém: bake CHILD; góc facing=1 → z=180−angle, facing=−1 → z=−angle + BỎ mirror.
    ///  - Hitbox mới idx 6-9 (GIỮ 0-5); CEP slot 50-56 (GIỮ 0-49); tái dùng slot L: 41=voice S200_2,
    ///    45=voice S200_1, 42=bụi bước, 50 dùng lại cho whoosh S205_7 trong M3.
    /// CLI: Unity.exe -batchmode -quit -executeMethod FA.EditorTools.NormalMSetupTool.Run
    /// </summary>
    public static class NormalMSetupTool
    {
        private const string SpriteDir = "Assets/_Project/Art/Characters/Emiya Archer/Attack_M";
        private const string VfxDir = "Assets/_Project/Art/Characters/Emiya Archer/VFX";
        private const string AudioDir = "Assets/_Project/Audio/Emiya Archer";
        private const string AnimDir = "Assets/_Project/Animations/SampleFighter";
        private const string ControllerPath = AnimDir + "/SampleFighter.controller";
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/Emiya Archer.prefab";
        private const string DataDir = "Assets/_Project/Data/Emiya_Archer/Heavy_Attack";
        private const string EffectsDataDir = "Assets/_Project/Data/Emiya_Archer/Effects";
        private const string HitLandedChannelPath = "Assets/_Project/Data/OnHitLandedChannel.asset";

        private const float Fps = 60f;
        private const int CepSlotBase = 50;   // slot CEP tiếp theo sau L (0-49 đã dùng).
        private const int HitboxBase = 6;     // hitbox index tiếp theo sau L (0-5 đã dùng).

        // ---------------- frame data .air (tick tích lũy, sprite 12-xxx) ----------------
        private static readonly (int tick, int sprite)[] M1Frames =
        {
            (0, 166), (1, 167), (3, 168), (6, 169), (8, 170), (14, 171), (19, 172), (24, 167), (26, 166),
        };
        private const int M1Len = 28;

        private static readonly (int tick, int sprite)[] M2Frames =
        {
            (0, 173), (3, 174), (5, 175), (7, 176), (11, 177), (15, 178), (19, 179),
            (23, 180), (27, 181), (31, 182),
        };
        private const int M2Len = 35;

        private static readonly (int tick, int sprite)[] M3Frames =
        {
            (0, 183), (2, 184), (4, 185), (6, 186), (8, 187), (9, 189), (10, 190), (15, 191),
            (19, 192), (23, 193), (27, 194), (30, 195), (33, 196), (36, 197),
        };
        private const int M3Len = 39;

        private static readonly (int tick, int sprite)[] M4Frames =
        {
            (0, 197), (2, 198), (4, 199), (6, 200), (9, 201), (13, 202), (14, 203), (15, 204),
            (17, 205), (21, 206), (25, 207), (29, 208), (33, 209), (37, 210), (41, 211), (45, 212),
        };
        private const int M4Len = 49;

        [MenuItem("FA/Tools/Setup Archer Attack M theo MUGEN (anim + VFX/SFX)")]
        public static void Run()
        {
            ConfigureSpriteImports();
            Dictionary<string, EffectData> fx = CreateEffectAssets();
            AnimationClip m1 = CreateM1Clip();
            AnimationClip m2 = CreateM2Clip();
            AnimationClip m3 = CreateM3Clip();
            AnimationClip m4 = CreateM4Clip();
            UpdateMoveData(m1, m2, m3, m4);
            UpdateAnimatorStates(m1, m2, m3, m4);
            WireCharacterPrefab(fx);
            AssetDatabase.SaveAssets();
            Debug.Log("[NormalMSetup] DONE — nM1 lao/nM2 bổ dọc/nM3 2-hit charge/nM4 launcher theo " +
                      "MUGEN 300-303, hitbox idx 6-9, CEP slot 50-56.");
        }

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

        private static string[] Range(int group, int from, int count) =>
            Enumerable.Range(from, count).Select(i => $"{group}-{i}").ToArray();

        // ---------------------------------------------------------------- VFX prefab + EffectData
        private static Dictionary<string, EffectData> CreateEffectAssets()
        {
            Color white = Color.white;
            // 7137 vệt đâm/dọc (770, 2t/frame = 30fps). Góc: facing=1 → z = 180 − angle (bài học L).
            GameObject trailM1 = NormalLSetupTool.CreateTrailPrefab("M1_Trail_VFX", Range(770, 0, 7), 30f,
                new Vector2(0.25f, 0.4f), 185f, white, 7);                        // MUGEN angle -5
            GameObject trailM3 = NormalLSetupTool.CreateTrailPrefab("M3_Trail_VFX", Range(770, 0, 7), 30f,
                new Vector2(0.3f, 0.5f), 180f, white, 7);                         // MUGEN angle 0
            // 7130 vệt cung (764): M2 facing=-1 angle -20 → z=+20 BỎ mirror; M4 facing=1 angle 140 → z=40.
            GameObject trailM2 = NormalLSetupTool.CreateTrailPrefab("M2_Trail_VFX", Range(764, 0, 6), 33f,
                new Vector2(0.3f, 0.3f), 20f, white, 7);
            GameObject trailM4 = NormalLSetupTool.CreateTrailPrefab("M4_Trail_VFX", Range(764, 0, 6), 33f,
                new Vector2(0.35f, 0.3f), 40f, white, 7);

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
                    Debug.LogError($"[NormalMSetup] {name}: thiếu AudioClip ({fx.sfxClips.Length}/{clips.Length})");
                fx.volume = vol;
                fx.scale = 1f;
                EditorUtility.SetDirty(fx);
                result[name] = fx;
                return fx;
            }

            // Vị trí theo explod MUGEN (px/100, y đảo dấu).
            Make("M1_Swing", trailM1, new Vector2(0.30f, 0.30f), true, new[] { "S205_7" }, 1f);      // slot 50
            Make("M2_Swing", trailM2, new Vector2(0.10f, 0.33f), true, new[] { "S3_Whoosh_1" }, 1f); // 51 (205-2)
            Make("M3_Swing", trailM3, new Vector2(0.35f, 0.35f), true, new[] { "S205_6" }, 1f);      // 52
            Make("M4_Swing", trailM4, new Vector2(0.14f, 0.40f), true, new[] { "S205_4" }, 1f);      // 53
            Make("M4_WhooshUp", null, Vector2.zero, false, new[] { "S205_5" }, 1f);                  // 54
            Make("M_Voice16", null, Vector2.zero, false, new[] { "S200_16" }, 0.9f);                 // 55 (M3)
            Make("M_Voice15", null, Vector2.zero, false, new[] { "S200_15" }, 0.9f);                 // 56 (M4)
            return result;
        }

        // ---------------------------------------------------------------- Clips
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
                if (s == null) { Debug.LogError($"[NormalMSetup] Thiếu sprite 12-{spriteIdx}"); continue; }
                keys.Add(new ObjectReferenceKeyframe { time = tick / Fps, value = s });
            }
            keys.Add(new ObjectReferenceKeyframe { time = (lengthTicks - 0.5f) / Fps, value = keys[keys.Count - 1].value });
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
            AnimationUtility.SetAnimationEvents(clip, events.OrderBy(e => e.time).ToArray());

            string path = $"{AnimDir}/{clipName}.anim";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static AnimationClip CreateM1Clip() => BuildClip("Attack_M1", M1Frames, M1Len, new[]
        {
            Ev(1, "MoveImpulse", 0),                                   // VelSet x=-2 (e2) — nhún lùi
            Ev(6, "MoveImpulse", 1),                                   // x=4 (e4)
            Ev(6, "PlaySfx", 41), Ev(6, "PlayVfx", 42),                // voice S200-2 (slot L) + bụi
            Ev(8, "MoveImpulse", 2),                                   // x=9 (e5) — LAO chém
            Ev(8, "PlayVfx", 50), Ev(8, "PlaySfx", 50),                // vệt 7137 + whoosh S205-7
            Ev(8, "HitboxOn", 6), Ev(14, "HitboxOff", 6),              // hit (Clsn1 e5, với 0.59u)
            Ev(12, "CancelWindowOpen"),
            Ev(20, "CancelWindowClose"),
            Ev(21, "KnockbackPoint"),                                  // SAU close — fix vùng-chết nối đòn
            Ev(27, "RecoveryEnd"),
        });

        private static AnimationClip CreateM2Clip() => BuildClip("Attack_M2", M2Frames, M2Len, new[]
        {
            Ev(3, "MoveImpulse", 0), Ev(3, "PlaySfx", 45),             // x=3 (e2) + voice S200-1 (slot L)
            Ev(5, "MoveImpulse", 1),                                   // x=6 (e3)
            Ev(5, "PlayVfx", 51), Ev(5, "PlaySfx", 51),                // vệt 764 + whoosh S205-2
            Ev(5, "PlayVfx", 42),                                      // bụi đá tới (explod 8005 pos 25,0)
            Ev(7, "MoveImpulse", 1),                                   // x=6 (e4)
            Ev(7, "HitboxOn", 7), Ev(11, "HitboxOff", 7),              // hit bổ dọc (Clsn1 e4, cao 0.68u)
            Ev(10, "CancelWindowOpen"),
            Ev(18, "CancelWindowClose"),
            Ev(19, "KnockbackPoint"),                                  // SAU close — fix vùng-chết nối đòn
            Ev(34, "RecoveryEnd"),
        });

        private static AnimationClip CreateM3Clip() => BuildClip("Attack_M3", M3Frames, M3Len, new[]
        {
            Ev(2, "MoveImpulse", 0),                                   // x=-2 (e2) — nhún lùi lấy đà
            Ev(8, "MoveDriftOn"), Ev(8, "PlayVfx", 42),                // Veladd charge (e5..e8) → drift + bụi
            Ev(9, "PlaySfx", 55),                                      // voice S200-16 (e6)
            Ev(10, "PlayVfx", 52), Ev(10, "PlaySfx", 52),              // vệt 7137 + whoosh S205-6 (e6+1t)
            Ev(10, "HitboxOn", 8), Ev(12, "HitboxOff", 8),             // hit 1 (Clsn1 e7 t10-15, chia 2 cửa sổ
            Ev(13, "HitboxOn", 8), Ev(15, "HitboxOff", 8),             //  — MUGEN persistent=2 multi-hit)
            Ev(12, "PlaySfx", 50),                                     // whoosh S205-7 (e7+2t, tái dùng slot 50)
            Ev(15, "MoveDriftOff"),
            Ev(16, "CancelWindowOpen"),                                // SAU hit 2 off — ấn sớm không cắt hit 2
            Ev(24, "CancelWindowClose"),
            Ev(25, "KnockbackPoint"),                                  // SAU close — fix vùng-chết nối đòn
            Ev(38, "RecoveryEnd"),
        });

        private static AnimationClip CreateM4Clip() => BuildClip("Attack_M4", M4Frames, M4Len, new[]
        {
            Ev(2, "MoveImpulse", 0),                                   // x=-2 (e2) — thu người lấy đà
            Ev(9, "PlaySfx", 56),                                      // voice S200-15 (e5)
            Ev(13, "MoveDriftOn"),                                     // Veladd +3/tick (e6..e9) → drift charge
            Ev(14, "PlayVfx", 53), Ev(14, "PlaySfx", 53),              // vệt 764 (z40) + whoosh S205-4 (e7)
            Ev(15, "PlaySfx", 54),                                     // whoosh dâng S205-5 (e7+1t)
            Ev(17, "HitboxOn", 9), Ev(21, "HitboxOff", 9),             // hit (Clsn1 e9)
            Ev(21, "MoveDriftOff"),
            Ev(48, "RecoveryEnd"),
        });   // causesLaunch=true → văng NGAY trên hit, KHÔNG cần KnockbackPoint; không followUps → no cancel.

        // ---------------------------------------------------------------- MoveData
        private static void UpdateMoveData(AnimationClip m1, AnimationClip m2, AnimationClip m3, AnimationClip m4)
        {
            void Update(string file, string moveId, AnimationClip clip, List<Vector2> impulses, float drift,
                        int hitstun, int hitstop, float dmg, bool hitsLow, bool launch)
            {
                var move = AssetDatabase.LoadAssetAtPath<MoveData>($"{DataDir}/{file}");
                if (move == null) { Debug.LogError($"[NormalMSetup] Không thấy {file}"); return; }
                move.moveId = moveId;
                move.clip = clip;
                move.motionImpulses = impulses;
                move.driftSpeed = drift;
                move.hitstunFrames = hitstun;      // MUGEN ground.hittime
                move.hitstopFrames = hitstop;      // MUGEN pausetime
                move.baseDamagePercent = dmg;      // thang L: 15 MUGEN = 0.4 (M2 25→0.7, M4 30→0.8). CẦN TEST.
                move.hitsLow = hitsLow;
                move.hitsHigh = true;
                move.causesLaunch = launch;
                EditorUtility.SetDirty(move);
            }
            // px/tick × 0.6 = u/s. M1 lao x=9 = 5.4 u/s NGAY lúc hit; M3/M4 Veladd charge → drift.
            Update("EmiyaArcher_nM.asset", "nM1", m1,
                new List<Vector2> { new Vector2(-1.2f, 0f), new Vector2(2.4f, 0f), new Vector2(5.4f, 0f) },
                0f, 20, 6, 0.4f, true, false);
            Update("EmiyaArcher_nM2.asset", "nM2", m2,
                new List<Vector2> { new Vector2(1.8f, 0f), new Vector2(3.6f, 0f) },
                0f, 22, 9, 0.7f, true, false);
            Update("EmiyaArcher_nM3.asset", "nM3", m3,
                new List<Vector2> { new Vector2(-1.2f, 0f) }, 3.0f, 30, 6, 0.4f, true, false);
            // M4: MUGEN fall=1 (guardflag HA — không đỡ ngồi) → LAUNCHER văng ngay (mở juggle P7).
            Update("EmiyaArcher_nM4.asset", "nM4", m4,
                new List<Vector2> { new Vector2(-1.2f, 0f) }, 3.2f, 25, 14, 0.8f, false, true);
        }

        // ---------------------------------------------------------------- Animator
        private static void UpdateAnimatorStates(AnimationClip m1, AnimationClip m2, AnimationClip m3, AnimationClip m4)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) { Debug.LogError("[NormalMSetup] Không thấy controller"); return; }
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            void Point(string stateName, AnimationClip clip)
            {
                AnimatorState st = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == stateName);
                if (st == null) { Debug.LogError($"[NormalMSetup] Không thấy state {stateName}"); return; }
                st.motion = clip;
            }
            Point("Attack_M", m1);    // StateId 9
            Point("Attack_M2", m2);   // StateId 32
            Point("Attack_M3", m3);   // StateId 33
            Point("Attack_M4", m4);   // StateId 34
            EditorUtility.SetDirty(controller);
        }

        // ---------------------------------------------------------------- Prefab: hitbox + CEP
        private static void WireCharacterPrefab(Dictionary<string, EffectData> fx)
        {
            var hitChannel = AssetDatabase.LoadAssetAtPath<HitEventChannelSO>(HitLandedChannelPath);
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
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
                    box.enabled = false;
                    var hb = go.GetComponent<FA.Combat.HitboxAuthoring>();
                    if (hb == null) hb = go.AddComponent<FA.Combat.HitboxAuthoring>();
                    var so = new SerializedObject(hb);
                    so.FindProperty("hitChannel").objectReferenceValue = hitChannel;
                    so.FindProperty("box").objectReferenceValue = box;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    return hb;
                }
                // Clsn1 union + nới ~0.06u về trước (bài học L). CẦN TEST.
                var hbM1 = MakeHitbox("Hitbox_M1", new Vector2(0.385f, 0.285f), new Vector2(0.53f, 0.25f));
                var hbM2 = MakeHitbox("Hitbox_M2", new Vector2(0.30f, 0.395f), new Vector2(0.54f, 0.57f));
                var hbM3 = MakeHitbox("Hitbox_M3", new Vector2(0.46f, 0.295f), new Vector2(0.50f, 0.33f));
                var hbM4 = MakeHitbox("Hitbox_M4", new Vector2(0.395f, 0.28f), new Vector2(0.47f, 0.54f));

                var hbCtrl = root.GetComponentInChildren<FA.Combat.HitboxController>();
                if (hbCtrl == null) { Debug.LogError("[NormalMSetup] Prefab thiếu HitboxController!"); }
                else
                {
                    var so = new SerializedObject(hbCtrl);
                    SerializedProperty arr = so.FindProperty("hitboxes");
                    if (arr.arraySize < HitboxBase + 4) arr.arraySize = HitboxBase + 4;   // GIỮ 0-5.
                    arr.GetArrayElementAtIndex(HitboxBase + 0).objectReferenceValue = hbM1;
                    arr.GetArrayElementAtIndex(HitboxBase + 1).objectReferenceValue = hbM2;
                    arr.GetArrayElementAtIndex(HitboxBase + 2).objectReferenceValue = hbM3;
                    arr.GetArrayElementAtIndex(HitboxBase + 3).objectReferenceValue = hbM4;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                var cep = root.GetComponent<CharacterEffectPlayer>();
                if (cep == null) { Debug.LogError("[NormalMSetup] Prefab thiếu CharacterEffectPlayer!"); }
                else
                {
                    var so = new SerializedObject(cep);
                    SerializedProperty arr = so.FindProperty("effects");
                    string[] slots = { "M1_Swing", "M2_Swing", "M3_Swing", "M4_Swing",
                                       "M4_WhooshUp", "M_Voice16", "M_Voice15" };
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
