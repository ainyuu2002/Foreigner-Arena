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
    /// TOOL DỰNG ĐÒN H "BẮN TÊN" cho Emiya Archer THEO CHUẨN MUGEN state 400 (idempotent) — đòn này
    /// làm TỪ ĐẦU (bản cũ chưa có mũi tên): normal dạng projectile (GDD P9) + biến thể GIỮ-ĐỂ-CHARGE.
    ///  - Anim 400 (41 tick): rút tên → giương cung → BẮN tại elem 12 (t21, helper 040 vx=20px/t
    ///    = 12 u/s, dmg 60 → 1.6%) + recoil VelSet x=-2. Mũi tên MUGEN VÔ HÌNH — hình ảnh là chùm
    ///    explod bám theo (406 thân tia sprite 780, 407 xoáy 730, 408 trail 770) → port = prefab
    ///    projectile flipbook 780 + TrailRenderer.
    ///  - CHARGE (anim 401, helper 045): giữ H tới frame ngắm (t16) → Animation Event **ChargeCheck**
    ///    (relay MỚI) → AttackState BeginMove(chargedVariant) swap sang clip Attack_H_Charged (StateId
    ///    40): gồng thêm ~24t (spark 772 + flash full-charge 762 + S404-0/S200-15) rồi bắn tên CHARGED
    ///    (vx=25px/t = 15 u/s, dmg 110 → 2.9%, MUGEN fall=1 → causesLaunch=true VĂNG NGAY).
    ///  - Projectile P16: tên thường causesLaunch=false (chỉ %/hitstun — KHÔNG pending, không kẹt Hurt);
    ///    KHÔNG KnockbackPoint (projectile không dùng được KP). Impact = hitEffect per-move (790 + S405-20).
    ///  - Muzzle prefab (0.1, 0.38) TRÙNG vị trí spawn MUGEN (10,-38) — dùng luôn, không sửa.
    /// Cấp phát: StateId 40; projectile variant 6 (thường) + 7 (charged); CEP slot 57-66 (GIỮ 0-56;
    /// tái dùng 42 = bụi chân, 56 = voice S200-15). Không thêm hitbox melee (tên tự mang hitbox).
    /// CLI: Unity.exe -batchmode -quit -executeMethod FA.EditorTools.NormalHSetupTool.Run
    /// </summary>
    public static class NormalHSetupTool
    {
        private const string SpriteDir = "Assets/_Project/Art/Characters/Emiya Archer/Attack_H";
        private const string VfxDir = "Assets/_Project/Art/Characters/Emiya Archer/VFX";
        private const string AudioDir = "Assets/_Project/Audio/Emiya Archer";
        private const string AnimDir = "Assets/_Project/Animations/SampleFighter";
        private const string ControllerPath = AnimDir + "/SampleFighter.controller";
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/Emiya Archer.prefab";
        private const string DataDir = "Assets/_Project/Data/Emiya_Archer";
        private const string EffectsDataDir = "Assets/_Project/Data/Emiya_Archer/Effects";
        private const string ProjectilePrefabDir = "Assets/_Project/Prefabs/Projectiles";
        private const string HitLandedChannelPath = "Assets/_Project/Data/OnHitLandedChannel.asset";

        private const float Fps = 60f;
        private const int ChargedStateId = 40;   // 39 = Ultimate_Excalibur → tiếp theo 40.
        private const int ArrowVariant = 6;      // ProjectileSpawner: 5 = ExcaliburBeam → tiếp 6/7.
        private const int ArrowChargedVariant = 7;
        private const int CepSlotBase = 57;      // 0-56 đã dùng (xem memory pipeline).
        private const int FireTick = 21;         // elem 12 (sprite 314) — helper mũi tên spawn.
        private const int ChargeCheckTick = 16;  // elem 11 (sprite 313, frame ngắm) — còn giữ H → charged.
        private const int ChargedFireTick = 24;  // clip charged: gồng 24t rồi bắn.

        // ---------------- frame data .air anim 400 (tick tích lũy, sprite 12-xxx) ----------------
        private static readonly (int tick, int sprite)[] HFrames =
        {
            (0, 0), (1, 0), (2, 305), (4, 306), (5, 307), (7, 308), (9, 309), (11, 310),
            (13, 311), (15, 312), (16, 313), (21, 314), (31, 315), (36, 316),
        };
        private const int HLen = 41;

        // Charged (từ frame ngắm trở đi — swap tại t16 của clip thường nên clip này BẮT ĐẦU ở 313):
        // giữ 313 gồng 24t (spark → flash full-charge) rồi 314 bắn, 315/316 recovery.
        private static readonly (int tick, int sprite)[] HChargedFrames =
        {
            (0, 313), (24, 314), (34, 315), (39, 316),
        };
        private const int HChargedLen = 44;

        [MenuItem("FA/Tools/Setup Archer Attack H theo MUGEN (bắn tên + charge)")]
        public static void Run()
        {
            ConfigureSpriteImports();
            Dictionary<string, EffectData> fx = CreateEffectAssets();
            AnimationClip hClip = CreateHClip();
            AnimationClip hCharged = CreateHChargedClip();
            MoveData arrowMove = CreateArrowMoveData(fx);
            MoveData arrowChargedMove = CreateArrowChargedMoveData(fx);
            // Scale ĐỀU (child beam xoay -90 — scale lệch trục sẽ méo hình): MUGEN .3,.35 → .32 / .4,.45 → .42.
            GameObject arrow = CreateArrowPrefab("HArrow", arrowMove, new Vector2(0.32f, 0.32f), 12f,
                new Color(1f, 0.9f, 0.6f, 0.6f), 0.22f);
            GameObject arrowCharged = CreateArrowPrefab("HArrowCharged", arrowChargedMove,
                new Vector2(0.42f, 0.42f), 15f, new Color(1f, 0.5f, 0.35f, 0.7f), 0.3f);
            UpdateMoveData(hClip, hCharged);
            UpdateAnimatorStates(hClip, hCharged);
            WireCharacterPrefab(fx, arrow, arrowCharged);
            AssetDatabase.SaveAssets();
            Debug.Log("[NormalHSetup] DONE — nH bắn tên theo MUGEN 400/401: tap = tên thường (1.6%), " +
                      "giữ H = charged (2.9%, văng ngay). Variant 6/7, StateId 10/40, CEP 57-66.");
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

        private static string[] Range(int group, int from, int count) =>
            Enumerable.Range(from, count).Select(i => $"{group}-{i}").ToArray();

        // ---------------------------------------------------------------- 2. VFX prefab + EffectData
        private static Dictionary<string, EffectData> CreateEffectAssets()
        {
            Color white = Color.white;
            // Explod 403: glow rút tên ở tay (784, 1t = 60fps, scale .17,.35). Sprite vẽ DỌC → xoay
            // z=-90 cho nằm NGANG (user xác nhận theo MUGEN).
            GameObject drawGlow = NormalLSetupTool.CreateTrailPrefab("H_DrawGlow_VFX",
                Range(784, 0, 15).Where(SpriteExists).ToArray(), 60f, new Vector2(0.17f, 0.35f), -90f, white, 7);
            // Explod 404: glow tụ ở đầu tên khi ngắm (758-1 + 786-0..5, 2t = 30fps, scale .2).
            GameObject aimGlow = NormalLSetupTool.CreateTrailPrefab("H_AimGlow_VFX",
                new[] { "758-1" }.Concat(Range(786, 0, 6)).ToArray(), 30f, new Vector2(0.2f, 0.2f), 0f, white, 7);
            // Explod 8002: muzzle burst khi bắn (711-0/4..9, 2t = 30fps, scale .4).
            GameObject muzzle = NormalLSetupTool.CreateTrailPrefab("H_Muzzle_VFX",
                new[] { "711-0", "711-4", "711-5", "711-6", "711-7", "711-8", "711-9" }, 30f,
                new Vector2(0.4f, 0.4f), 0f, white, 7);
            // Explod 407: xoáy tụ lực tại điểm bắn (730-0..10, ~2.4t/frame ≈ 25fps; growth .air bake trung bình).
            GameObject spiral = NormalLSetupTool.CreateTrailPrefab("H_SpiralGather_VFX",
                Range(730, 0, 11), 25f, new Vector2(0.17f, 0.17f), 0f, white, 6);
            // (Explod 7145 vệt kết sau bắn — user yêu cầu BỎ. Xoá asset cũ nếu tool từng tạo.)
            AssetDatabase.DeleteAsset("Assets/_Project/Prefabs/VFX/H_EndFlourish_VFX.prefab");
            AssetDatabase.DeleteAsset($"{EffectsDataDir}/H_End.asset");
            // Explod 516: flash full-charge (762-0..17, 1t = 60fps, scale .25).
            GameObject chargeFlash = NormalLSetupTool.CreateTrailPrefab("H_ChargeFlash_VFX",
                Range(762, 0, 18), 60f, new Vector2(0.25f, 0.25f), 0f, white, 7);
            // Explod 411: mảnh năng lượng charge (772-0..8, 2t = 30fps, scale .25).
            GameObject chargeSpark = NormalLSetupTool.CreateTrailPrefab("H_ChargeSpark_VFX",
                Range(772, 0, 9), 30f, new Vector2(0.25f, 0.25f), 0f, white, 7);
            // Anim 416: tên nổ khi CHẠM (790-0..6, 2t = 30fps, scale .4) — hitEffect per-move của mũi tên.
            GameObject impact = NormalLSetupTool.CreateTrailPrefab("H_ArrowImpact_VFX",
                Range(790, 0, 7), 30f, new Vector2(0.4f, 0.4f), 0f, white, 8);

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
                    Debug.LogError($"[NormalHSetup] {name}: thiếu AudioClip ({fx.sfxClips.Length}/{clips.Length})");
                fx.volume = vol;
                fx.scale = 1f;
                EditorUtility.SetDirty(fx);
                result[name] = fx;
                return fx;
            }

            // Vị trí theo explod MUGEN (px/100, y đảo dấu). PickClip random → mỗi slot 1 SFX.
            Make("H_Voice", null, Vector2.zero, false, new[] { "S400_0" }, 0.9f);                    // 57
            Make("H_Draw", drawGlow, new Vector2(0.14f, 0.14f), true, new[] { "S404_2" }, 1f);       // 58
            Make("H_Aim", aimGlow, new Vector2(0.24f, 0.40f), true, new string[0], 1f);              // 59
            Make("H_Fire", muzzle, new Vector2(0.30f, 0.38f), false, new[] { "S405_0" }, 1f);        // 60
            Make("H_FireWhoosh", spiral, new Vector2(0.30f, 0.38f), false, new[] { "S405_1" }, 1f);  // 61
            result["H_End"] = null;                                                                  // 62 — BỎ
            Make("H_ChargeLoop", null, Vector2.zero, false, new[] { "S404_1" }, 0.6f);               // 63
            Make("H_ChargeFull", chargeFlash, new Vector2(0.01f, 0.32f), true, new[] { "S404_0" }, 1f); // 64
            Make("H_ChargeSpark", chargeSpark, new Vector2(0.05f, 0.40f), true, new string[0], 1f);  // 65
            Make("H_FireCharged", null, Vector2.zero, false, new[] { "S405_10" }, 1f);               // 66
            Make("H_ArrowImpact", impact, Vector2.zero, false, new[] { "S405_20" }, 1f);             // hitEffect
            return result;
        }

        private static bool SpriteExists(string name) =>
            Special3SetupTool.LoadSprite(VfxDir, name) != null;

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
                if (s == null) { Debug.LogError($"[NormalHSetup] Thiếu sprite 12-{spriteIdx}"); continue; }
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

        // Anim 400: voice t0 (elem 1), glow rút tên t6 (elem 5 +1t), kéo cung t7 (elem 6), glow ngắm +
        // ChargeCheck t16 (elem 11), BẮN t21 (elem 12: helper + VelSet -2 + muzzle 8002/8005), vệt kết
        // t37 (elem 14 +1t). KHÔNG KnockbackPoint (projectile), KHÔNG cancel window (không followUps).
        private static AnimationClip CreateHClip() => BuildClip("Attack_H", HFrames, HLen, new[]
        {
            Ev(0, "PlaySfx", 57),                                      // voice S400-0
            Ev(6, "PlayVfx", 58), Ev(7, "PlaySfx", 58),                // glow tay 784 + kéo cung S404-2
            Ev(16, "PlayVfx", 59),                                     // glow ngắm 758/786 ở đầu tên
            Ev(ChargeCheckTick, "ChargeCheck"),                        // còn giữ H → swap Attack_H_Charged
            Ev(FireTick, "SpawnProjectile", ArrowVariant),
            Ev(FireTick, "MoveImpulse", 0),                            // recoil VelSet x=-2 → -1.2 u/s
            Ev(FireTick, "PlayVfx", 60), Ev(FireTick, "PlaySfx", 60),  // muzzle 711 + S405-0
            Ev(FireTick, "PlayVfx", 61), Ev(FireTick, "PlaySfx", 61),  // xoáy 730 + whoosh S405-1
            Ev(FireTick, "PlayVfx", 42),                               // bụi chân 8005 (tái dùng slot L)
            Ev(40, "RecoveryEnd"),
        });

        // Anim 401 (phần sau frame ngắm): gồng 24t — spark t4, flash full-charge + voice t16 (MUGEN
        // EnvShake + 516/411/410 + S404-0/S200-15) — rồi bắn tên CHARGED t24. Recoil mạnh hơn nhẹ.
        private static AnimationClip CreateHChargedClip() => BuildClip("Attack_H_Charged",
            HChargedFrames, HChargedLen, new[]
        {
            Ev(0, "PlaySfx", 63),                                      // hold charge S404-1
            Ev(4, "PlayVfx", 65),                                      // spark 772
            Ev(16, "PlayVfx", 64), Ev(16, "PlaySfx", 64),              // flash 762 + S404-0
            Ev(16, "PlaySfx", 56),                                     // voice gồng S200-15 (slot M)
            Ev(ChargedFireTick, "SpawnProjectile", ArrowChargedVariant),
            Ev(ChargedFireTick, "MoveImpulse", 0),
            Ev(ChargedFireTick, "PlayVfx", 60), Ev(ChargedFireTick, "PlaySfx", 66),   // muzzle + S405-10
            Ev(ChargedFireTick, "PlayVfx", 61), Ev(ChargedFireTick, "PlaySfx", 61),   // xoáy + whoosh
            Ev(ChargedFireTick, "PlayVfx", 42),
            Ev(43, "RecoveryEnd"),
        });

        // ---------------------------------------------------------------- 4. MoveData
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

        /// <summary>Tên thường (HitDef helper 040): dmg 60 → 1.6% (thang 15 = 0.4), hittime 25,
        /// pausetime 10, guardflag MA. causesLaunch=false — projectile carry P16 (chỉ %/hitstun,
        /// KHÔNG pending → không kẹt Hurt).</summary>
        private static MoveData CreateArrowMoveData(Dictionary<string, EffectData> fx)
        {
            var move = LoadOrCreate($"{DataDir}/EmiyaArcher_nH_Arrow.asset");
            move.moveId = "nH_arrow";
            move.category = MoveCategory.Normal;
            move.animatorStateId = 10;
            move.motion = MotionInput.None;
            move.button = InputButton.H;
            move.manaCostUnits = 0;
            move.manaGainMultiplier = 0.7f;           // balance: H hệ số 0.7.
            move.baseDamagePercent = 1.6f;
            move.launchAngle = 0f;
            move.baseKnockback = 0f;
            move.knockbackGrowth = 0f;
            move.isSpike = false;
            move.causesLaunch = false;
            move.role = MoveRole.Projectile;
            move.hitsLow = true;                      // guardflag MA — đỡ đứng + ngồi đều được.
            move.hitsHigh = true;
            move.hitstunFrames = 25;
            move.hitstopFrames = 10;
            move.motionImpulses = new List<Vector2>();
            move.driftSpeed = 0f;
            move.followUps = new List<FollowUp>();
            move.chargedVariant = null;
            move.hitEffect = fx["H_ArrowImpact"];
            move.blockEffect = AssetDatabase.LoadAssetAtPath<EffectData>($"{EffectsDataDir}/Block_Guard.asset");
            EditorUtility.SetDirty(move);
            return move;
        }

        /// <summary>Tên CHARGED (HitDef helper 045): dmg 110 → 2.9%, fall=1 → causesLaunch=true văng
        /// NGAY trên hit (path launcher P7). MUGEN ground.velocity (-6,-3) → góc ~27° chếch lên.</summary>
        private static MoveData CreateArrowChargedMoveData(Dictionary<string, EffectData> fx)
        {
            var move = LoadOrCreate($"{DataDir}/EmiyaArcher_nH_ArrowCharged.asset");
            move.moveId = "nH_arrow_charged";
            move.category = MoveCategory.Normal;
            move.animatorStateId = ChargedStateId;
            move.motion = MotionInput.None;
            move.button = InputButton.H;
            move.manaCostUnits = 0;
            move.manaGainMultiplier = 0.7f;
            move.baseDamagePercent = 2.9f;
            move.launchAngle = 27f;
            move.baseKnockback = 7f;
            move.knockbackGrowth = 0.12f;
            move.isSpike = false;
            move.causesLaunch = true;
            move.role = MoveRole.Projectile;
            move.hitsLow = true;
            move.hitsHigh = true;
            move.hitstunFrames = 25;
            move.hitstopFrames = 12;
            move.motionImpulses = new List<Vector2>();
            move.driftSpeed = 0f;
            move.followUps = new List<FollowUp>();
            move.chargedVariant = null;
            move.hitEffect = fx["H_ArrowImpact"];
            move.blockEffect = AssetDatabase.LoadAssetAtPath<EffectData>($"{EffectsDataDir}/Block_Guard.asset");
            EditorUtility.SetDirty(move);
            return move;
        }

        /// <summary>nH (asset CŨ — GIỮ guid để binding CombatController nguyên) + nH_Charged MỚI.
        /// Cả 2 là move THÂN NGƯỜI: dmg 0 (damage nằm ở mũi tên), chỉ recoil + spawn projectile.</summary>
        private static void UpdateMoveData(AnimationClip hClip, AnimationClip hCharged)
        {
            var charged = LoadOrCreate($"{DataDir}/EmiyaArcher_nH_Charged.asset");
            charged.moveId = "nH_charged";
            charged.category = MoveCategory.Normal;
            charged.animatorStateId = ChargedStateId;
            charged.clip = hCharged;
            charged.motion = MotionInput.None;
            charged.button = InputButton.H;
            charged.chargeFrames = 0;
            charged.chargedVariant = null;
            charged.manaCostUnits = 0;
            charged.manaGainMultiplier = 0.7f;
            charged.baseDamagePercent = 0f;
            charged.causesLaunch = false;
            charged.role = MoveRole.Projectile;
            charged.hitstunFrames = 0;
            charged.hitstopFrames = 0;
            charged.motionImpulses = new List<Vector2> { new Vector2(-1.5f, 0f) };
            charged.driftSpeed = 0f;
            charged.followUps = new List<FollowUp>();
            EditorUtility.SetDirty(charged);

            var nH = AssetDatabase.LoadAssetAtPath<MoveData>($"{DataDir}/EmiyaArcher_nH.asset");
            if (nH == null) { Debug.LogError("[NormalHSetup] Không thấy EmiyaArcher_nH.asset"); return; }
            nH.moveId = "nH";
            nH.clip = hClip;
            nH.chargeFrames = 8;                      // trần đo giữ = InputBuffer ~10f → 8 an toàn.
            nH.chargedVariant = charged;
            nH.baseDamagePercent = 0f;
            nH.causesLaunch = false;
            nH.role = MoveRole.Projectile;
            nH.hitstunFrames = 0;
            nH.hitstopFrames = 0;
            nH.motionImpulses = new List<Vector2> { new Vector2(-1.2f, 0f) };   // recoil VelSet x=-2.
            nH.driftSpeed = 0f;
            nH.followUps = new List<FollowUp>();
            EditorUtility.SetDirty(nH);
        }

        // ---------------------------------------------------------------- 5. Prefab mũi tên
        /// <summary>Mũi tên = thân tia 780 flipbook loop (anim 406, 3t = 20fps) + glow bao ngoài +
        /// TrailRenderer (thay explod trail 408 mỗi tick). Hitbox theo Clsn1 (-19,-8 → 18,8 = 0.37×0.16u
        /// thế giới, chia scale root). Bay thẳng gravity 0, despawnOnHit (khác MUGEN xuyên — gọn hơn,
        /// impact 790 hiện qua hitEffect).</summary>
        private static GameObject CreateArrowPrefab(string name, MoveData arrowMove, Vector2 scale,
                                                    float speed, Color trailColor, float trailWidth)
        {
            var hitChannel = AssetDatabase.LoadAssetAtPath<HitEventChannelSO>(HitLandedChannelPath);
            var root = new GameObject(name);
            try
            {
                root.layer = LayerMask.NameToLayer(GameLayers.ProjectileP1Name);
                root.transform.localScale = new Vector3(scale.x, scale.y, 1f);   // explod 406 scale.

                // Sprite 780 vẽ DỌC, đầu mũi hướng XUỐNG — xoay z=+90 cho đầu mũi ra TRƯỚC (user test:
                // z=-90 bị ngược đầu/đuôi). Bake rotation trên CHILD (root giữ identity: collider/velocity/trail).
                Sprite[] beamFrames = { LoadVfx("780-1"), LoadVfx("780-3"), LoadVfx("780-2") };
                var beam = new GameObject("Beam");
                beam.transform.SetParent(root.transform, false);
                beam.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                var sr = beam.AddComponent<SpriteRenderer>();
                sr.sprite = beamFrames[0];
                sr.sortingOrder = 6;
                AddFlipbookLoop(beam, beamFrames, 20f);

                // Lớp glow bao ngoài cho dày (bài học Super2 "beam chưa lực").
                var glow = new GameObject("Glow");
                glow.transform.SetParent(root.transform, false);
                glow.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                glow.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
                var srG = glow.AddComponent<SpriteRenderer>();
                srG.sprite = beamFrames[0];
                srG.color = new Color(1f, 1f, 1f, 0.4f);
                srG.sortingOrder = 5;
                AddFlipbookLoop(glow, beamFrames, 20f);

                var rb = root.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;

                var body = root.AddComponent<BoxCollider2D>();
                body.isTrigger = true;
                body.size = new Vector2(1.25f, 0.45f);

                var proj = root.AddComponent<Projectile>();
                var soP = new SerializedObject(proj);
                soP.FindProperty("lifetime").floatValue = 2.5f;
                soP.FindProperty("despawnOnHit").boolValue = true;
                soP.ApplyModifiedPropertiesWithoutUndo();

                var launch = root.AddComponent<ProjectileLaunchSetup>();
                var soL = new SerializedObject(launch);
                soL.FindProperty("launchUpSpeed").floatValue = 0f;
                soL.FindProperty("spinDegPerSec").floatValue = 0f;
                soL.FindProperty("overrideForwardSpeed").floatValue = speed;   // 040 vx=20 / 045 vx=25 px/t.
                soL.FindProperty("upSpeedJitter").floatValue = 0f;
                soL.ApplyModifiedPropertiesWithoutUndo();

                var trail = root.AddComponent<TrailRenderer>();
                trail.time = 0.25f;
                trail.startWidth = trailWidth;
                trail.endWidth = 0f;
                trail.material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                trail.startColor = trailColor;
                trail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
                trail.sortingOrder = 4;

                var hitboxGo = new GameObject("Hitbox");
                hitboxGo.transform.SetParent(root.transform, false);
                hitboxGo.layer = LayerMask.NameToLayer(GameLayers.HitboxName);
                var hbBox = hitboxGo.AddComponent<BoxCollider2D>();
                hbBox.isTrigger = true;
                hbBox.size = new Vector2(0.37f / scale.x, 0.16f / scale.y);   // Clsn1 anim 405, world-size.
                var hb = hitboxGo.AddComponent<FA.Combat.HitboxAuthoring>();
                var so = new SerializedObject(hb);
                so.FindProperty("hitChannel").objectReferenceValue = hitChannel;
                so.FindProperty("moveOverride").objectReferenceValue = arrowMove;   // 1 move cố định.
                so.FindProperty("box").objectReferenceValue = hbBox;
                so.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, $"{ProjectilePrefabDir}/{name}.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Sprite LoadVfx(string n)
        {
            Sprite s = Special3SetupTool.LoadSprite(VfxDir, n);
            if (s == null) Debug.LogError($"[NormalHSetup] Thiếu sprite VFX {n}");
            return s;
        }

        private static void AddFlipbookLoop(GameObject go, Sprite[] frames, float fps)
        {
            var flip = go.AddComponent<FlipbookOneShot>();
            var so = new SerializedObject(flip);
            SerializedProperty pFrames = so.FindProperty("frames");
            pFrames.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
                pFrames.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            so.FindProperty("framesPerSecond").floatValue = fps;
            so.FindProperty("loop").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- 6. Animator
        private static void UpdateAnimatorStates(AnimationClip hClip, AnimationClip hCharged)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) { Debug.LogError("[NormalHSetup] Không thấy controller"); return; }
            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            AnimatorState h = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Attack_H");
            if (h == null) Debug.LogError("[NormalHSetup] Không thấy state Attack_H");
            else h.motion = hClip;

            AnimatorState hc = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Attack_H_Charged");
            if (hc == null) hc = sm.AddState("Attack_H_Charged");
            hc.motion = hCharged;
            hc.writeDefaultValues = true;
            AnimatorStateTransition tr = sm.anyStateTransitions.FirstOrDefault(t => t.destinationState == hc);
            if (tr == null) tr = sm.AddAnyStateTransition(hc);
            tr.hasExitTime = false;
            tr.hasFixedDuration = true;
            tr.duration = 0f;
            tr.canTransitionToSelf = false;   // gotcha Can-Transition-To-Self.
            tr.conditions = new[]
            {
                new AnimatorCondition { mode = AnimatorConditionMode.Equals, parameter = "StateId",
                                        threshold = ChargedStateId }
            };
            EditorUtility.SetDirty(controller);
        }

        // ---------------------------------------------------------------- 7. Prefab: CEP + spawner
        private static void WireCharacterPrefab(Dictionary<string, EffectData> fx,
                                                GameObject arrow, GameObject arrowCharged)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var cep = root.GetComponent<CharacterEffectPlayer>();
                if (cep == null) { Debug.LogError("[NormalHSetup] Prefab thiếu CharacterEffectPlayer!"); }
                else
                {
                    var so = new SerializedObject(cep);
                    SerializedProperty arr = so.FindProperty("effects");
                    // Slot 62 (H_End cũ) = null — user bỏ vệt kết; GIỮ vị trí để 63-66 không xô lệch.
                    string[] slots = { "H_Voice", "H_Draw", "H_Aim", "H_Fire", "H_FireWhoosh",
                                       "H_End", "H_ChargeLoop", "H_ChargeFull", "H_ChargeSpark",
                                       "H_FireCharged" };
                    if (arr.arraySize < CepSlotBase + slots.Length) arr.arraySize = CepSlotBase + slots.Length;
                    for (int i = 0; i < slots.Length; i++)
                        arr.GetArrayElementAtIndex(CepSlotBase + i).objectReferenceValue =
                            fx.TryGetValue(slots[i], out EffectData e) ? e : null;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                var spawner = root.GetComponent<ProjectileSpawner>();
                if (spawner == null) { Debug.LogError("[NormalHSetup] Prefab thiếu ProjectileSpawner!"); }
                else
                {
                    var so = new SerializedObject(spawner);
                    SerializedProperty arr = so.FindProperty("projectilePrefabs");
                    if (arr.arraySize < ArrowChargedVariant + 1) arr.arraySize = ArrowChargedVariant + 1;
                    arr.GetArrayElementAtIndex(ArrowVariant).objectReferenceValue =
                        arrow.GetComponent<Projectile>();
                    arr.GetArrayElementAtIndex(ArrowChargedVariant).objectReferenceValue =
                        arrowCharged.GetComponent<Projectile>();
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
