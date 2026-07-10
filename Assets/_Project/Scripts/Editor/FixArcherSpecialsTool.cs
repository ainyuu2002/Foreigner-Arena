using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using FA.Core;
using FA.Combat;
using FA.Specials;

namespace FA.EditorTools
{
    /// <summary>
    /// TOOL SỬA 2 ĐÒN SPECIAL của Emiya Archer (idempotent — chạy lại an toàn):
    ///  1. Wire <c>SpecialController.hitboxController</c> = HitboxController cùng prefab (đang TRỐNG →
    ///     special dùng MoveData null/stale của đòn thường — nguyên nhân "chém xuyên không trúng" /
    ///     "chỉ hất 1 tí").
    ///  2. Tạo MoveData <c>Special_2_Ender</c> (hit KẾT văng theo % — causesLaunch=false, chờ
    ///     KnockbackPoint) + retune <c>Special_2</c> thành hit CARRY (nhấc nhẹ CỐ ĐỊNH: growth=0).
    ///  3. Thêm child <c>Hitbox1</c> (clone Hitbox0) với <c>moveOverride</c>=ender + đăng ký vào
    ///     HitboxController.hitboxes[1] — cơ chế "per-hitbox MoveData override" cho đòn nhiều hit.
    ///  4. Viết lại Animation Event của clip Special_2: 4 hit carry (Hitbox0, cửa sổ 2 frame — cửa sổ
    ///     1 frame cũ có thể lọt giữa 2 FixedUpdate → trượt hit) + 1 hit ender (HitboxOn(1)) +
    ///     KnockbackPoint sau hit kết + RecoveryEnd giữ nguyên.
    /// Chạy trong Editor: menu FA → Tools, hoặc CLI:
    ///   Unity.exe -batchmode -quit -executeMethod FA.EditorTools.FixArcherSpecialsTool.Run
    /// Gotcha SetDirty (memory P12): mọi thay đổi asset rời phải SetDirty + SaveAssets.
    /// </summary>
    public static class FixArcherSpecialsTool
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/Emiya Archer.prefab";
        private const string Special2Path = "Assets/_Project/Data/Emiya_Archer/Special_Move/Special_2.asset";
        private const string EnderPath = "Assets/_Project/Data/Emiya_Archer/Special_Move/Special_2_Ender.asset";
        private const string Special2AnimPath = "Assets/_Project/Animations/SampleFighter/Special_2.anim";
        private const float Fps = 60f; // clip Special_2 sample rate 60.

        [MenuItem("FA/Tools/Fix Archer Specials (P9 wiring + S2 multi-hit)")]
        public static void Run()
        {
            MoveData ender = CreateOrUpdateEnder();
            RetuneSpecial2Carry();
            RewirePrefab(ender);
            ResizeS2Hitboxes();
            RewriteSpecial2Events();
            AssetDatabase.SaveAssets();
            Debug.Log("[FixArcherSpecials] DONE — wiring + ender + hitbox size + events đã cập nhật.");
        }

        /// <summary>PHÓNG TO hitbox riêng của S2 thành CỘT DỌC CAO — nhân vật chỉ cao ~0.54u (capsule thân)
        /// trong khi vận tốc bay 5 u/s ≈ 0.08u/frame → lệch pha victim↔attacker vài frame là thoát khỏi
        /// hitbox 1×1 đặt ở chân. Hitbox cao nuốt trọn drift (KHÔNG tune vận tốc chính xác tuyệt đối được).
        /// Hitbox2 = carry (không override, ăn move hiện hành), Hitbox1 = ender. Hitbox0 (đòn thường)
        /// GIỮ NGUYÊN 1×1. Size/offset CẦN TEST — chỉnh trực tiếp trên prefab.</summary>
        private static void ResizeS2Hitboxes()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                foreach (string name in new[] { "Hitbox1", "Hitbox2" })
                {
                    Transform t = FindDeep(root.transform, name);
                    var box = t != null ? t.GetComponent<BoxCollider2D>() : null;
                    if (box == null)
                    {
                        Debug.LogError($"[FixArcherSpecials] Không thấy BoxCollider2D trên {name}.");
                        continue;
                    }
                    box.size = new Vector2(1.4f, 2.5f);   // rộng 2 bên (xoay 2 kiếm), CAO nuốt drift. CẦN TEST
                    box.offset = new Vector2(0f, 0.8f);   // tâm nâng lên: che từ dưới chân tới ~2u trên đầu.
                    box.isTrigger = true;                 // Hitbox2 user tạo đang isTrigger=0 (Awake có sửa
                                                          // runtime nhưng serialize đúng vẫn hơn).
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        /// <summary>Hit KẾT của Special 2 — văng theo % kiểu Smash (causesLaunch=false → victim chỉ lưu
        /// pending, văng tại Animation Event KnockbackPoint). Số CẦN TEST — chỉnh Inspector.
        /// ĐÃ TỒN TẠI → trả về NGUYÊN TRẠNG (không ghi đè số user đã tune trong Inspector).</summary>
        private static MoveData CreateOrUpdateEnder()
        {
            var ender = AssetDatabase.LoadAssetAtPath<MoveData>(EnderPath);
            if (ender != null) return ender; // giữ giá trị user đã tune.
            ender = ScriptableObject.CreateInstance<MoveData>();

            ender.moveId = "special2_ender";
            ender.category = MoveCategory.Special;
            ender.animatorStateId = 20;          // cùng clip Special_2 (chỉ để tra cứu — ender không kích trực tiếp).
            ender.motion = MotionInput.None;     // không nằm trong MoveSet — chỉ dùng làm override Hitbox1.
            ender.baseDamagePercent = 5f;        // CẦN TEST
            ender.launchAngle = 75f;             // văng chéo lên trước. CẦN TEST
            ender.baseKnockback = 12f;           // CẦN TEST
            ender.knockbackGrowth = 0.25f;       // scale theo % địch (Smash). CẦN TEST
            ender.causesLaunch = false;          // chờ KnockbackPoint (đường P16 chuẩn).
            ender.isSpike = false;
            ender.hitsHigh = true;
            ender.hitsLow = false;
            ender.hitstunFrames = 20;
            ender.hitstopFrames = 10;            // hit kết khựng đã hơn hit giữa. CẦN TEST
            ender.manaCostUnits = 0;             // cost đã trừ ở Special_2 khi kích đòn.

            AssetDatabase.CreateAsset(ender, EnderPath);
            EditorUtility.SetDirty(ender);
            return ender;
        }

        /// <summary>Special_2 (các hit GIỮA) = carry SF-style: nhấc CỐ ĐỊNH (growth=0). TỐC ĐỘ victim
        /// ĐỒNG TỐC attacker (5 = 5): chênh lệch vài u/s ở scale nhân vật 0.54u là trôi mất mục tiêu
        /// (drift 2 u/s ≈ 4 thân người/giây). Lệch pha dư (launch trễ hitstop+1, gravity giữa refresh)
        /// do HITBOX CAO (ResizeS2Hitboxes) nuốt — KHÔNG bù bằng vận tốc. CẦN TEST.</summary>
        private static void RetuneSpecial2Carry()
        {
            var s2 = AssetDatabase.LoadAssetAtPath<MoveData>(Special2Path);
            if (s2 == null) { Debug.LogError($"[FixArcherSpecials] Không thấy {Special2Path}"); return; }
            s2.moveId = "special2";
            s2.baseKnockback = 5f;   // ĐỒNG TỐC motionImpulses (0,5) — hitbox cao lo phần lệch. CẦN TEST
            s2.knockbackGrowth = 0f; // carry CỐ ĐỊNH, không scale % (hit kết mới scale).
            s2.hitstopFrames = 5;    // hit giữa khựng ngắn (multi-hit dồn dập). CẦN TEST
            if (s2.motionImpulses == null || s2.motionImpulses.Count == 0)
                s2.motionImpulses = new List<Vector2> { new Vector2(0f, 5f) };
            else
                s2.motionImpulses[0] = new Vector2(0f, 5f);
            EditorUtility.SetDirty(s2);
        }

        /// <summary>Wire hitboxController + dựng Hitbox1 (override=ender) trên prefab.</summary>
        private static void RewirePrefab(MoveData ender)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var sc = root.GetComponentInChildren<SpecialController>(true);
                var hc = root.GetComponentInChildren<HitboxController>(true);
                if (sc == null || hc == null)
                {
                    Debug.LogError("[FixArcherSpecials] Prefab thiếu SpecialController/HitboxController.");
                    return;
                }

                // 1) SpecialController.hitboxController = HitboxController (đường SetCurrentMove cho special).
                var soSc = new SerializedObject(sc);
                soSc.FindProperty("hitboxController").objectReferenceValue = hc;
                soSc.ApplyModifiedPropertiesWithoutUndo();

                // 2) Hitbox1 = clone Hitbox0 + moveOverride = ender.
                var soHc = new SerializedObject(hc);
                SerializedProperty arr = soHc.FindProperty("hitboxes");
                if (arr.arraySize < 1 || arr.GetArrayElementAtIndex(0).objectReferenceValue == null)
                {
                    Debug.LogError("[FixArcherSpecials] HitboxController.hitboxes[0] trống — cần Hitbox0 từ P5.");
                    return;
                }
                var hb0 = (HitboxAuthoring)arr.GetArrayElementAtIndex(0).objectReferenceValue;

                // Idempotent: tìm Hitbox1 sẵn có (cùng parent) trước khi clone.
                Transform parent = hb0.transform.parent;
                Transform t1 = parent != null ? parent.Find("Hitbox1") : null;
                HitboxAuthoring hb1 = t1 != null ? t1.GetComponent<HitboxAuthoring>() : null;
                if (hb1 == null)
                {
                    GameObject go1 = Object.Instantiate(hb0.gameObject, parent);
                    go1.name = "Hitbox1";
                    hb1 = go1.GetComponent<HitboxAuthoring>();
                }

                var soHb1 = new SerializedObject(hb1);
                soHb1.FindProperty("moveOverride").objectReferenceValue = ender;
                soHb1.ApplyModifiedPropertiesWithoutUndo();

                if (arr.arraySize < 2) arr.arraySize = 2;
                arr.GetArrayElementAtIndex(1).objectReferenceValue = hb1;
                soHc.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Viết lại events clip Special_2 (bản 8-hit của user, StopTime 1.1s/66f, lưới 4-frame):
        /// 7 hit CARRY (Hitbox0, cửa sổ 3f + nghỉ 1f — mỗi On phải có Off TRƯỚC On kế: bật-khi-đang-bật
        /// KHÔNG tạo OnTriggerEnter mới) + 1 hit ENDER CUỐI CÙNG (Hitbox1 override) + KnockbackPoint
        /// 1 frame SAU HitboxOff(1) (hit kết kịp đăng ký pending trước khi văng) + RecoveryEnd f65.</summary>
        private static void RewriteSpecial2Events()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Special2AnimPath);
            if (clip == null) { Debug.LogError($"[FixArcherSpecials] Không thấy {Special2AnimPath}"); return; }

            var ev = new List<AnimationEvent>();
            void Add(int frame, string fn, int i = 0) =>
                ev.Add(new AnimationEvent { time = frame / Fps, functionName = fn, intParameter = i });

            // 7 hit carry trên Hitbox2 (CỘT CAO riêng S2; Hitbox0 1×1 của normal giữ nguyên).
            // KHÔNG tắt-bật collider giữa các hit: tắt-bật khi 2 bên vẫn chồng nhau không tạo lại contact
            // đáng tin (Enter không re-fire, Stay theo contact cũng tắt) → chỉ ăn hit đầu. Thay vào đó:
            // collider BẬT LIÊN TỤC f33→f60, mỗi HitboxOn = RE-ARM (ResetIgnore) → OnTriggerStay của
            // physics step kế đăng ký hit mới. 1 HitboxOff duy nhất đóng chuỗi.
            int[] carryOnFrames = { 33, 37, 41, 45, 49, 53, 57 };        // 7 nhịp re-arm, lưới 4f của user.
            foreach (int f in carryOnFrames)
            {
                Add(f, "MoveImpulse");                                   // attacker nhích lên cùng nhịp hit.
                Add(f, "HitboxOn", 2);                                   // lần đầu = bật; các lần sau = re-arm.
            }
            Add(60, "HitboxOff", 2);                                     // đóng chuỗi carry MỘT lần.
            Add(61, "MoveImpulse");                                      // nhịp cuối cùng ender.
            Add(61, "HitboxOn", 1);                                      // hit KẾT = Hitbox1 (override ender).
            Add(63, "HitboxOff", 1);
            Add(64, "KnockbackPoint");                                   // 1f sau Off(1) → văng pending theo %.
            Add(65, "RecoveryEnd");                                      // giữ vị trí user đặt (f65).

            AnimationUtility.SetAnimationEvents(clip, ev.OrderBy(e => e.time).ToArray());
            EditorUtility.SetDirty(clip);
        }
    }
}
