using UnityEngine;
using FA.Core;

namespace FA.Items
{
    /// <summary>Buff sát thương có thời hạn, không cộng dồn với chính nó.</summary>
    [DisallowMultipleComponent]
    public sealed class ItemDamageBuff : TimedCharacterStatus, IBuffSource
    {
        private float _multiplier = 1f;

        /// <summary>Phần trăm sát thương cộng thêm cho HUD.</summary>
        public float BonusFraction => _active ? Mathf.Max(0f, _multiplier - 1f) : 0f;

        public float DamageDealtMult => _active ? _multiplier : 1f;
        public float KnockbackDealtMult => 1f;
        public float HitstunTakenMult => 1f;
        public float MoveSpeedMult => 1f;
        public bool IsRageActive => false;
        public bool IsUltimateActive => false;

        /// <summary>Áp mới hoặc làm mới buff sát thương.</summary>
        public void ApplyOrRefresh(float multiplier, float duration, Color tint)
        {
            _multiplier = Mathf.Max(1f, multiplier);
            ApplyOrRefresh(duration, tint);
        }

        protected override void OnStatusApplied() => RefreshAggregator();

        protected override void OnStatusRemoved() => RefreshAggregator();

        private void RefreshAggregator()
        {
            var agg = GetComponentInParent<BuffAggregator>();
            if (agg != null) agg.RefreshSources();
        }
    }
}
