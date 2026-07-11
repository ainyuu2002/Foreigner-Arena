using System;
using UnityEngine;

namespace FA.Specials
{
    /// <summary>
    /// FADE ĐEN CHUYỂN CẢNH nhanh (UBW kết thúc: "đen màn → đổi cảnh khi đen kịt → hiện lại như chưa
    /// có gì"): quad đen world sortingOrder cao phủ màn, alpha 0→1→0 theo UNSCALED time, tự Destroy.
    /// Gọi tĩnh <see cref="Play"/> với callback <c>onBlack</c> — chạy ĐÚNG LÚC màn đen kịt (đó là chỗ
    /// revert background/BGM để người chơi không thấy khoảnh khắc swap). Dùng chung cho hết-nộ lẫn
    /// sau super-UBW (sắp làm).
    /// </summary>
    public sealed class ScreenFadeQuad : MonoBehaviour
    {
        private float _in, _hold, _out;
        private Action _onBlack;
        private SpriteRenderer _sr;
        private float _t;
        private bool _blackDone;

        /// <summary>Fade đen: vào <paramref name="inSeconds"/> → giữ <paramref name="holdSeconds"/>
        /// (gọi <paramref name="onBlack"/> lúc vừa đen kịt) → nhả <paramref name="outSeconds"/> → Destroy.</summary>
        public static void Play(float inSeconds, float holdSeconds, float outSeconds, Action onBlack)
        {
            var go = new GameObject("ScreenFade");
            var fade = go.AddComponent<ScreenFadeQuad>();
            fade._in = Mathf.Max(0.01f, inSeconds);
            fade._hold = Mathf.Max(0f, holdSeconds);
            fade._out = Mathf.Max(0.01f, outSeconds);
            fade._onBlack = onBlack;
        }

        private void Awake()
        {
            _sr = gameObject.AddComponent<SpriteRenderer>();
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.black);
            tex.Apply();
            _sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            _sr.sortingOrder = 95;   // trên chant-dim (80) + flash-dim (90), dưới Canvas UI.
            _sr.color = new Color(0f, 0f, 0f, 0f);
            Camera cam = Camera.main;
            transform.position = cam != null
                ? new Vector3(cam.transform.position.x, cam.transform.position.y, 0f)
                : Vector3.zero;
            transform.localScale = new Vector3(400f, 400f, 1f);
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            float a;
            if (_t < _in) a = _t / _in;
            else
            {
                if (!_blackDone)
                {
                    _blackDone = true;
                    _onBlack?.Invoke();   // màn đang đen kịt — swap cảnh ở đây.
                }
                float tOut = _t - _in - _hold;
                if (tOut < 0f) a = 1f;
                else if (tOut < _out) a = 1f - tOut / _out;
                else { Destroy(gameObject); return; }
            }
            _sr.color = new Color(0f, 0f, 0f, a);
        }
    }
}
