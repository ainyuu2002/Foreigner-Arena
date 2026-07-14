using UnityEngine;
using FA.Core;

namespace FA.Items
{
    /// <summary>
    /// (P13) BUFF DAMAGE từ item — implement <see cref="IBuffSource"/> (Core) → <see cref="BuffAggregator"/>
    /// gom chung với Rage Mode (STACK với Rage bằng nhân chồng — quyết định #1: DMG Buff = NHÂN ×, CHỈ
    /// damage, KHÔNG knockback).
    ///
    /// (Hợp nhất item) Kế thừa <see cref="TimedCharacterStatus"/> (cache renderer + tint + nháy 3s cuối +
    /// countdown giây thực CHUNG). Chỉ giữ phần RIÊNG: multiplier + báo <see cref="BuffAggregator"/> mỗi lần
    /// áp/hết. KHÔNG STACK với chính nó (refresh = reset thời lượng + đổi mult, KHÔNG cộng dồn). Sống TRÊN
    /// nhân vật (không trên item đã Destroy).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ItemDamageBuff : TimedCharacterStatus, IBuffSource
    {
        private float _multiplier = 1f;

        // ===== IBuffSource (chỉ DamageDealtMult thay đổi; còn lại trung tính ×1.0/false) =====
        public float DamageDealtMult => _active ? _multiplier : 1f;
        public float KnockbackDealtMult => 1f;   // quyết định #1: KHÔNG buff knockback.
        public float HitstunTakenMult => 1f;
        public float MoveSpeedMult => 1f;
        public bool IsRageActive => false;        // item buff KHÔNG bật cờ Rage.
        public bool IsUltimateActive => false;

        /// <summary>
        /// Áp/refresh buff lên NHÂN VẬT này. Item (khi nhặt) tìm/AddComponent ItemDamageBuff trên root người
        /// nhặt rồi gọi cái này. Không-stack: luôn ghi đè mult + reset duration (base). <paramref name="tint"/>
        /// = màu báo hiệu đang có buff (item DMG đỏ).
        /// </summary>
        public void ApplyOrRefresh(float multiplier, float duration, Color tint)
        {
            _multiplier = Mathf.Max(1f, multiplier);
            ApplyOrRefresh(duration, tint); // base: cache renderer + tint + coroutine → gọi OnStatusApplied.
        }

        protected override void OnStatusApplied() => RefreshAggregator();

        protected override void OnStatusRemoved() => RefreshAggregator();

        private void RefreshAggregator()
        {
            // BuffAggregator (cùng nhân vật) gom lại để tính ngay (nguồn vừa thêm/đổi/hết giá trị).
            var agg = GetComponentInParent<BuffAggregator>();
            if (agg != null) agg.RefreshSources();
        }
    }
}
