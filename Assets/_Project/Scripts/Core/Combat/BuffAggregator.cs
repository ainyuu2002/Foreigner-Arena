using System.Collections.Generic;
using UnityEngine;

namespace FA.Core
{
    /// <summary>
    /// (P13) MẶT TIỀN (facade) gom MỌI <see cref="IBuffSource"/> trên nhân vật → expose multiplier TỔNG qua
    /// <see cref="IBuffContext"/>. Đặt ở FA.Core (logic thuần, cả Specials/Items/Combat đều ref Core) — sạch
    /// nhất cho yêu cầu đa-nguồn buff (research §4, Phương án B đã chốt).
    ///
    /// AGGREGATION:
    ///  • DamageDealtMult / KnockbackDealtMult / HitstunTakenMult / MoveSpeedMult = TÍCH (×) các nguồn
    ///    (Rage ×1.2 × ItemBuff ×1.5 = ×1.8 — multiplicative, chuẩn fighting). Mỗi nguồn không-active = ×1.0
    ///    → vô hại.
    ///  • IsRageActive / IsUltimateActive = OR các nguồn (bất kỳ nguồn bật cờ → cờ tổng bật).
    ///
    /// BẢO TOÀN P10: khi CHỈ có Rage Mode (1 nguồn), tích = đúng giá trị Rage cũ; khi không nguồn nào active,
    /// tổng = ×1.0 / false → consumer (HitboxAuthoring/HitReaction/Hitstop/Motor) nhân vô hại y như trước.
    /// Consumer KHÔNG đổi: vẫn GetComponent&lt;IBuffContext&gt;() — chỉ là component trỏ vào BuffAggregator
    /// thay cho RageModeController. Nếu prefab CHƯA gắn BuffAggregator → GetComponent trả null → null-safe ×1.0.
    ///
    /// NGUỒN ĐỘNG (runtime AddComponent): ItemDamageBuff (P13) gắn khi nhặt buff. <see cref="RefreshSources"/>
    /// quét lại danh sách nguồn (ItemDamageBuff gọi sau khi AddComponent). Mặc định cũng quét Awake +
    /// fallback "quét lười mỗi truy vấn nếu chưa cache" để không phụ thuộc thứ tự AddComponent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuffAggregator : MonoBehaviour, IBuffContext
    {
        private readonly List<IBuffSource> _sources = new List<IBuffSource>(4);
        private bool _scanned;

        private void Awake() => RefreshSources();

        /// <summary>Quét lại mọi <see cref="IBuffSource"/> trên object này + children (Rage + item buff).
        /// Gọi ở Awake + khi 1 nguồn được THÊM/BỎ runtime (ItemDamageBuff gọi sau AddComponent/khi hết).</summary>
        public void RefreshSources()
        {
            _sources.Clear();
            GetComponentsInChildren(true, _sources);
            _scanned = true;
        }

        private void EnsureScanned()
        {
            if (!_scanned) RefreshSources();
        }

        private float Product(System.Func<IBuffSource, float> selector)
        {
            EnsureScanned();
            float result = 1f;
            for (int i = 0; i < _sources.Count; i++)
            {
                var s = _sources[i];
                if (s == null) continue;
                result *= selector(s);
            }
            return result;
        }

        private bool AnyFlag(System.Func<IBuffSource, bool> selector)
        {
            EnsureScanned();
            for (int i = 0; i < _sources.Count; i++)
            {
                var s = _sources[i];
                if (s != null && selector(s)) return true;
            }
            return false;
        }

        // ===== IBuffContext (TÍCH multiplier / OR cờ — vô hại khi không nguồn nào active) =====
        public float DamageDealtMult => Product(s => s.DamageDealtMult);
        public float KnockbackDealtMult => Product(s => s.KnockbackDealtMult);
        public float HitstunTakenMult => Product(s => s.HitstunTakenMult);
        public float MoveSpeedMult => Product(s => s.MoveSpeedMult);
        public bool IsRageActive => AnyFlag(s => s.IsRageActive);
        public bool IsUltimateActive => AnyFlag(s => s.IsUltimateActive);
    }
}
