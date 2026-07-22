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
    /// TOOL DỰNG ULTIMATE 2 "EXCALIBUR" cho Emiya Archer (idempotent) từ MUGEN state 12000 —
    /// ĐÒN KẾT TRONG UBW: đang install (nộ chưa drain hết) ấn nút NỘ lần nữa →
    ///  1. TRACE (anim 12000, trong SUPER FLASH — freeze + portrait 9000-50): triệu hồi kiếm thánh,
    ///     voice announce S12000-0, glow 796, vòng sáng đất 840.
    ///  2. CHARGE (anim 12001, thế giới sống lại, Archer BẤT TỬ — MUGEN NotHitBy): trụ sáng 15,10x
    ///     + hum S12005-0, cuối charge bùng tia sáng 849/851/786 — địch có ~2.3s để chạy/nhảy né.
    ///  3. CHÉM (anim 12002): spawn BEAM KIẾM QUANG đứng tại chỗ dài ~6u (sprite 847 + cánh sáng 832
    ///     + ring 711 + sparkle 745) — tái dùng ProjectileDrill: 10 hit khoan nhỏ giọt (ghim victim)
    ///     → flash kết 732/820 + 2 hit trong flash → hit CUỐI văng cực mạnh (block VÔ DỤNG — MUGEN
    ///     guardflag rỗng → role BlockBreak). Archer giữ pose chém tới khi beam xong (PoseLock).
    ///  4. RECOVERY (anim 12003) → UltimateState.Exit tự ForceEnd UBW ("dùng xong = hết nộ luôn").
    /// CLI: Unity.exe -batchmode -quit -executeMethod FA.EditorTools.UltimateExcaliburSetupTool.Run
    /// </summary>
    public static class UltimateExcaliburSetupTool
    {
        private const string SpriteDir = "Assets/_Project/Art/Characters/Emiya Archer/Ultimate2";
        private const string PortraitDir = "Assets/_Project/Art/Characters/Emiya Archer/Portrait";
        private const string VfxDir = "Assets/_Project/Art/Characters/Emiya Archer/VFX";
        private const string ClipPath = "Assets/_Project/Animations/SampleFighter/Ultimate_Excalibur.anim";
        private const string ControllerPath = "Assets/_Project/Animations/SampleFighter/SampleFighter.controller";
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/Emiya Archer.prefab";
        private const string DataDir = "Assets/_Project/Data/Emiya_Archer/Ultimate";
        private const string EffectsDataDir = "Assets/_Project/Data/Emiya_Archer/Effects";
        private const string BeamPrefabPath = "Assets/_Project/Prefabs/Projectiles/ExcaliburBeam.prefab";
        private const string HitLandedChannelPath = "Assets/_Project/Data/OnHitLandedChannel.asset";
        private const string HitConfirmedChannelPath = "Assets/_Project/Data/OnHitConfirmedChannel.asset";

        private const int StateId = 39;        // 35=S3, 36=Super1, 37=Super2, 38=Ultimate_Chant.
        private const float Fps = 60f;
        private const int PortraitIndex = 3;   // 0=9000-10, 1=9000-20, 2=9000-12 → Excalibur = 9000-50.
        private const int BeamVariant = 5;     // ProjectileSpawner: 0-3 = kiếm/blade, 4 = Super2Arrow.
        private const int EffectSlotBase = 24; // CharacterEffectPlayer: 0-23 đã cấp (xem memory).

        // Timeline tick 60fps — bám AIR gốc, nén SuperPause 260t thành flash hết đoạn trace:
        private const int FlashEndTick = 95;    // hết anim 12000 (trace) — thế giới sống lại khi charge.
        private const int SwingTick = 246;      // sprite 812 (chém xuống) — beam spawn tại đây.
        private const int RecoveryEndTick = 462;

        private static readonly (int tick, string sprite)[] Frames = BuildFrames();

        private static (int, string)[] BuildFrames()
        {
            var list = new List<(int, string)>();
            int t = 0;
            void Add(int sprite, int dur) { list.Add((t, $"12-{sprite}")); t += dur; }

            // ---- TRACE (anim 12000): 611-620 vươn tay + 780-791 kiếm hiện dần. t0..94.
            for (int s = 611; s <= 618; s++) Add(s, 3);
            Add(619, 5); Add(620, 3);
            Add(780, 20); Add(781, 5); Add(782, 5); Add(783, 5); Add(784, 5); Add(785, 10);
            Add(786, 2); Add(787, 2); Add(788, 2); Add(789, 2); Add(790, 3); Add(791, 2);

            // ---- CHARGE (anim 12001): intro 792-795 + 3 vòng loop nạp (796-806, bỏ 800). t95..230.
            for (int s = 792; s <= 795; s++) Add(s, 4);
            int[] chargeLoop = { 796, 797, 798, 799, 801, 802, 803, 804, 805, 806 };
            for (int round = 0; round < 3; round++)
                foreach (int s in chargeLoop) Add(s, 4);

            // ---- CHÉM (anim 12002): giơ kiếm 808-811 → chém 812 (t246 = SwingTick) → giữ pose
            //      813/815 xen kẽ ~1.7s trong lúc beam khoan (MUGEN cũng loop 813/815).
            Add(808, 3); Add(809, 4); Add(810, 6); Add(811, 2); Add(812, 2);
            Add(813, 5); Add(815, 5);
            for (int i = 0; i < 20; i++) Add(i % 2 == 0 ? 813 : 815, 5);

            // ---- RECOVERY (anim 12003): hạ kiếm 813-833. Kết t464.
            Add(813, 6); Add(814, 10);
            for (int s = 815; s <= 822; s++) Add(s, 6);
            for (int s = 823; s <= 828; s++) Add(s, 2);
            Add(829, 10);
            for (int s = 830; s <= 833; s++) Add(s, 5);
            return list.ToArray();
        }

        [MenuItem("FA/Tools/Setup Archer Ultimate 2 (Excalibur trong UBW)")]
        public static void Run()
        {
            EnsureDataFolder();
            ConfigureSpriteImports();
            AnimationClip clip = CreateClip();
            MoveData main = CreateMainMoveData(clip);
            MoveData carry = CreateCarryMoveData(clip);
            MoveData ender = CreateEnderMoveData(clip);
            Dictionary<string, EffectData> fx = UltimateExcaliburFxBuilder.CreateEffectAssets();
            GameObject beam = CreateBeamPrefab(carry, ender, fx["Ulti2_BeamEnd"]);
            AssignHitEffects(carry, ender);
            AddAnimatorState(clip);
            WireCharacterPrefab(beam, main, fx);
            AssetDatabase.SaveAssets();
            Debug.Log("[Ulti2Setup] DONE — Excalibur trong UBW: ấn nộ lần 2 khi install, flash 9000-50, " +
                      "trace→charge (bất tử) → beam 13 hit khoan+flash kết văng, xong tự ForceEnd UBW.");
        }

        private static void EnsureDataFolder()
        {
            if (!AssetDatabase.IsValidFolder(DataDir))
                AssetDatabase.CreateFolder("Assets/_Project/Data/Emiya_Archer", "Ultimate");
        }

        private static void ConfigureSpriteImports()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D",
                         new[] { SpriteDir, VfxDir, PortraitDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                if (imp == null) continue;
                var settings = new TextureImporterSettings();
                imp.ReadTextureSettings(settings);
                settings.textureType = TextureImporterType.Sprite;
                settings.spriteMode = (int)SpriteImportMode.Single;
                settings.spritePixelsPerUnit = 100f;
                settings.spriteAlignment = (int)(path.StartsWith(SpriteDir)
                    ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
                settings.mipmapEnabled = false;
                imp.SetTextureSettings(settings);
                if (path.StartsWith(PortraitDir)) imp.maxTextureSize = 2048;   // artwork giữ nét.
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
                if (s == null) { Debug.LogError($"[Ulti2Setup] Thiếu sprite {spriteName}"); continue; }
                keys.Add(new ObjectReferenceKeyframe { time = tick / Fps, value = s });
            }
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());

            AnimationEvent Ev(int tick, string fn, int i = 0) =>
                new AnimationEvent { time = tick / Fps, functionName = fn, intParameter = i };

            // VŨ ĐẠO VFX (user chốt round 2): PlaySfx và PlayVfx là 2 event TÁCH BẠCH — sound trong
            // EffectData chỉ phát khi có PlaySfx (round 1 thiếu → S6000/S404/hum câm).
            var events = new[]
            {
                Ev(0, "SuperFlashStart", PortraitIndex),   // freeze + zoom + tối màn + cut-in 9000-50.
                Ev(1, "PlaySfx", 24),    // voice announce (S12000-0, ~6s vang xuyên charge)
                Ev(21, "PlaySfx", 25),   // tiếng kiếm hiện S6000-0 (MUGEN 12000 elem 8 — CHỈ sound)
                Ev(88, "PlayVfx", 26),   // HÀO QUANG vận khí 840 bao quanh người (loop tới lúc chém)
                Ev(88, "PlaySfx", 26),   // bùng khí S404-2 (MUGEN elem 20)
                Ev(FlashEndTick, "SuperFlashEnd"),
                Ev(111, "PlayVfx", 27),  // kiếm trắng glow 15-100 đính kiếm (12010, từ khi 12001 vào loop)
                Ev(111, "PlaySfx", 27),  // hum vận khí S12005-0
                // (12017 cột sáng dọc kiếm — user BỎ round 3, slot 33 trống)
                Ev(185, "PlayVfx", 28),  // tia bùng 849 (12016) — 46t, KẾT ĐÚNG lúc loop vận xong t231
                Ev(185, "PlaySfx", 28),  // power-up S12005-5
                Ev(205, "PlaySfx", 29),  // năng lượng dâng đỉnh (S12005-6)
                Ev(213, "PlayVfx", 34),  // sparkle 786 (12018) — 18t, kết đúng t231
                Ev(231, "PlayVfx", 35),  // kiếm trắng theo hành động vung (12011, loop đồng pha 813/815)
                Ev(244, "PlayVfx", 36),  // hào quang 840 XOAY theo đường kiếm vung xuống (user chốt)
                Ev(SwingTick, "SpawnProjectile", BeamVariant),   // 12020 beam = luồng đánh/hitbox 12040
                Ev(SwingTick, "PlaySfx", 30),   // BẮN BEAM (S12005-11)
                Ev(SwingTick, "PlayVfx", 25),   // vệt sáng theo tay trên đường chém (7145/796)
                Ev(SwingTick + 10, "PlaySfx", 31),   // beam gầm (S12005-12, MUGEN t10)
                Ev(358, "PlayVfx", 37),  // kiếm trắng TAN 12012 (đầu recovery, hết frame ≈ hết đòn)
                Ev(364, "PlaySfx", 32),  // whoosh hạ kiếm (S1005-10, MUGEN 12003 elem 10)
                Ev(434, "PlaySfx", 25),  // tiếng kiếm tan S6000-0 (MUGEN 12003 elem 21 — chỉ sound)
                Ev(RecoveryEndTick, "RecoveryEnd"),
            };
            AnimationUtility.SetAnimationEvents(clip, events.OrderBy(e => e.time).ToArray());

            AssetDatabase.DeleteAsset(ClipPath);
            AssetDatabase.CreateAsset(clip, ClipPath);
            return clip;
        }

        private static MoveData CreateMainMoveData(AnimationClip clip)
        {
            var move = LoadOrCreate($"{DataDir}/Ultimate_Excalibur.asset");
            move.moveId = "ultimate_excalibur";
            move.category = MoveCategory.Super;
            move.animatorStateId = StateId;
            move.clip = clip;
            move.motion = MotionInput.None;
            move.button = InputButton.Rage;
            move.manaCostUnits = 0;                   // giá = TOÀN BỘ nộ còn lại (ForceEnd UBW khi xong).
            move.baseDamagePercent = 0f;              // damage nằm ở beam (carry/ender).
            move.causesLaunch = false;
            move.role = MoveRole.BlockBreak;
            move.hitstunFrames = 0;
            move.hitstopFrames = 0;
            move.motionImpulses = new List<Vector2>();
            move.followUps = new List<FollowUp>();
            EditorUtility.SetDirty(move);
            return move;
        }

        /// <summary>Hit KHOAN nhỏ giọt của beam (MUGEN 19dmg/tick, persistent=8): % nhỏ, hitstun >
        /// rehit giữ victim suốt chuỗi, BlockBreak (MUGEN guardflag rỗng = không đỡ được).</summary>
        private static MoveData CreateCarryMoveData(AnimationClip clip)
        {
            var move = LoadOrCreate($"{DataDir}/Ultimate_Excalibur_Carry.asset");
            move.moveId = "ultimate_excalibur_carry";
            move.category = MoveCategory.Super;
            move.animatorStateId = StateId;
            move.clip = clip;
            move.motion = MotionInput.None;
            move.button = InputButton.Rage;
            move.manaCostUnits = 0;
            move.baseDamagePercent = 1.2f;            // 12 hit ≈ 14.4%. CẦN TEST.
            move.launchAngle = 0f;
            move.baseKnockback = 0f;
            move.knockbackGrowth = 0f;
            move.isSpike = false;
            move.causesLaunch = false;                // carry projectile: KHÔNG pending (pattern P16).
            move.role = MoveRole.BlockBreak;
            move.hitsLow = true;                      // beam quét sát đất — ngồi cũng dính.
            move.hitsHigh = true;
            move.hitstunFrames = 22;                  // > rehitFrames (8) → giữ victim tới hit kế.
            move.hitstopFrames = 2;
            move.motionImpulses = new List<Vector2>();
            move.followUps = new List<FollowUp>();
            EditorUtility.SetDirty(move);
            return move;
        }

        private static MoveData CreateEnderMoveData(AnimationClip clip)
        {
            var move = LoadOrCreate($"{DataDir}/Ultimate_Excalibur_Ender.asset");
            move.moveId = "ultimate_excalibur_ender";
            move.category = MoveCategory.Super;
            move.animatorStateId = StateId;
            move.clip = clip;
            move.motion = MotionInput.None;
            move.button = InputButton.Rage;
            move.manaCostUnits = 0;
            move.baseDamagePercent = 6f;              // tổng đòn ≈ 20%. CẦN TEST.
            move.launchAngle = 38f;                   // văng chéo lên xa — % cao có thể kill.
            move.baseKnockback = 13f;
            move.knockbackGrowth = 0.16f;
            move.isSpike = false;
            move.causesLaunch = true;                 // ENDER: văng NGAY trên hit (path launcher P7).
            move.role = MoveRole.BlockBreak;
            move.hitsLow = true;
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

        /// <summary>Beam kiếm quang ĐỨNG TẠI CHỖ trước mặt caster (MUGEN helper 12015, anim 12040 =
        /// Clsn1 nở dần tới ~300px): tái dùng ProjectileDrill (khoan → flash kết → văng) với speed ≈ 0.
        /// ownHitRadius PHẢI to (7u) — hitbox dài 6u, contact point xa gốc vẫn là hit CỦA beam.</summary>
        private static GameObject CreateBeamPrefab(MoveData carryMove, MoveData enderMove,
                                                   EffectData beamEndFx)
        {
            var hitChannel = AssetDatabase.LoadAssetAtPath<HitEventChannelSO>(HitLandedChannelPath);
            var root = new GameObject("ExcaliburBeam");
            try
            {
                root.layer = LayerMask.NameToLayer(GameLayers.ProjectileP1Name);
                // Sprite 847 = 5.94×3.42u → ép dẹt: beam thế giới ~5.3u dài, ~1.9u cao. CẦN TEST.
                root.transform.localScale = new Vector3(0.9f, 0.55f, 1f);

                var rb = root.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;

                var body = root.AddComponent<BoxCollider2D>();
                body.isTrigger = true;
                body.size = new Vector2(1f, 1f);

                var proj = root.AddComponent<Projectile>();
                var soP = new SerializedObject(proj);
                soP.FindProperty("despawnOnHit").boolValue = false;   // drill tự quản vòng đời.
                soP.FindProperty("lifetime").floatValue = 3.5f;       // hụt (không hit) tự tan + nhả pose.
                soP.ApplyModifiedPropertiesWithoutUndo();

                var drill = root.AddComponent<ProjectileDrill>();
                var soD = new SerializedObject(drill);
                soD.FindProperty("carryMove").objectReferenceValue = carryMove;
                soD.FindProperty("enderMove").objectReferenceValue = enderMove;
                soD.FindProperty("explosionEffect").objectReferenceValue = beamEndFx;
                soD.FindProperty("hitConfirmedChannel").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<HitEventChannelSO>(HitConfirmedChannelPath);
                soD.FindProperty("carryHits").intValue = 10;     // 13 hit: 10 khoan + 2 flash + 1 văng. CẦN TEST.
                soD.FindProperty("blastHits").intValue = 2;
                soD.FindProperty("rehitFrames").intValue = 8;
                soD.FindProperty("drillSpeed").floatValue = 0f;  // beam đứng im tuyệt đối.
                soD.FindProperty("ownHitRadius").floatValue = 7f;   // phủ trọn chiều dài beam!
                soD.ApplyModifiedPropertiesWithoutUndo();

                var launch = root.AddComponent<ProjectileLaunchSetup>();
                var soL = new SerializedObject(launch);
                soL.FindProperty("launchUpSpeed").floatValue = 0f;
                soL.FindProperty("spinDegPerSec").floatValue = 0f;
                // 0 = "giữ speed spawner" (3 u/s) → beam TRÔI. Dùng 0.01 ≈ đứng im tới hit đầu
                // (drill set velocity = 0 khi bắt đầu khoan vì drillSpeed = 0).
                soL.FindProperty("overrideForwardSpeed").floatValue = 0.01f;
                soL.FindProperty("upSpeedJitter").floatValue = 0f;
                soL.ApplyModifiedPropertiesWithoutUndo();

                UltimateExcaliburFxBuilder.AddBeamVisualLayers(root);

                var hitboxGo = new GameObject("Hitbox");
                hitboxGo.transform.SetParent(root.transform, false);
                hitboxGo.layer = LayerMask.NameToLayer(GameLayers.HitboxName);
                var hbBox = hitboxGo.AddComponent<BoxCollider2D>();
                hbBox.isTrigger = true;
                hbBox.size = new Vector2(6.6f, 3f);      // × scale root = 5.94×1.65u thế giới.
                hbBox.offset = new Vector2(3f, 0f);      // beam trải VỀ TRƯỚC từ gốc (muzzle).
                var hb = hitboxGo.AddComponent<FA.Combat.HitboxAuthoring>();
                var so = new SerializedObject(hb);
                so.FindProperty("hitChannel").objectReferenceValue = hitChannel;
                so.FindProperty("moveOverride").objectReferenceValue = null;   // drill swap carry→ender.
                so.FindProperty("box").objectReferenceValue = hbBox;
                so.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, BeamPrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void AssignHitEffects(MoveData carry, MoveData ender)
        {
            var guard = AssetDatabase.LoadAssetAtPath<EffectData>($"{EffectsDataDir}/Block_Guard.asset");
            var medium = AssetDatabase.LoadAssetAtPath<EffectData>($"{EffectsDataDir}/Hit_Medium1.asset");
            var heavy = AssetDatabase.LoadAssetAtPath<EffectData>($"{EffectsDataDir}/Hit_Heavy2.asset");
            carry.hitEffect = medium;                 // spark mỗi tick beam.
            carry.blockEffect = guard;
            ender.hitEffect = heavy;                  // hit văng — flash kết do drill spawn rồi.
            ender.blockEffect = guard;
            EditorUtility.SetDirty(carry);
            EditorUtility.SetDirty(ender);
        }

        private static void AddAnimatorState(AnimationClip clip)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) { Debug.LogError("[Ulti2Setup] Không thấy controller"); return; }
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState state = sm.states.Select(s => s.state)
                .FirstOrDefault(s => s.name == "Ultimate_Excalibur");
            if (state == null) state = sm.AddState("Ultimate_Excalibur");
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

        private static void WireCharacterPrefab(GameObject beam, MoveData main,
                                                Dictionary<string, EffectData> fx)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                // CharacterEffectPlayer: GIỮ slot 0-23, thêm 24-32 cho Excalibur.
                var cep = root.GetComponent<FA.Effects.CharacterEffectPlayer>();
                if (cep == null) { Debug.LogError("[Ulti2Setup] Prefab thiếu CharacterEffectPlayer."); return; }
                var soCep = new SerializedObject(cep);
                SerializedProperty pEffects = soCep.FindProperty("effects");
                var extra = new[]
                {
                    fx["Ulti2_Announce"],      // 24
                    fx["Ulti2_SlashTrail"],    // 25 — vệt theo tay (796) + sound S6000-0
                    fx["Ulti2_Aura"],          // 26 — hào quang vận khí (840) + S404-2
                    fx["Ulti2_SwordGlow"],     // 27 — kiếm trắng đính kiếm (15-100) + hum
                    fx["Ulti2_PowerRays"],     // 28 — tia bùng cuối vận (849) + S12005-5
                    fx["Ulti2_PowerRise"],     // 29
                    fx["Ulti2_BeamFire"],      // 30
                    fx["Ulti2_BeamRoar"],      // 31
                    fx["Ulti2_Sheathe"],       // 32
                    fx["Ulti2_SwordColumn"],   // 33 — TRỐNG (12017 bỏ, giữ chỗ không xáo slot sau)
                    fx["Ulti2_ChargeSpark"],   // 34 — sparkle cuối vận (786)
                    fx["Ulti2_SwordLoop"],     // 35 — kiếm trắng theo vung (12011)
                    fx["Ulti2_SlashArc"],      // 36 — hào quang xoay theo đường chém (840)
                    fx["Ulti2_SwordFade"],     // 37 — kiếm trắng tan (12012)
                };
                if (pEffects.arraySize < EffectSlotBase + extra.Length)
                    pEffects.arraySize = EffectSlotBase + extra.Length;
                for (int i = 0; i < extra.Length; i++)
                    pEffects.GetArrayElementAtIndex(EffectSlotBase + i).objectReferenceValue = extra[i];
                soCep.ApplyModifiedPropertiesWithoutUndo();

                // ProjectileSpawner: variant 5 = beam Excalibur.
                var spawner = root.GetComponent<ProjectileSpawner>();
                if (spawner != null)
                {
                    var soSp = new SerializedObject(spawner);
                    SerializedProperty pPrefabs = soSp.FindProperty("projectilePrefabs");
                    if (pPrefabs.arraySize < BeamVariant + 1) pPrefabs.arraySize = BeamVariant + 1;
                    pPrefabs.GetArrayElementAtIndex(BeamVariant).objectReferenceValue =
                        beam.GetComponent<Projectile>();
                    soSp.ApplyModifiedPropertiesWithoutUndo();
                }
                else Debug.LogError("[Ulti2Setup] Prefab thiếu ProjectileSpawner!");

                // SuperFlashController: portraits[3] = 9000-50 (artwork hexagon charge).
                var flash = root.GetComponent<FA.Effects.SuperFlashController>();
                if (flash != null)
                {
                    var soFl = new SerializedObject(flash);
                    SerializedProperty pPortraits = soFl.FindProperty("portraits");
                    if (pPortraits.arraySize < PortraitIndex + 1) pPortraits.arraySize = PortraitIndex + 1;
                    pPortraits.GetArrayElementAtIndex(PortraitIndex).objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<Sprite>($"{PortraitDir}/9000-50.png");
                    soFl.ApplyModifiedPropertiesWithoutUndo();
                }
                else Debug.LogError("[Ulti2Setup] Prefab thiếu SuperFlashController!");

                // DefenseController: ultimateMove — kích bằng nút Nộ khi UBW đang install.
                var def = root.GetComponent<DefenseController>();
                if (def != null)
                {
                    var soDef = new SerializedObject(def);
                    soDef.FindProperty("ultimateMove").objectReferenceValue = main;
                    soDef.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(def);
                }
                else Debug.LogError("[Ulti2Setup] Prefab thiếu DefenseController (chạy UltimateSetupTool trước)!");

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
