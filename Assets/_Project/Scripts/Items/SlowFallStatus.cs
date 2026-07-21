using UnityEngine;

namespace FA.Items
{
    /// <summary>Giảm trọng lực của nhân vật trong một khoảng thời gian.</summary>
    [DisallowMultipleComponent]
    public sealed class SlowFallStatus : TimedCharacterStatus
    {
        private float _value;
        private Rigidbody2D _body;
        private float _originalGravityScale;
        private float _slowGravityScale;
        private bool _gravityModified;

        /// <summary>Tỉ lệ giảm trọng lực cho HUD.</summary>
        public float GravityReductionFraction => _active ? _value : 0f;

        /// <summary>Áp mới hoặc làm mới hiệu ứng rơi chậm.</summary>
        public void ApplyOrRefresh(float value, float duration, Color tint)
        {
            _value = Mathf.Clamp01(value);
            ApplyOrRefresh(duration, tint);
        }

        protected override void OnStatusApplied()
        {
            if (_body == null) _body = GetComponentInChildren<Rigidbody2D>();
            if (_body == null) return;

            if (!_gravityModified)
            {
                TryStartSlowFall();
                return;
            }

            UpdateSlowGravity();
            if (_body.gravityScale > 0f) _body.gravityScale = _slowGravityScale;
        }

        private void FixedUpdate()
        {
            if (!_active || _body == null) return;
            if (!_gravityModified)
            {
                TryStartSlowFall();
                return;
            }

            // Movement có thể reset gravity khi đổi state. Gravity 0 được giữ nguyên cho Dash.
            if (_body.gravityScale > 0f && !Mathf.Approximately(_body.gravityScale, _slowGravityScale))
                _body.gravityScale = _slowGravityScale;
        }

        private void TryStartSlowFall()
        {
            // Đợi Dash kết thúc nếu effect được nhặt đúng lúc gravity đang bị khóa về 0.
            if (_body == null || _body.gravityScale <= 0f) return;

            _originalGravityScale = _body.gravityScale;
            _gravityModified = true;
            UpdateSlowGravity();
            _body.gravityScale = _slowGravityScale;
        }

        private void UpdateSlowGravity()
        {
            _slowGravityScale = Mathf.Max(0.1f, _originalGravityScale * (1f - _value));
        }

        protected override void OnStatusRemoved() => RestoreGravityIfNeeded();

        private void RestoreGravityIfNeeded()
        {
            if (!_gravityModified || _body == null) return;

            // Nếu đang Dash, để state Movement tự trả gravity về baseline khi Dash kết thúc.
            if (_body.gravityScale > 0f) _body.gravityScale = _originalGravityScale;
            _gravityModified = false;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            RestoreGravityIfNeeded();
        }
    }
}
