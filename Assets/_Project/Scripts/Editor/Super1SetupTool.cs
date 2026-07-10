using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FA.Core;
using FA.Specials;

namespace FA.EditorTools
{
    /// <summary>
    /// TOOL DỰNG SUPER 1 "Sword Barrage" cho Emiya Archer (idempotent) từ MUGEN state 2000:
    /// pose gồng (anim 2000 RÚT NGẮN) → xả 8 blade bay thẳng (7 carry + 1 ender văng — pattern
    /// carry/ender như Special 3) → recovery (anim 2002). Khác gốc CÓ CHỦ Ý: bỏ SuperPause 310 tick
    /// (time-stop = Phase 17) → super chạy real-time, đứng bắn ~3.2s là rủi ro chủ đích.
    /// Kích hoạt: NÚT Super1 (category=Super, P9 TrySuper — air-usable), tốn 3/7 ô mana (~50% GDD).
    /// Blade dùng ProjectileLaunchSetup overrideForwardSpeed (spawner chỉ có 1 speed chung = 3 của
    /// kiếm S3; blade ghi đè 9) + upSpeedJitter tỏa nhẹ + gravityScale 0 (bay thẳng).
    /// CLI: Unity.exe -batchmode -quit -executeMethod FA.EditorTools.Super1SetupTool.Run
    /// </summary>
    public static class Super1SetupTool
    {
        private const string SpriteDir = "Assets/_Project/Art/Characters/Emiya Archer/Super1";
        private const string SwordSpriteDir = "Assets/_Project/Art/Characters/Emiya Archer/Special3";
        private const string VfxDir = "Assets/_Project/Art/Characters/Emiya Archer/VFX";
        private const string AudioDir = "Assets/_Project/Audio/Emiya Archer";
        private const string ClipPath = "Assets/_Project/Animations/SampleFighter/Super_1.anim";
        private const string ControllerPath = "Assets/_Project/Animations/SampleFighter/SampleFighter.controller";
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/Emiya Archer.prefab";
        private const string SuperDataDir = "Assets/_Project/Data/Emiya_Archer/Super_Move";
        private const string MoveSetPath = "Assets/_Project/Data/Emiya_Archer/Special_Move/MoveSet.asset";
        private const string EffectsDataDir = "Assets/_Project/Data/Emiya_Archer/Effects";
        private const string VfxPrefabDir = "Assets/_Project/Prefabs/VFX";
        private const string BladePrefabPath = "Assets/_Project/Prefabs/Projectiles/BarrageBlade.prefab";
        private const string BladeEnderPrefabPath = "Assets/_Project/Prefabs/Projectiles/BarrageBlade_Ender.prefab";
        private const string HitLandedChannelPath = "Assets/_Project/Data/OnHitLandedChannel.asset";

        private const int StateId = 36;   // Special_3 = 35.
        private const float Fps = 60f;

        // Timeline (tick 60fps): pose anim 2000 RÚT (elem1 30→10t, elem6 30→15t) + barrage anim 2001
        // nguyên bản (15 elem + loop 711-720) + recovery anim 2002 nguyên bản.
        private static readonly (int tick, string sprite)[] Frames = BuildFrames();

        private static (int, string)[] BuildFrames()
        {
            var list = new List<(int, string)>();
            int t = 0;
            void Add(int sprite, int dur) { list.Add((t, $"12-{sprite}")); t += dur; }
            // Pose (anim 2000 rút ngắn):
            Add(700, 10); Add(701, 5); Add(702, 5); Add(703, 5); Add(704, 5); Add(705, 15);   // hết 45
            // Barrage (anim 2001): elem 1-15
            for (int s = 706; s <= 720; s++) Add(s, 4);                                        // 45..105
            // Loop 711-720 một lần (tương ứng elem 16-25 gốc)
            for (int s = 711; s <= 720; s++) Add(s, 4);                                        // 105..145
            // Recovery (anim 2002):
            Add(721, 3); Add(722, 3);
            for (int s = 723; s <= 732; s++) Add(s, 4);                                        // ..191
            return list.ToArray();
        }

        // Tick bắn (elem 4-8 volley 1 = tick 57..73 + 3 phát trong loop). Phát CUỐI = ender.
        private static readonly int[] CarryShotTicks = { 57, 61, 65, 69, 73, 109, 117 };
        private const int EnderShotTick = 125;
        private const int RecoveryEndTick = 190;

        [MenuItem("FA/Tools/Setup Archer Super 1 (sword barrage)")]
        public static void Run()
        {
            EnsureFolders();
            ConfigureSpriteImports();
            AnimationClip clip = CreateClip();
            MoveData main = CreateMainMoveData(clip);
            MoveData blade = CreateBladeMoveData(clip, false);
            MoveData bladeEnder = CreateBladeMoveData(clip, true);
            GameObject bladePrefab = CreateBladePrefab(blade, BladePrefabPath);
            GameObject bladeEnderPrefab = CreateBladePrefab(bladeEnder, BladeEnderPrefabPath);
            Dictionary<string, EffectData> fx = CreateEffectAssets();
            AssignHitEffects(blade, bladeEnder);
            AddMoveSetEntry(main);
            AddAnimatorState(clip);
            WireCharacterPrefab(bladePrefab, bladeEnderPrefab, fx);
            AssetDatabase.SaveAssets();
            Debug.Log("[Super1Setup] DONE — Super 1 barrage: nút Super1, 3 ô mana, 8 blade (7 carry + 1 ender).");
        }

        private static void EnsureFolders()
        {
            foreach (string dir in new[] { SuperDataDir })
            {
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    string parent = dir.Substring(0, dir.LastIndexOf('/'));
                    AssetDatabase.CreateFolder(parent, dir.Substring(dir.LastIndexOf('/') + 1));
                }
            }
        }

        private static void ConfigureSpriteImports()
        {
            // Super1 (thân nhân vật: Bottom) + VFX mới 826 (Center) — cùng logic Special3SetupTool.
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteDir, VfxDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                if (imp == null) continue;
                var settings = new TextureImporterSettings();
                imp.ReadTextureSettings(settings);
                settings.textureType = TextureImporterType.Sprite;
                settings.spriteMode = (int)SpriteImportMode.Single;
                settings.spritePixelsPerUnit = 100f;
                settings.spriteAlignment = (int)(path.StartsWith(VfxDir)
                    ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
                settings.mipmapEnabled = false;
                imp.SetTextureSettings(settings);
                imp.SaveAndReimport();
            }
            AssetDatabase.Refresh();
        }

        private static AnimationClip CreateClip()
        {
            var clip = new AnimationClip { frameRate = Fps };
            var keys = new List<ObjectReferenceKeyframe>();
            foreach ((int tick, string spriteName) in Frames)
            {
                Sprite s = Special3SetupTool.LoadSprite(SpriteDir, spriteName);
                if (s == null) { Debug.LogError($"[Super1Setup] Thiếu sprite {spriteName}"); continue; }
                keys.Add(new ObjectReferenceKeyframe { time = tick / Fps, value = s });
            }
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());

            AnimationEvent Ev(int tick, string fn, int i = 0) =>
                new AnimationEvent { time = tick / Fps, functionName = fn, intParameter = i };

            var events = new List<AnimationEvent>
            {
                Ev(0, "PlayVfx", 4),    // super flash (anim 6106 / sprite 826)
                Ev(0, "PlaySfx", 4),    // announce (S6100,2)
                Ev(0, "PlaySfx", 5),    // voice (S2000,5)
                Ev(30, "PlaySfx", 6),   // draw kiếm (S800,0 — cuối pose)
                Ev(45, "PlaySfx", 7),   // vào loạt bắn (S2005,1)
                Ev(EnderShotTick, "SpawnProjectile", 3),   // blade ENDER (variant 3: văng)
                Ev(EnderShotTick, "PlaySfx", 8),
                Ev(RecoveryEndTick, "RecoveryEnd"),
            };
            foreach (int tick in CarryShotTicks)
            {
                events.Add(Ev(tick, "SpawnProjectile", 2));   // blade carry (variant 2)
                events.Add(Ev(tick, "PlaySfx", 8));           // tiếng mỗi phát (S2005,0)
            }
            AnimationUtility.SetAnimationEvents(clip, events.OrderBy(e => e.time).ToArray());

            AssetDatabase.DeleteAsset(ClipPath);
            AssetDatabase.CreateAsset(clip, ClipPath);
            return clip;
        }

        private static MoveData CreateMainMoveData(AnimationClip clip)
        {
            var move = LoadOrCreate($"{SuperDataDir}/Super_1.asset");
            move.moveId = "super1";
            move.category = MoveCategory.Super;       // P9 TrySuper quét category này.
            move.animatorStateId = StateId;
            move.clip = clip;
            move.motion = MotionInput.None;           // super = nút riêng, không motion (GDD).
            move.button = InputButton.Super1;
            move.manaCostUnits = 2;                   // user chốt 2026-07-10: Super1 = 2, Super2 = 3. CẦN TEST.
            move.baseDamagePercent = 0f;              // damage nằm ở blade (moveOverride) — thân không hitbox.
            move.causesLaunch = false;
            move.role = MoveRole.Projectile;
            move.hitstunFrames = 0;
            move.hitstopFrames = 0;
            move.motionImpulses = new List<Vector2>();
            move.followUps = new List<FollowUp>();
            EditorUtility.SetDirty(move);
            return move;
        }

        private static MoveData CreateBladeMoveData(AnimationClip clip, bool ender)
        {
            var move = LoadOrCreate($"{SuperDataDir}/{(ender ? "Super_1_Blade_Ender" : "Super_1_Blade")}.asset");
            move.moveId = ender ? "super1_blade_ender" : "super1_blade";
            move.category = MoveCategory.Super;
            move.animatorStateId = StateId;
            move.clip = clip;
            move.motion = MotionInput.None;
            move.button = InputButton.Super1;
            move.manaCostUnits = 0;                   // cost trừ ở move chính.
            move.baseDamagePercent = ender ? 4f : 2.5f;   // 7×2.5 + 4 ≈ 21.5% full combo. CẦN TEST.
            move.launchAngle = 42f;
            move.baseKnockback = ender ? 9f : 6f;
            move.knockbackGrowth = ender ? 0.12f : 0.08f;
            move.isSpike = false;
            move.causesLaunch = ender;                // carry: không pending (fix projectile) — chỉ hitstun.
            move.role = MoveRole.Projectile;
            move.hitsLow = false;
            move.hitsHigh = true;
            move.hitstunFrames = ender ? 20 : 24;     // carry giữ combo giữa các phát (gap 8 tick). CẦN TEST.
            move.hitstopFrames = ender ? 8 : 3;       // hitstop nhỏ cho hit liên hoàn khỏi khựng.
            move.motionImpulses = new List<Vector2>();
            move.followUps = new List<FollowUp>();
            EditorUtility.SetDirty(move);
            return move;
        }

        private static MoveData LoadOrCreate(string path)
        {
            var move = AssetDatabase.LoadAssetAtPath<MoveData>(path);
            if (move == null)
            {
                move = ScriptableObject.CreateInstance<MoveData>();
                AssetDatabase.CreateAsset(move, path);
            }
            return move;
        }

        /// <summary>Blade barrage: như ThrownSword nhưng BAY THẲNG (gravity 0, override speed 9, jitter
        /// dọc tỏa nhẹ), sống ngắn hơn, sprite kiếm 12-489 nhỏ hơn.</summary>
        private static GameObject CreateBladePrefab(MoveData move, string prefabPath)
        {
            var hitChannel = AssetDatabase.LoadAssetAtPath<HitEventChannelSO>(HitLandedChannelPath);
            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                root.layer = LayerMask.NameToLayer(GameLayers.ProjectileP1Name);
                root.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

                var sr = root.AddComponent<SpriteRenderer>();
                sr.sprite = Special3SetupTool.LoadSprite(SwordSpriteDir, "12-489");
                sr.sortingOrder = 5;

                var rb = root.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;                 // bay THẲNG (khác kiếm S3 vòng cung).
                rb.freezeRotation = false;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;

                var body = root.AddComponent<BoxCollider2D>();
                body.isTrigger = true;
                body.size = new Vector2(0.2f, 0.2f);

                root.AddComponent<Projectile>();

                var launch = root.AddComponent<ProjectileLaunchSetup>();
                var soL = new SerializedObject(launch);
                soL.FindProperty("launchUpSpeed").floatValue = 0f;
                soL.FindProperty("spinDegPerSec").floatValue = 900f;
                soL.FindProperty("overrideForwardSpeed").floatValue = 9f;   // spawner chung = 3 (kiếm S3). CẦN TEST.
                soL.FindProperty("upSpeedJitter").floatValue = 0.6f;        // loạt bắn tỏa nhẹ.
                soL.ApplyModifiedPropertiesWithoutUndo();

                var trail = root.AddComponent<TrailRenderer>();
                trail.time = 0.15f;
                trail.startWidth = 0.1f;
                trail.endWidth = 0f;
                trail.material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                trail.startColor = new Color(0.7f, 0.85f, 1f, 0.6f);   // xanh thép — phân biệt kiếm S3.
                trail.endColor = new Color(0.7f, 0.85f, 1f, 0f);
                trail.sortingOrder = 4;

                var hitboxGo = new GameObject("Hitbox");
                hitboxGo.transform.SetParent(root.transform, false);
                hitboxGo.layer = LayerMask.NameToLayer(GameLayers.HitboxName);
                var hbBox = hitboxGo.AddComponent<BoxCollider2D>();
                hbBox.isTrigger = true;
                hbBox.size = new Vector2(0.3f, 0.3f);
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

        private static Dictionary<string, EffectData> CreateEffectAssets()
        {
            // Super flash = MUGEN anim 6106 (sprite 826-0..15, 3t/frame = 20fps).
            GameObject flash = Special3SetupTool.CreateFlipbookPrefab("Super1Flash_VFX",
                Enumerable.Range(0, 16).Select(i => $"826-{i}").ToArray(), 20f, 0.5f, 12);

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
                    Debug.LogError($"[Super1Setup] {name}: thiếu AudioClip ({fx.sfxClips.Length}/{clips.Length})");
                fx.volume = vol;
                EditorUtility.SetDirty(fx);
                result[name] = fx;
                return fx;
            }

            Make("Super1_Cast", flash, new Vector2(0f, 0.4f), true, new[] { "S6100_2" }, 1f);
            Make("Super1_Voice", null, Vector2.zero, false, new[] { "S2000_5" }, 0.9f);
            Make("Super1_Draw", null, Vector2.zero, false, new[] { "S800_0" }, 0.9f);
            Make("Super1_Volley", null, Vector2.zero, false, new[] { "S2005_1" }, 1f);
            Make("Super1_Shot", null, Vector2.zero, false, new[] { "S2005_0" }, 0.85f);
            return result;
        }

        private static void AssignHitEffects(MoveData blade, MoveData bladeEnder)
        {
            var medium = AssetDatabase.LoadAssetAtPath<EffectData>($"{EffectsDataDir}/Hit_Medium1.asset");
            var heavy = AssetDatabase.LoadAssetAtPath<EffectData>($"{EffectsDataDir}/Hit_Heavy2.asset");
            var guard = AssetDatabase.LoadAssetAtPath<EffectData>($"{EffectsDataDir}/Block_Guard.asset");
            blade.hitEffect = medium;
            blade.blockEffect = guard;
            bladeEnder.hitEffect = heavy;
            bladeEnder.blockEffect = guard;
            EditorUtility.SetDirty(blade);
            EditorUtility.SetDirty(bladeEnder);
        }

        private static void AddMoveSetEntry(MoveData move)
        {
            var moveSet = AssetDatabase.LoadAssetAtPath<MoveSetData>(MoveSetPath);
            if (moveSet == null) { Debug.LogError("[Super1Setup] Không thấy MoveSet.asset"); return; }
            var so = new SerializedObject(moveSet);
            SerializedProperty entries = so.FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++)
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("move").objectReferenceValue == move)
                    return;
            entries.arraySize++;
            SerializedProperty e = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            e.FindPropertyRelative("motion").intValue = (int)MotionInput.None;
            e.FindPropertyRelative("button").intValue = (int)InputButton.Super1;
            e.FindPropertyRelative("move").objectReferenceValue = move;
            e.FindPropertyRelative("charged").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(moveSet);
        }

        private static void AddAnimatorState(AnimationClip clip)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) { Debug.LogError("[Super1Setup] Không thấy controller"); return; }
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Super_1");
            if (state == null) state = sm.AddState("Super_1");
            state.motion = clip;
            state.writeDefaultValues = true;
            AnimatorStateTransition tr = sm.anyStateTransitions.FirstOrDefault(t => t.destinationState == state);
            if (tr == null) tr = sm.AddAnyStateTransition(state);
            tr.hasExitTime = false;
            tr.hasFixedDuration = true;
            tr.duration = 0f;
            tr.canTransitionToSelf = false;   // gotcha Can-Transition-To-Self.
            tr.conditions = new[]
            {
                new AnimatorCondition { mode = AnimatorConditionMode.Equals, parameter = "StateId", threshold = StateId }
            };
            EditorUtility.SetDirty(controller);
        }

        private static void WireCharacterPrefab(GameObject blade, GameObject bladeEnder,
                                                Dictionary<string, EffectData> fx)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                // CharacterEffectPlayer: GIỮ slot 0-3 (Special 3), thêm 4-8 cho Super 1.
                var cep = root.GetComponent<FA.Effects.CharacterEffectPlayer>();
                if (cep == null) { Debug.LogError("[Super1Setup] Prefab thiếu CharacterEffectPlayer (chạy Special3SetupTool trước)."); return; }
                var soCep = new SerializedObject(cep);
                SerializedProperty pEffects = soCep.FindProperty("effects");
                var extra = new[]
                {
                    fx["Super1_Cast"], fx["Super1_Voice"], fx["Super1_Draw"],
                    fx["Super1_Volley"], fx["Super1_Shot"],
                };
                int baseCount = 4;   // slot 0-3 = Special 3.
                pEffects.arraySize = baseCount + extra.Length;
                for (int i = 0; i < extra.Length; i++)
                    pEffects.GetArrayElementAtIndex(baseCount + i).objectReferenceValue = extra[i];
                soCep.ApplyModifiedPropertiesWithoutUndo();

                // ProjectileSpawner: variant 2 = blade carry, 3 = blade ender (0/1 = kiếm S3).
                var spawner = root.GetComponent<ProjectileSpawner>();
                if (spawner != null)
                {
                    var soSp = new SerializedObject(spawner);
                    SerializedProperty pPrefabs = soSp.FindProperty("projectilePrefabs");
                    if (pPrefabs.arraySize < 4) pPrefabs.arraySize = 4;
                    pPrefabs.GetArrayElementAtIndex(2).objectReferenceValue = blade.GetComponent<Projectile>();
                    pPrefabs.GetArrayElementAtIndex(3).objectReferenceValue = bladeEnder.GetComponent<Projectile>();
                    soSp.ApplyModifiedPropertiesWithoutUndo();
                }
                else Debug.LogError("[Super1Setup] Prefab thiếu ProjectileSpawner!");

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
