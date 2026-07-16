using UnityEngine;

namespace FA.Items
{
    /// <summary>
    /// (Hợp nhất item) Trạng thái RƠI CHẬM có thời hạn — giảm gravityScale của Rigidbody2D người nhặt
    /// (×(1-value), sàn 0.1). Kế thừa <see cref="TimedCharacterStatus"/> (cache renderer + tint + nháy +
    /// countdown chung). Dedup theo TYPE = GetComponent&lt;SlowFallStatus&gt;() (1 component/nhân vật).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SlowFallStatus : TimedCharacterStatus
    {
        private float _value;
        private Rigidbody2D _body;
        private float _originalGravityScale;
        private bool _gravityModified;

        /// <summary>Overload: đặt độ giảm rồi refresh (base lo tint/countdown). value ∈ [0..1].</summary>
        public void ApplyOrRefresh(float value, float duration, Color tint)
        {
            _value = Mathf.Clamp01(value);
            ApplyOrRefresh(duration, tint);
        }

        protected override void OnStatusApplied()
        {
            RestoreGravityIfNeeded(); // refresh: trả về gốc trước khi áp lại (không nhân chồng).

            _body = GetComponentInChildren<Rigidbody2D>();
            if (_body == null) return;

            _originalGravityScale = _body.gravityScale;
            _body.gravityScale = Mathf.Max(0.1f, _originalGravityScale * (1f - _value));
            _gravityModified = true;
        }

        protected override void OnStatusRemoved() => RestoreGravityIfNeeded();

        private void RestoreGravityIfNeeded()
        {
            if (!_gravityModified || _body == null) return;

            _body.gravityScale = _originalGravityScale;
            _gravityModified = false;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            RestoreGravityIfNeeded();
        }
    }
}
