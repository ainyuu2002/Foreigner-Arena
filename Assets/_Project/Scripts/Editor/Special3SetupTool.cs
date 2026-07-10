using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FA.Core;
using FA.Effects;
using FA.Specials;

namespace FA.EditorTools
{
    /// <summary>
    /// TOOL DỰNG SPECIAL 3 cho Emiya Archer (idempotent — chạy lại an toàn) từ dữ liệu MUGEN đã trích
    /// (state 1200: ném 2 kiếm bay vòng cung — helper 1205/1206, vx=5 vy=-7 gravity 0.4/tick):
    ///  1. Import settings: sprite Special3 (Single, PPU 100, pivot Bottom — 12-489 kiếm pivot Center
    ///     để xoay quanh tâm) + VFX (Center).
    ///  2. Clip Special_3.anim từ frame data .air anim 1200 (60fps = 60tick MUGEN) + Animation Event:
    ///     PlaySfx/PlayVfx (cast/voice/whoosh/glow), SpawnProjectile(0) tại tick 31 &amp; 55 (elem 7/13),
    ///     RecoveryEnd tick 90.
    ///  3. VFX prefab (FlipbookOneShot): CastRing (6100), HandGlow (764), HitBurst (794).
    ///  4. Sword prefab (Rigidbody2D gravityScale 1.47 + ProjectileLaunchSetup vy/spin + Projectile +
    ///     child Hitbox moveOverride=Special_3).
    ///  5. EffectData asset ×6 + MoveData Special_3 (QCF+H) + entry MoveSet + Animator state
    ///     Special_3 (StateId==35, KHÔNG Can-Transition-To-Self — gotcha đã biết).
    ///  6. Wire prefab: AudioSource + CharacterEffectPlayer + HitEffectPlayer + Muzzle +
    ///     ProjectileSpawner (prefab kiếm, speed 3, kênh hit).
    /// CLI: Unity.exe -batchmode -quit -executeMethod FA.EditorTools.Special3SetupTool.Run
    /// Gotcha SetDirty (memory P12): mọi asset rời sửa xong phải SetDirty + SaveAssets.
    /// </summary>
    public static class Special3SetupTool
    {
        private const string SpriteDir = "Assets/_Project/Art/Characters/Emiya Archer/Special3";
        private const string VfxDir = "Assets/_Project/Art/Characters/Emiya Archer/VFX";
        private const string AudioDir = "Assets/_Project/Audio/Emiya Archer";
        private const string ClipPath = "Assets/_Project/Animations/SampleFighter/Special_3.anim";
        private const string ControllerPath = "Assets/_Project/Animations/SampleFighter/SampleFighter.controller";
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/Emiya Archer.prefab";
        private const string MoveDataPath = "Assets/_Project/Data/Emiya_Archer/Special_Move/Special_3.asset";
        private const string EnderDataPath = "Assets/_Project/Data/Emiya_Archer/Special_Move/Special_3_Ender.asset";
        private const string MoveSetPath = "Assets/_Project/Data/Emiya_Archer/Special_Move/MoveSet.asset";
        private const string EffectsDataDir = "Assets/_Project/Data/Emiya_Archer/Effects";
        private const string VfxPrefabDir = "Assets/_Project/Prefabs/VFX";
        private const string ProjectilePrefabDir = "Assets/_Project/Prefabs/Projectiles";
        private const string SwordPrefabPath = ProjectilePrefabDir + "/ThrownSword.prefab";
        private const string SwordEnderPrefabPath = ProjectilePrefabDir + "/ThrownSword_Ender.prefab";
        private const string HitLandedChannelPath = "Assets/_Project/Data/OnHitLandedChannel.asset";
        private const string HitConfirmedChannelPath = "Assets/_Project/Data/OnHitConfirmedChannel.asset";
        private const string HitBlockedChannelPath = "Assets/_Project/Data/OnHitBlockedChannel.asset";

        private const int StateId = 35;          // animatorStateId mới (8-34 đã dùng).
        private const float Fps = 60f;           // 1 tick MUGEN = 1 frame 60fps.

        // Frame data .air anim 1200 MỞ RỘNG 3 NÉM: MUGEN gốc ném 2 kiếm (elem 7/13) rồi ChangeAnim
        // RESTART trong khi kiếm còn bay → thực tế ra thêm kiếm nữa (số lượng biến thiên). Port cố
        // định 3 ném cho deterministic: lặp lại chu kỳ tay ném (12-455..460) 1 lần trước recovery.
        // Ném tại tick 31 / 55 / 80 (2 carry + 1 ender).
        private static readonly (int tick, string sprite)[] Frames =
        {
            (0, "0-0"), (12, "12-450"), (17, "12-451"), (24, "12-452"), (28, "12-453"),
            (31, "12-454"), (35, "12-455"), (39, "12-456"), (43, "12-457"), (47, "12-458"),
            (51, "12-459"), (55, "12-460"),
            // chu kỳ ném thứ 3 (lặp elem 8-13)
            (60, "12-455"), (64, "12-456"), (68, "12-457"), (72, "12-458"), (76, "12-459"), (80, "12-460"),
            // recovery (elem 14-21 gốc)
            (85, "12-461"), (89, "12-462"), (93, "12-463"), (97, "12-464"), (101, "12-465"),
            (105, "12-466"), (109, "12-467"), (113, "12-484"),
        };

        [MenuItem("FA/Tools/Setup Archer Special 3 (swords + VFX/SFX)")]
        public static void Run()
        {
            EnsureFolders();
            ConfigureSpriteImports();
            AnimationClip clip = CreateClip();
            MoveData move = CreateMoveData(clip);
            MoveData ender = CreateEnderMoveData(clip);
            GameObject swordCarry = CreateSwordPrefab(move, SwordPrefabPath);
            GameObject swordEnder = CreateSwordPrefab(ender, SwordEnderPrefabPath);
            Dictionary<string, EffectData> fx = CreateEffectAssets();
            AddMoveSetEntry(move);
            AddAnimatorState(clip);
            AssignMoveEffects(fx);
            WireCharacterPrefab(swordCarry, swordEnder, fx);
            AssetDatabase.SaveAssets();
            Debug.Log("[Special3Setup] DONE — 3 kiếm carry/ender + hit effect PER-MOVE (spark/sound theo MUGEN).");
        }

        private static void EnsureFolders()
        {
            foreach (string dir in new[] { EffectsDataDir, VfxPrefabDir, ProjectilePrefabDir })
            {
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    string parent = dir.Substring(0, dir.LastIndexOf('/'));
                    AssetDatabase.CreateFolder(parent, dir.Substring(dir.LastIndexOf('/') + 1));
                }
            }
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
                bool isSword = path.EndsWith("12-489.png");
                var settings = new TextureImporterSettings();
                imp.ReadTextureSettings(settings);
                settings.textureType = TextureImporterType.Sprite;
                settings.spriteMode = (int)SpriteImportMode.Single;   // MUGEN: 1 file = 1 sprite, KHÔNG sprite sheet.
                settings.spritePixelsPerUnit = 100f;
                // Kiếm xoay quanh TÂM + VFX nổ từ tâm → Center; sprite thân nhân vật → Bottom (theo convention).
                settings.spriteAlignment = (int)((isVfx || isSword) ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
                settings.mipmapEnabled = false;
                imp.SetTextureSettings(settings);
                imp.SaveAndReimport();
            }
            AssetDatabase.Refresh();
        }

        // internal: Super1SetupTool (cùng assembly) tái dùng.
        internal static Sprite LoadSprite(string dir, string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{name}.png");

        // ---------------------------------------------------------------- 2. Animation Clip
        private static AnimationClip CreateClip()
        {
            var clip = new AnimationClip { frameRate = Fps };

            var keys = new List<ObjectReferenceKeyframe>();
            foreach ((int tick, string spriteName) in Frames)
            {
                Sprite s = LoadSprite(SpriteDir, spriteName);
                if (s == null) { Debug.LogError($"[Special3Setup] Thiếu sprite {spriteName}"); continue; }
                keys.Add(new ObjectReferenceKeyframe { time = tick / Fps, value = s });
            }
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());

            AnimationEvent Ev(int tick, string fn, int i = 0) =>
                new AnimationEvent { time = tick / Fps, functionName = fn, intParameter = i };

            var events = new[]
            {
                Ev(0, "PlaySfx", 0),            // tiếng kích chiêu (S6100-0)
                Ev(0, "PlaySfx", 1),            // voice line (random 3 clip — MUGEN S1200 random%3)
                Ev(0, "PlayVfx", 0),            // vòng flash kích chiêu (anim 6100, pos 0,-30px)
                Ev(28, "PlayVfx", 1),           // glow tay (anim 7130, elem 6)
                Ev(31, "SpawnProjectile", 0),   // kiếm 1 — CARRY (không văng, chỉ hitstun)
                Ev(31, "PlaySfx", 2),           // whoosh (S205-1/2 random)
                Ev(51, "PlayVfx", 1),           // glow tay lần 2 (elem 12)
                Ev(55, "SpawnProjectile", 0),   // kiếm 2 — CARRY
                Ev(55, "PlaySfx", 2),
                Ev(76, "PlayVfx", 1),           // glow tay lần 3
                Ev(80, "SpawnProjectile", 1),   // kiếm 3 — ENDER (variant 1: văng khi trúng)
                Ev(80, "PlaySfx", 2),
                Ev(117, "RecoveryEnd"),         // hết đòn (~1.95s)
            };
            AnimationUtility.SetAnimationEvents(clip, events.OrderBy(e => e.time).ToArray());

            AssetDatabase.DeleteAsset(ClipPath);
            AssetDatabase.CreateAsset(clip, ClipPath);
            return clip;
        }

        // ---------------------------------------------------------------- 3. MoveData + MoveSet
        private static MoveData CreateMoveData(AnimationClip clip)
        {
            var move = AssetDatabase.LoadAssetAtPath<MoveData>(MoveDataPath);
            if (move == null)
            {
                move = ScriptableObject.CreateInstance<MoveData>();
                AssetDatabase.CreateAsset(move, MoveDataPath);
            }
            move.moveId = "special3";
            move.category = MoveCategory.Special;
            move.animatorStateId = StateId;
            move.clip = clip;
            move.motion = MotionInput.QCF;          // MUGEN: ~D,DF,F,b — slot QCF+M đã dùng → QCF+H.
            move.button = InputButton.H;
            move.manaCostUnits = 1;                 // MUGEN power>=1000. CẦN TEST.
            move.baseDamagePercent = 3f;            // MUGEN damage 13/kiếm (life 1000). CẦN TEST.
            move.launchAngle = 50f;
            move.baseKnockback = 8f;
            move.knockbackGrowth = 0.1f;
            move.isSpike = false;
            // CARRY: kiếm 1-2 KHÔNG văng (causesLaunch=false → KnockbackResolver chỉ lưu pending) để
            // các kiếm sau còn trúng — knockback dồn cho kiếm ENDER (pattern carry/ender như Special_2).
            move.causesLaunch = false;
            move.role = MoveRole.Projectile;
            move.hitsLow = false;
            move.hitsHigh = true;
            move.hitstunFrames = 30;                // đủ dài giữ combo tới kiếm kế (gap ném 25 tick). CẦN TEST.
            move.hitstopFrames = 8;                 // MUGEN pausetime 6. CẦN TEST.
            move.motionImpulses = new List<Vector2>();   // MUGEN velset 0,0 — đứng yên khi ném.
            move.driftSpeed = 0f;
            EditorUtility.SetDirty(move);
            return move;
        }

        /// <summary>Kiếm CUỐI (ender): trúng là văng NGAY (causesLaunch — projectile không dựa được vào
        /// Animation Event KnockbackPoint vì thời điểm trúng phụ thuộc khoảng cách bay, có thể SAU khi
        /// clip nhân vật đã kết thúc).</summary>
        private static MoveData CreateEnderMoveData(AnimationClip clip)
        {
            var move = AssetDatabase.LoadAssetAtPath<MoveData>(EnderDataPath);
            if (move == null)
            {
                move = ScriptableObject.CreateInstance<MoveData>();
                AssetDatabase.CreateAsset(move, EnderDataPath);
            }
            move.moveId = "special3_ender";
            move.category = MoveCategory.Special;
            move.animatorStateId = StateId;
            move.clip = clip;
            move.motion = MotionInput.None;         // không gọi trực tiếp — chỉ làm moveOverride hitbox kiếm 3.
            move.button = InputButton.H;
            move.manaCostUnits = 0;
            move.baseDamagePercent = 4f;
            move.launchAngle = 50f;
            move.baseKnockback = 8f;
            move.knockbackGrowth = 0.1f;
            move.isSpike = false;
            move.causesLaunch = true;
            move.role = MoveRole.Projectile;
            move.hitsLow = false;
            move.hitsHigh = true;
            move.hitstunFrames = 20;
            move.hitstopFrames = 8;
            move.motionImpulses = new List<Vector2>();
            move.driftSpeed = 0f;
            EditorUtility.SetDirty(move);
            return move;
        }

        private static void AddMoveSetEntry(MoveData move)
        {
            var moveSet = AssetDatabase.LoadAssetAtPath<MoveSetData>(MoveSetPath);
            if (moveSet == null) { Debug.LogError("[Special3Setup] Không thấy MoveSet.asset"); return; }

            var so = new SerializedObject(moveSet);
            SerializedProperty entries = so.FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("move").objectReferenceValue == move)
                    return;   // idempotent — đã có entry.
            }
            entries.arraySize++;
            SerializedProperty e = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            e.FindPropertyRelative("motion").intValue = (int)MotionInput.QCF;
            e.FindPropertyRelative("button").intValue = (int)InputButton.H;
            e.FindPropertyRelative("move").objectReferenceValue = move;
            e.FindPropertyRelative("charged").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(moveSet);
        }

        // ---------------------------------------------------------------- 4. Animator state
        private static void AddAnimatorState(AnimationClip clip)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) { Debug.LogError("[Special3Setup] Không thấy controller"); return; }

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Special_3");
            if (state == null) state = sm.AddState("Special_3");
            state.motion = clip;
            state.writeDefaultValues = true;

            // Transition Any State → Special_3 khi StateId==35. BỎ Can-Transition-To-Self (gotcha:
            // để bật thì mỗi frame StateId==35 re-enter → kẹt frame 0).
            AnimatorStateTransition tr = sm.anyStateTransitions.FirstOrDefault(t => t.destinationState == state);
            if (tr == null) tr = sm.AddAnyStateTransition(state);
            tr.hasExitTime = false;
            tr.hasFixedDuration = true;
            tr.duration = 0f;
            tr.canTransitionToSelf = false;
            tr.conditions = new[]
            {
                new AnimatorCondition { mode = AnimatorConditionMode.Equals, parameter = "StateId", threshold = StateId }
            };
            EditorUtility.SetDirty(controller);
        }

        // ---------------------------------------------------------------- 5. VFX prefabs + EffectData
        internal static GameObject CreateFlipbookPrefab(string name, string[] spriteNames, float fps,
                                                        float scale, int sortingOrder)
        {
            string path = $"{VfxPrefabDir}/{name}.prefab";
            var go = new GameObject(name);
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                Sprite[] sprites = spriteNames.Select(n => LoadSprite(VfxDir, n)).Where(s => s != null).ToArray();
                if (sprites.Length != spriteNames.Length)
                    Debug.LogError($"[Special3Setup] {name}: thiếu sprite VFX ({sprites.Length}/{spriteNames.Length})");
                sr.sprite = sprites.Length > 0 ? sprites[0] : null;
                sr.sortingOrder = sortingOrder;

                var flip = go.AddComponent<FlipbookOneShot>();
                var so = new SerializedObject(flip);
                SerializedProperty pFrames = so.FindProperty("frames");
                pFrames.arraySize = sprites.Length;
                for (int i = 0; i < sprites.Length; i++)
                    pFrames.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
                so.FindProperty("framesPerSecond").floatValue = fps;
                so.ApplyModifiedPropertiesWithoutUndo();

                go.transform.localScale = new Vector3(scale, scale, 1f);
                return PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally { Object.DestroyImmediate(go); }
        }

        private static Dictionary<string, EffectData> CreateEffectAssets()
        {
            // Scale theo explod MUGEN: 6100 ring .5, 764 glow .3, 794 burst .26-.27.
            GameObject ring = CreateFlipbookPrefab("S3_CastRing_VFX",
                new[] { "6100-0", "6100-2", "6100-3", "6100-4", "6100-5", "6100-6", "6100-7", "6100-8", "6100-9" },
                30f, 0.5f, 10);
            GameObject glow = CreateFlipbookPrefab("S3_HandGlow_VFX",
                new[] { "764-0", "764-1", "764-2", "764-3", "764-4", "764-5" }, 33f, 0.3f, 11);
            // SPARK theo MUGEN state 6000 (helper spark mọi đòn spawn): var(20)=0 default = anim
            // 7025+7030 (sprite 752+766, 2 LỚP chồng nhau), var(20)=1 (đòn kết chuỗi anim 202) =
            // 7020+7024 (750+754), guard (state 6005) = 7160 (1120). Explod scale .23 × fvar(31)
            // theo độ nặng (≈1/1.2/1.5) → prefab scale .23, EffectData.scale nhân thêm.
            // Dọn bản tạm cũ. (752-15/754-4 không tồn tại trong SFF — MUGEN tự bỏ, ta dùng phần có.)
            AssetDatabase.DeleteAsset($"{VfxPrefabDir}/S3_HitBurst_VFX.prefab");
            AssetDatabase.DeleteAsset($"{EffectsDataDir}/Hit_Sword.asset");
            AssetDatabase.DeleteAsset($"{EffectsDataDir}/Hit_Spark.asset");
            GameObject spark = CreateLayeredSparkPrefab("HitSpark_VFX",
                Enumerable.Range(0, 15).Select(i => $"752-{i}").ToArray(), 42f,
                new[] { "766-1", "766-1", "766-1", "766-2", "766-3", "766-4" }, 20f, 0.23f);
            GameObject chainSpark = CreateLayeredSparkPrefab("ChainSpark_VFX",
                new[] { "750-0", "750-0", "750-1", "750-1", "750-2", "750-2", "750-3", "750-3", "750-4" }, 28f,
                new[] { "754-0", "754-0", "754-1", "754-1", "754-2", "754-2", "754-3", "754-3" }, 36f, 0.23f);
            GameObject guardSpark = CreateFlipbookPrefab("GuardSpark_VFX",
                Enumerable.Range(0, 8).Select(i => $"1120-{i}").ToArray(), 60f, 0.23f, 11);

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
                    Debug.LogError($"[Special3Setup] {name}: thiếu AudioClip ({fx.sfxClips.Length}/{clips.Length})");
                fx.volume = vol;
                EditorUtility.SetDirty(fx);
                result[name] = fx;
                return fx;
            }

            // Vị trí theo explod MUGEN (px/100): ring 0,-30 → (0, 0.30); glow tay 10,-33 → (0.10, 0.33).
            Make("S3_CastFlash", ring, new Vector2(0f, 0.30f), false, new[] { "S3_Cast" }, 1f);
            Make("S3_Voice", null, Vector2.zero, false, new[] { "S3_Voice_0", "S3_Voice_1", "S3_Voice_2" }, 0.9f);
            Make("S3_Whoosh", null, Vector2.zero, false, new[] { "S3_Whoosh_0", "S3_Whoosh_1" }, 1f);
            Make("S3_HandGlow", glow, new Vector2(0.10f, 0.33f), true, new string[0], 1f);

            // Bộ hit effect PER-MOVE (spark scale + hitsound theo HitDef MUGEN từng đòn).
            EffectData MakeHit(string name, GameObject vfx, float scale, string sfx)
            {
                EffectData fx = Make(name, vfx, Vector2.zero, false, new[] { sfx }, 1f);
                fx.scale = scale;
                EditorUtility.SetDirty(fx);
                return fx;
            }
            MakeHit("Hit_Light", spark, 1.0f, "S7_30");        // nL (MUGEN 200: S7,30)
            MakeHit("Hit_Medium1", spark, 1.2f, "S7_31");      // nM1/nM3 (300/302: S7,31)
            MakeHit("Hit_Medium2", spark, 1.2f, "S7_32");      // nL2/nM2 (201/301: S7,32)
            MakeHit("Hit_Heavy", spark, 1.5f, "S7_33");        // nH/kiếm S3 (405/1205: S7,33)
            MakeHit("Hit_Heavy2", spark, 1.5f, "S7_35");       // nM4/ender (303/1000: S7,35)
            MakeHit("Hit_Chain", chainSpark, 1.2f, "S7_07");   // nL3 kết chuỗi (202: S7,7 + spark 7020/7024)
            EffectData guard = Make("Block_Guard", guardSpark, Vector2.zero, false, new[] { "Guard" }, 1f);
            guard.scale = 1f;
            EditorUtility.SetDirty(guard);
            return result;
        }

        /// <summary>Spark 2 LỚP như MUGEN (2 explod chồng): root = lớp A (dài hơn, hủy cả cụm khi xong),
        /// child = lớp B. Cả hai FlipbookOneShot tự chạy; child hết trước tự Destroy riêng.</summary>
        internal static GameObject CreateLayeredSparkPrefab(string name, string[] framesA, float fpsA,
                                                            string[] framesB, float fpsB, float scale)
        {
            string path = $"{VfxPrefabDir}/{name}.prefab";
            GameObject layerB = CreateFlipbookPrefab($"{name}_Layer2", framesB, fpsB, 1f, 10);
            var go = new GameObject(name);
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                Sprite[] sprites = framesA.Select(n => LoadSprite(VfxDir, n)).Where(s => s != null).ToArray();
                sr.sprite = sprites.Length > 0 ? sprites[0] : null;
                sr.sortingOrder = 11;
                var flip = go.AddComponent<FlipbookOneShot>();
                var so = new SerializedObject(flip);
                SerializedProperty pFrames = so.FindProperty("frames");
                pFrames.arraySize = sprites.Length;
                for (int i = 0; i < sprites.Length; i++)
                    pFrames.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
                so.FindProperty("framesPerSecond").floatValue = fpsA;
                so.ApplyModifiedPropertiesWithoutUndo();

                var child = (GameObject)PrefabUtility.InstantiatePrefab(layerB);
                child.transform.SetParent(go.transform, false);

                go.transform.localScale = new Vector3(scale, scale, 1f);
                return PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ---------------------------------------------------------------- 6. Sword prefab
        private static GameObject CreateSwordPrefab(MoveData move, string prefabPath)
        {
            var hitChannel = AssetDatabase.LoadAssetAtPath<HitEventChannelSO>(HitLandedChannelPath);
            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                root.layer = LayerMask.NameToLayer(GameLayers.ProjectileP1Name);
                root.transform.localScale = new Vector3(1.5f, 1.5f, 1f);   // kiếm 22px hơi nhỏ — phóng nhẹ. CẦN TEST.

                var sr = root.AddComponent<SpriteRenderer>();
                sr.sprite = LoadSprite(SpriteDir, "12-489");
                sr.sortingOrder = 5;

                var rb = root.AddComponent<Rigidbody2D>();
                rb.gravityScale = 1.47f;    // MUGEN 0.4px/tick² = 14.4 u/s² ≈ 1.47×9.81. CẦN TEST.
                rb.freezeRotation = false;  // xoay bằng angularVelocity (ProjectileLaunchSetup).
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;

                var body = root.AddComponent<BoxCollider2D>();
                body.isTrigger = true;      // chỉ để bắt va TƯỜNG (Projectile.OnTriggerEnter2D lọc layer Wall).
                body.size = new Vector2(0.2f, 0.2f);

                root.AddComponent<Projectile>();              // lifetime/maxTravel/despawn-on-hit mặc định.
                root.AddComponent<ProjectileLaunchSetup>();   // vy=4.2 + spin 720°/s + bật hitbox.

                var trail = root.AddComponent<TrailRenderer>();
                trail.time = 0.2f;
                trail.startWidth = 0.12f;
                trail.endWidth = 0f;
                trail.material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                trail.startColor = new Color(1f, 1f, 1f, 0.6f);
                trail.endColor = new Color(1f, 0.9f, 0.5f, 0f);
                trail.sortingOrder = 4;

                var hitboxGo = new GameObject("Hitbox");
                hitboxGo.transform.SetParent(root.transform, false);
                hitboxGo.layer = LayerMask.NameToLayer(GameLayers.HitboxName);
                var hbBox = hitboxGo.AddComponent<BoxCollider2D>();
                hbBox.isTrigger = true;
                hbBox.size = new Vector2(0.3f, 0.3f);
                // HitboxAuthoring ở FA.Combat (editor asmdef có ref); wire field private qua SerializedObject.
                var hb = hitboxGo.AddComponent<FA.Combat.HitboxAuthoring>();
                var so = new SerializedObject(hb);
                so.FindProperty("hitChannel").objectReferenceValue = hitChannel;
                so.FindProperty("moveOverride").objectReferenceValue = move;
                so.FindProperty("box").objectReferenceValue = hbBox;
                so.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ---------------------------------------------------------------- 7. Gán hit effect per-move
        /// <summary>Gán hitEffect/blockEffect vào TỪNG MoveData hiện có của Emiya theo mapping HitDef
        /// MUGEN (mỗi đòn 1 hitsound; spark var(20): anim 202 = chain spark, còn lại default; scale
        /// theo độ nặng). Đòn mới sau này chỉ cần gán trong Inspector.</summary>
        private static void AssignMoveEffects(Dictionary<string, EffectData> fx)
        {
            const string dataRoot = "Assets/_Project/Data/Emiya_Archer";
            var mapping = new (string path, string effect)[]
            {
                ($"{dataRoot}/Light_Attack/EmiyaArcher_nL.asset", "Hit_Light"),     // MUGEN 200: S7,30
                ($"{dataRoot}/Light_Attack/EmiyaArcher_nL2.asset", "Hit_Medium2"),  // 201: S7,32
                ($"{dataRoot}/Light_Attack/EmiyaArcher_nL3.asset", "Hit_Chain"),    // 202: S7,7 + spark 7020
                ($"{dataRoot}/Heavy_Attack/EmiyaArcher_nM.asset", "Hit_Medium1"),   // 300: S7,31
                ($"{dataRoot}/Heavy_Attack/EmiyaArcher_nM2.asset", "Hit_Medium2"),  // 301: S7,32
                ($"{dataRoot}/Heavy_Attack/EmiyaArcher_nM3.asset", "Hit_Medium1"),  // 302: S7,31
                ($"{dataRoot}/Heavy_Attack/EmiyaArcher_nM4.asset", "Hit_Heavy2"),   // 303: S7,35
                ($"{dataRoot}/EmiyaArcher_nH.asset", "Hit_Heavy"),                  // 405: S7,33 (spark to)
                ($"{dataRoot}/Special_Move/Special_1.asset", "Hit_Heavy2"),         // 1000: S7,35
                ($"{dataRoot}/Special_Move/Special_2.asset", "Hit_Medium2"),        // 1100: S7,32
                ($"{dataRoot}/Special_Move/Special_2_Ender.asset", "Hit_Heavy2"),
                (MoveDataPath, "Hit_Heavy"),                                        // kiếm S3 (1205: S7,33)
                (EnderDataPath, "Hit_Heavy"),
            };
            foreach ((string path, string effect) in mapping)
            {
                var move = AssetDatabase.LoadAssetAtPath<MoveData>(path);
                if (move == null) { Debug.LogWarning($"[Special3Setup] Không thấy MoveData {path}"); continue; }
                move.hitEffect = fx[effect];
                move.blockEffect = fx["Block_Guard"];
                EditorUtility.SetDirty(move);
            }
        }

        // ---------------------------------------------------------------- 8. Wire prefab nhân vật
        private static void WireCharacterPrefab(GameObject swordCarry, GameObject swordEnder,
                                                Dictionary<string, EffectData> fx)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var audio = root.GetComponent<AudioSource>();
                if (audio == null) audio = root.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.spatialBlend = 0f;

                var relay = root.GetComponentInChildren<AnimationEventRelay>();

                var cep = root.GetComponent<CharacterEffectPlayer>();
                if (cep == null) cep = root.AddComponent<CharacterEffectPlayer>();
                var soCep = new SerializedObject(cep);
                soCep.FindProperty("relay").objectReferenceValue = relay;
                soCep.FindProperty("audioSource").objectReferenceValue = audio;
                SerializedProperty pEffects = soCep.FindProperty("effects");
                var slots = new[] { fx["S3_CastFlash"], fx["S3_Voice"], fx["S3_Whoosh"], fx["S3_HandGlow"] };
                pEffects.arraySize = slots.Length;
                for (int i = 0; i < slots.Length; i++)
                    pEffects.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
                soCep.ApplyModifiedPropertiesWithoutUndo();

                var hep = root.GetComponent<HitEffectPlayer>();
                if (hep == null) hep = root.AddComponent<HitEffectPlayer>();
                var soHep = new SerializedObject(hep);
                soHep.FindProperty("hitConfirmedChannel").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<HitEventChannelSO>(HitConfirmedChannelPath);
                soHep.FindProperty("hitBlockedChannel").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<HitEventChannelSO>(HitBlockedChannelPath);
                soHep.FindProperty("hitEffect").objectReferenceValue = fx["Hit_Light"];   // fallback move không khai báo.
                soHep.FindProperty("blockEffect").objectReferenceValue = fx["Block_Guard"];
                soHep.FindProperty("audioSource").objectReferenceValue = audio;
                soHep.ApplyModifiedPropertiesWithoutUndo();

                Transform muzzle = root.transform.Find("Muzzle");
                if (muzzle == null)
                {
                    muzzle = new GameObject("Muzzle").transform;
                    muzzle.SetParent(root.transform, false);
                }
                muzzle.localPosition = new Vector3(0.10f, 0.38f, 0f);   // MUGEN helper pos 10,-38.

                var spawner = root.GetComponent<ProjectileSpawner>();
                if (spawner != null)
                {
                    var soSp = new SerializedObject(spawner);
                    soSp.FindProperty("muzzle").objectReferenceValue = muzzle;
                    if (soSp.FindProperty("hitChannel").objectReferenceValue == null)
                        soSp.FindProperty("hitChannel").objectReferenceValue =
                            AssetDatabase.LoadAssetAtPath<HitEventChannelSO>(HitLandedChannelPath);
                    soSp.FindProperty("projectileSpeed").floatValue = 3f;   // MUGEN vx 5px/tick = 3 u/s. CẦN TEST.
                    // variant 0 = kiếm carry (SpawnProjectile(0)), variant 1 = kiếm ender (SpawnProjectile(1)).
                    SerializedProperty pPrefabs = soSp.FindProperty("projectilePrefabs");
                    pPrefabs.arraySize = 2;
                    pPrefabs.GetArrayElementAtIndex(0).objectReferenceValue = swordCarry.GetComponent<Projectile>();
                    pPrefabs.GetArrayElementAtIndex(1).objectReferenceValue = swordEnder.GetComponent<Projectile>();
                    soSp.ApplyModifiedPropertiesWithoutUndo();
                }
                else Debug.LogError("[Special3Setup] Prefab thiếu ProjectileSpawner!");

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
