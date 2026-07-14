using System.Collections;
using UnityEngine;

namespace FA.Items
{
    public sealed class MysteryBlockTimedStatus : MonoBehaviour
    {
        private const float BlinkLeadSeconds = 3f;
        private const float BlinkPeriod = 0.2f;

        public MysteryBlockOutcomeType Type => _type;

        private MysteryBlockOutcomeType _type;
        private float _value;
        private float _timeLeft;
        private bool _active;

        private Rigidbody2D _body;
        private float _originalGravityScale;
        private bool _gravityModified;

        private SpriteRenderer[] _renderers;
        private Color[] _originalColors;
        private Color _tint = Color.white;
        private Coroutine _routine;

        public void ApplyOrRefresh(MysteryBlockOutcome outcome, Color tint)
        {
            if (outcome == null) return;

            CacheRenderers();
            RestoreGravityIfNeeded();

            _type = outcome.type;
            _value = Mathf.Max(0f, outcome.value);
            _timeLeft = Mathf.Max(0.1f, outcome.durationSeconds);
            _tint = tint;
            _active = true;

            ApplyStatus();
            ApplyTint(true);

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(StatusLoop());
        }

        private void ApplyStatus()
        {
            if (_type != MysteryBlockOutcomeType.SlowFall) return;

            _body = GetComponentInChildren<Rigidbody2D>();
            if (_body == null) return;

            _originalGravityScale = _body.gravityScale;
            float gravityMultiplier = Mathf.Clamp01(1f - _value);
            _body.gravityScale = Mathf.Max(0.1f, _originalGravityScale * gravityMultiplier);
            _gravityModified = true;
        }

        private IEnumerator StatusLoop()
        {
            while (_timeLeft > 0f)
            {
                _timeLeft -= Time.deltaTime;

                if (_timeLeft <= BlinkLeadSeconds)
                {
                    bool on = Mathf.Repeat(_timeLeft, BlinkPeriod * 2f) > BlinkPeriod;
                    ApplyTint(on);
                }

                yield return null;
            }

            _active = false;
            ApplyTint(false);
            RestoreGravityIfNeeded();
            Destroy(this);
        }

        private void CacheRenderers()
        {
            if (_renderers != null) return;

            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _originalColors[i] = _renderers[i].color;
        }

        private void ApplyTint(bool on)
        {
            if (_renderers == null) return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].color = on ? _tint : _originalColors[i];
            }
        }

        private void RestoreGravityIfNeeded()
        {
            if (!_gravityModified || _body == null) return;

            _body.gravityScale = _originalGravityScale;
            _gravityModified = false;
        }

        private void OnDisable()
        {
            if (!_active) ApplyTint(false);
            RestoreGravityIfNeeded();
        }
    }
}
