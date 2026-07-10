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
    /// TOOL DỰNG SUPER 2 "Bow Shot" cho Emiya Archer (idempotent) từ MUGEN state 2100:
    /// giương cung nạp năng lượng (anim 2100 + 1 vòng loop 670-678, có SUPER FLASH cut-in 9000-20)
    /// → bắn 1 MŨI TÊN KHOAN (helper 2130, vx=20px/tick → 12 u/s): trúng thì GHIM lại khoan nhiều hit
    /// nhỏ giọt (ProjectileDrill — MUGEN hitcount≤14 + VelSet x=1) rồi hit CUỐI = NỔ LỚN văng
    /// (vụ nổ anim 1418+2138/sprite 758+760 = hitEffect per-move của ender). Khác gốc CÓ CHỦ Ý:
    /// MUGEN kéo lê địch theo (TargetBind) — engine không có target-bind nên victim đứng ăn khoan
    /// tại chỗ (hitstun giữ), cảm giác "khoan vào người rồi bùng nổ" giữ nguyên.
    /// Kích hoạt: NÚT Super2, tốn 3/7 ô mana (user chốt: Super1 = 2, Super2 = 3).
    /// Super flash: event SuperFlashStart Int=1 → portrait 9000-20 (slot 1 trên SuperFlashController).
    /// CLI: Unity.exe -batchmode -quit -executeMethod FA.EditorTools.Super2SetupTool.Run
    /// </summary>
    public static class Super2SetupTool
    {
        private const string SpriteDir = "Assets/_Project/Art/Characters/Emiya Archer/Super2";
        private const string VfxDir = "Assets/_Project/Art/Characters/Emiya Archer/VFX";
        private const string PortraitDir = "Assets/_Project/Art/Characters/Emiya Archer/Portrait";
        private const string AudioDir = "Assets/_Project/Audio/Emiya Archer";
        private const string ClipPath = "Assets/_Project/Animations/SampleFighter/Super_2.anim";
        private const string ControllerPath = "Assets/_Project/Animations/SampleFighter/SampleFighter.controller";
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/Emiya Archer.prefab";
        private const string SuperDataDir = "Assets/_Project/Data/Emiya_Archer/Super_Move";
        private const string MoveSetPath = "Assets/_Project/Data/Emiya_Archer/Special_Move/MoveSet.asset";
        private const string EffectsDataDir = "Assets/_Project/Data/Emiya_Archer/Effects";
        private const string ArrowPrefabPath = "Assets/_Project/Prefabs/Projectiles/Super2Arrow.prefab";
        private const string HitLandedChannelPath = "Assets/_Project/Data/OnHitLandedChannel.asset";

        private const int StateId = 37;        // Special_3 = 35, Super_1 = 36.
        private const float Fps = 60f;
        private const int PortraitIndex = 1;   // SuperFlashController.portraits: 0 = Super1 (9000-10), 1 = Super2 (9000-20).
        private const int ArrowVariant = 4;    // ProjectileSpawner: 0/1 = kiếm S3, 2/3 = blade Super1.
        private const int EffectSlotBase = 9;  // CharacterEffectPlayer: 0-3 S3, 4-8 Super1 → Super2 = 9-14.

        // Timeline tick 60fps — anim 2100 nguyên bản (639-648, 664, 665, 669) + MỘT vòng loop giương
        // cung (670-678 ×4t) + anim 2102 bắn (679-699). MUGEN loop vô hạn theo hold_c — port bỏ hold.
        private const int ShootTick = 95;        // frame đầu anim 2102 (679) — helper mũi tên spawn tại đây.
        private const int FlashEndTick = 90;     // hết đoạn giương cung (MUGEN SuperPause 110t), trước phát bắn.
        private const int RecoveryEndTick = 174; // clip dài 176t.

        private static readonly (int tick, string sprite)[] Frames = BuildFrames();

        private static (int, string)[] BuildFrames()
        {
            var list = new List<(int, string)>();
            int t = 0;
            void Add(int sprite, int dur) { list.Add((t, $"12-{sprite}")); t += dur; }
            for (int s = 639; s <= 647; s++) Add(s, 3);   // giương cung: 0..26
            Add(648, 15); Add(664, 4); Add(665, 10); Add(669, 3);   // gồng: 27..58
            for (int s = 670; s <= 678; s++) Add(s, 4);   // 1 vòng loop nạp: 59..94
            Add(679, 3); Add(680, 2);                     // thả dây: 95..99
            for (int s = 681; s <= 699; s++) Add(s, 4);   // bắn + recovery: 100..175
            return list.ToArray();
        }

        [MenuItem("FA/Tools/Setup Archer Super 2 (bow shot)")]
        public static void Run()
        {
            ConfigureSpriteImports();
            AnimationClip clip = CreateClip();
            MoveData main = CreateMainMoveData(clip);
            MoveData drill = CreateDrillMoveData(clip);
            MoveData arrow = CreateArrowMoveData(clip);
            Dictionary<string, EffectData> fx = Super2FxBuilder.CreateEffectAssets();
            GameObject arrowPrefab = CreateArrowPrefab(drill, arrow, fx["Super2_HitExplosion"]);
            AssignHitEffects(drill, arrow);
            AddMoveSetEntry(main);
            AddAnimatorState(clip);
            WireCharacterPrefab(arrowPrefab, fx);
            AssetDatabase.SaveAssets();
            Debug.Log("[Super2Setup] DONE — Super 2 bow shot: nút Super2, 3 ô mana, flash 9000-20, " +
                      "mũi khoan 3 lớp → nổ 3 lớp + boom → văng ở hit cuối, gió tụ lực khi kéo cung.");
        }

        private static void ConfigureSpriteImports()
        {
            // Super2 (thân: Bottom) + VFX mới 815/758/711/802/798 (Center) — cùng logic các tool trước.
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
                if (s == null) { Debug.LogError($"[Super2Setup] Thiếu sprite {spriteName}"); continue; }
                keys.Add(new ObjectReferenceKeyframe { time = tick / Fps, value = s });
            }
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());

            AnimationEvent Ev(int tick, string fn, int i = 0) =>
                new AnimationEvent { time = tick / Fps, functionName = fn, intParameter = i };

            var events = new[]
            {
                Ev(0, "SuperFlashStart", PortraitIndex),   // freeze + zoom + tối màn + cut-in 9000-20.
                Ev(0, "PlaySfx", 9),     // announce (S6100,2)
                Ev(0, "PlaySfx", 10),    // giương cung/nạp (S2100,0)
                Ev(6, "PlayVfx", 9),     // burst kích chiêu (anim 6106 — tái dùng Super1Flash_VFX)
                Ev(6, "PlayVfx", 15),    // GIÓ TỤ LỰC: hút từ trước mặt về + chùm sáng đầu cung (PS)
                Ev(42, "PlayVfx", 11),   // orb năng lượng ở tay cung (anim 2110, loop tới lúc bắn)
                Ev(56, "PlaySfx", 11),   // năng lượng dâng cao (S2105,0)
                Ev(59, "PlayVfx", 12),   // vòng năng lượng bùng (anim 2111/2121)
                Ev(59, "PlaySfx", 12),   // xoẹt năng lượng (S404,2)
                Ev(FlashEndTick, "SuperFlashEnd"),
                Ev(ShootTick, "SpawnProjectile", ArrowVariant),
                Ev(ShootTick, "PlaySfx", 13),   // tiếng bắn (S2100,5)
                Ev(ShootTick, "PlayVfx", 13),   // muzzle burst (anim 8007)
                Ev(ShootTick, "PlaySfx", 14),   // whoosh tên rời cung (S2105,6)
                Ev(RecoveryEndTick, "RecoveryEnd"),
            };
            AnimationUtility.SetAnimationEvents(clip, events.OrderBy(e => e.time).ToArray());

            AssetDatabase.DeleteAsset(ClipPath);
            AssetDatabase.CreateAsset(clip, ClipPath);
            return clip;
        }

        private static MoveData CreateMainMoveData(AnimationClip clip)
        {
            var move = LoadOrCreate($"{SuperDataDir}/Super_2.asset");
            move.moveId = "super2";
            move.category = MoveCategory.Super;
            move.animatorStateId = StateId;
            move.clip = clip;
            move.motion = MotionInput.None;
            move.button = InputButton.Super2;
            move.manaCostUnits = 3;                   // user chốt 2026-07-10: Super1 = 2, Super2 = 3. CẦN TEST.
            move.baseDamagePercent = 0f;              // damage nằm ở mũi tên (moveOverride).
            move.causesLaunch = false;
            move.role = MoveRole.Projectile;
            move.hitstunFrames = 0;
            move.hitstopFrames = 0;
            move.motionImpulses = new List<Vector2>();
            move.followUps = new List<FollowUp>();
            EditorUtility.SetDirty(move);
            return move;
        }

        /// <summary>Hit KHOAN nhỏ giọt (MUGEN 15 dmg/hit): % rất nhỏ, hitstun đủ giữ victim tới hit kế
        /// (rehit 8f), KHÔNG launch (projectile carry — không pending, victim không kẹt Hurt).</summary>
        private static MoveData CreateDrillMoveData(AnimationClip clip)
        {
            var move = LoadOrCreate($"{SuperDataDir}/Super_2_Drill.asset");
            move.moveId = "super2_drill";
            move.category = MoveCategory.Super;
            move.animatorStateId = StateId;
            move.clip = clip;
            move.motion = MotionInput.None;
            move.button = InputButton.Super2;
            move.manaCostUnits = 0;
            move.baseDamagePercent = 1.5f;            // nhỏ giọt: 5 hit ≈ 7.5%. CẦN TEST.
            move.launchAngle = 0f;
            move.baseKnockback = 0f;
            move.knockbackGrowth = 0f;
            move.isSpike = false;
            move.causesLaunch = false;
            move.role = MoveRole.Projectile;
            move.hitsLow = false;
            move.hitsHigh = true;
            move.hitstunFrames = 22;                  // > rehitFrames (8) → victim bị giữ suốt chuỗi khoan.
            move.hitstopFrames = 2;                   // khựng nhẹ mỗi nhát khoan.
            move.motionImpulses = new List<Vector2>();
            move.followUps = new List<FollowUp>();
            EditorUtility.SetDirty(move);
            return move;
        }

        private static MoveData CreateArrowMoveData(AnimationClip clip)
        {
            var move = LoadOrCreate($"{SuperDataDir}/Super_2_Arrow.asset");
            move.moveId = "super2_arrow";
            move.category = MoveCategory.Super;
            move.animatorStateId = StateId;
            move.clip = clip;
            move.motion = MotionInput.None;
            move.button = InputButton.Super2;
            move.manaCostUnits = 0;                   // cost trừ ở move chính.
            move.baseDamagePercent = 10f;             // hit NỔ KẾT — dmg mạnh (khoan 7.5% + nổ 10%). CẦN TEST.
            move.launchAngle = 65f;                   // MUGEN vel nổ (-5,-13) → bốc thẳng lên chéo sau.
            move.baseKnockback = 11f;
            move.knockbackGrowth = 0.14f;
            move.isSpike = false;
            move.causesLaunch = true;                 // ENDER: văng NGAY trên hit (path launcher P7).
            move.role = MoveRole.Projectile;
            move.hitsLow = false;
            move.hitsHigh = true;
            move.hitstunFrames = 24;
            move.hitstopFrames = 12;
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

        /// <summary>Mũi tên beam (sprite 815 flipbook LOOP): bay thẳng nhanh (12 u/s = MUGEN 20px/tick),
        /// không xoay, hitbox theo Clsn1 gốc (-29,-9 → 39,11 = 0.68×0.2u thế giới). Trúng → GHIM khoan
        /// (ProjectileDrill: SetMove carry→ender, despawnOnHit tắt — drill tự quản vòng đời).</summary>
        private static GameObject CreateArrowPrefab(MoveData drillMove, MoveData enderMove,
                                                    EffectData explosionFx)
        {
            var hitChannel = AssetDatabase.LoadAssetAtPath<HitEventChannelSO>(HitLandedChannelPath);
            var root = new GameObject("Super2Arrow");
            try
            {
                root.layer = LayerMask.NameToLayer(GameLayers.ProjectileP1Name);
                root.transform.localScale = new Vector3(0.4f, 0.4f, 1f);   // MUGEN explod 2134 scale .4. CẦN TEST.

                var sr = root.AddComponent<SpriteRenderer>();
                sr.sprite = Special3SetupTool.LoadSprite(VfxDir, "815-0");
                sr.sortingOrder = 6;

                // Flipbook LOOP mũi tên rung năng lượng (anim 2134: 2t/frame = 30fps); sống theo Projectile.
                var flip = root.AddComponent<FA.Effects.FlipbookOneShot>();
                var soF = new SerializedObject(flip);
                SerializedProperty pFrames = soF.FindProperty("frames");
                Sprite[] frames = Enumerable.Range(0, 10)
                    .Select(i => Special3SetupTool.LoadSprite(VfxDir, $"815-{i}")).Where(s => s != null).ToArray();
                pFrames.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++)
                    pFrames.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                soF.FindProperty("framesPerSecond").floatValue = 30f;
                soF.FindProperty("loop").boolValue = true;
                soF.ApplyModifiedPropertiesWithoutUndo();

                var rb = root.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.freezeRotation = true;   // tên bay thẳng, không xoay.
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;

                var body = root.AddComponent<BoxCollider2D>();
                body.isTrigger = true;
                body.size = new Vector2(1.2f, 0.4f);   // ×0.4 scale = 0.48×0.16u.

                var proj = root.AddComponent<Projectile>();
                var soP = new SerializedObject(proj);
                soP.FindProperty("despawnOnHit").boolValue = false;   // drill tự quản (khoan nhiều hit).
                soP.FindProperty("lifetime").floatValue = 4f;          // đủ cho bay + chuỗi khoan.
                soP.ApplyModifiedPropertiesWithoutUndo();

                var drill = root.AddComponent<ProjectileDrill>();
                var soD = new SerializedObject(drill);
                soD.FindProperty("carryMove").objectReferenceValue = drillMove;
                soD.FindProperty("enderMove").objectReferenceValue = enderMove;
                soD.FindProperty("explosionEffect").objectReferenceValue = explosionFx;
                soD.FindProperty("hitConfirmedChannel").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<HitEventChannelSO>(
                        "Assets/_Project/Data/OnHitConfirmedChannel.asset");   // ghim victim (hit thật).
                soD.FindProperty("carryHits").intValue = 6;      // tổng 10 hit: 6 khoan + 3 trong nổ + 1 văng. CẦN TEST.
                soD.FindProperty("blastHits").intValue = 3;
                soD.FindProperty("rehitFrames").intValue = 8;
                soD.FindProperty("drillSpeed").floatValue = 0.8f;
                soD.ApplyModifiedPropertiesWithoutUndo();

                var launch = root.AddComponent<ProjectileLaunchSetup>();
                var soL = new SerializedObject(launch);
                soL.FindProperty("launchUpSpeed").floatValue = 0f;
                soL.FindProperty("spinDegPerSec").floatValue = 0f;
                soL.FindProperty("overrideForwardSpeed").floatValue = 12f;   // MUGEN vx 20px/tick.
                soL.FindProperty("upSpeedJitter").floatValue = 0f;
                soL.ApplyModifiedPropertiesWithoutUndo();

                var trail = root.AddComponent<TrailRenderer>();
                trail.time = 0.32f;
                trail.startWidth = 0.34f;   // dày hơn (feedback: beam chưa "lực").
                trail.endWidth = 0f;
                trail.material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                trail.startColor = new Color(1f, 0.45f, 0.3f, 0.7f);   // đỏ cam — tên phá giáp.
                trail.endColor = new Color(1f, 0.45f, 0.3f, 0f);
                trail.sortingOrder = 3;

                // Lớp GLOW bao ngoài + VỆT ĐUÔI (Super2FxBuilder) — beam 3 lớp cho dày.
                Super2FxBuilder.AddArrowVisualLayers(root);

                var hitboxGo = new GameObject("Hitbox");
                hitboxGo.transform.SetParent(root.transform, false);
                hitboxGo.layer = LayerMask.NameToLayer(GameLayers.HitboxName);
                var hbBox = hitboxGo.AddComponent<BoxCollider2D>();
                hbBox.isTrigger = true;
                hbBox.size = new Vector2(1.7f, 0.5f);   // Clsn1 68×20px / scale 0.4.
                hbBox.offset = new Vector2(0.12f, 0f);  // Clsn1 lệch mũi (+5px).
                var hb = hitboxGo.AddComponent<FA.Combat.HitboxAuthoring>();
                var so = new SerializedObject(hb);
                so.FindProperty("hitChannel").objectReferenceValue = hitChannel;
                // moveOverride ĐỂ TRỐNG (override thắng SetMove) — ProjectileDrill cấp move runtime
                // để swap được carry → ender giữa chừng.
                so.FindProperty("moveOverride").objectReferenceValue = null;
                so.FindProperty("box").objectReferenceValue = hbBox;
                so.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, ArrowPrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void AssignHitEffects(MoveData drill, MoveData arrow)
        {
            var guard = AssetDatabase.LoadAssetAtPath<EffectData>($"{EffectsDataDir}/Block_Guard.asset");
            var medium = AssetDatabase.LoadAssetAtPath<EffectData>($"{EffectsDataDir}/Hit_Medium1.asset");
            var heavy = AssetDatabase.LoadAssetAtPath<EffectData>($"{EffectsDataDir}/Hit_Heavy2.asset");
            drill.hitEffect = medium;                 // spark nhỏ mỗi nhát khoan/nhỏ giọt trong nổ.
            drill.blockEffect = guard;
            arrow.hitEffect = heavy;                  // hit văng: spark nặng — VỤ NỔ do drill spawn rồi.
            arrow.blockEffect = guard;
            EditorUtility.SetDirty(drill);
            EditorUtility.SetDirty(arrow);
        }

        private static void AddMoveSetEntry(MoveData move)
        {
            var moveSet = AssetDatabase.LoadAssetAtPath<MoveSetData>(MoveSetPath);
            if (moveSet == null) { Debug.LogError("[Super2Setup] Không thấy MoveSet.asset"); return; }
            var so = new SerializedObject(moveSet);
            SerializedProperty entries = so.FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++)
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("move").objectReferenceValue == move)
                    return;
            entries.arraySize++;
            SerializedProperty e = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            e.FindPropertyRelative("motion").intValue = (int)MotionInput.None;
            e.FindPropertyRelative("button").intValue = (int)InputButton.Super2;
            e.FindPropertyRelative("move").objectReferenceValue = move;
            e.FindPropertyRelative("charged").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(moveSet);
        }

        private static void AddAnimatorState(AnimationClip clip)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) { Debug.LogError("[Super2Setup] Không thấy controller"); return; }
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Super_2");
            if (state == null) state = sm.AddState("Super_2");
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

        private static void WireCharacterPrefab(GameObject arrow, Dictionary<string, EffectData> fx)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                // CharacterEffectPlayer: GIỮ slot 0-8 (S3 + Super1), thêm 9-14 cho Super 2.
                var cep = root.GetComponent<FA.Effects.CharacterEffectPlayer>();
                if (cep == null) { Debug.LogError("[Super2Setup] Prefab thiếu CharacterEffectPlayer (chạy Special3SetupTool trước)."); return; }
                var soCep = new SerializedObject(cep);
                SerializedProperty pEffects = soCep.FindProperty("effects");
                var extra = new[]
                {
                    fx["Super2_Cast"], fx["Super2_Draw"], fx["Super2_Charge"],
                    fx["Super2_Release"], fx["Super2_Shot"], fx["Super2_ArrowWhoosh"],
                    fx["Super2_Wind"],   // slot 15 — gió tụ lực khi kéo cung.
                };
                if (pEffects.arraySize < EffectSlotBase + extra.Length)
                    pEffects.arraySize = EffectSlotBase + extra.Length;
                for (int i = 0; i < extra.Length; i++)
                    pEffects.GetArrayElementAtIndex(EffectSlotBase + i).objectReferenceValue = extra[i];
                soCep.ApplyModifiedPropertiesWithoutUndo();

                // ProjectileSpawner: variant 4 = mũi tên Super 2.
                var spawner = root.GetComponent<ProjectileSpawner>();
                if (spawner != null)
                {
                    var soSp = new SerializedObject(spawner);
                    SerializedProperty pPrefabs = soSp.FindProperty("projectilePrefabs");
                    if (pPrefabs.arraySize < ArrowVariant + 1) pPrefabs.arraySize = ArrowVariant + 1;
                    pPrefabs.GetArrayElementAtIndex(ArrowVariant).objectReferenceValue =
                        arrow.GetComponent<Projectile>();
                    soSp.ApplyModifiedPropertiesWithoutUndo();
                }
                else Debug.LogError("[Super2Setup] Prefab thiếu ProjectileSpawner!");

                // SuperFlashController: portraits [0]=9000-10 (Super1), [1]=9000-20 (Super2).
                var flash = root.GetComponent<FA.Effects.SuperFlashController>();
                if (flash != null)
                {
                    var soFl = new SerializedObject(flash);
                    SerializedProperty pPortraits = soFl.FindProperty("portraits");
                    if (pPortraits.arraySize < 2) pPortraits.arraySize = 2;
                    pPortraits.GetArrayElementAtIndex(0).objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<Sprite>($"{PortraitDir}/9000-10.png");
                    pPortraits.GetArrayElementAtIndex(PortraitIndex).objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<Sprite>($"{PortraitDir}/9000-20.png");
                    // Flash LÂU + zoom THẤY RÕ (feedback user): giữ caster đứng im 1s đầu flash
                    // (áp mọi super) + zoom vào chậm hơn 0.12 → 0.3s.
                    soFl.FindProperty("holdSeconds").floatValue = 1f;
                    soFl.FindProperty("zoomInSeconds").floatValue = 0.3f;
                    soFl.ApplyModifiedPropertiesWithoutUndo();
                }
                else Debug.LogError("[Super2Setup] Prefab thiếu SuperFlashController (chạy SuperFlashSetupTool trước).");

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
