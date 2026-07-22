using UnityEngine;
using FA.Core;

namespace FA.GameFlow
{
    /// <summary>
    /// ĐĨA HỒI SINH kiểu Smash (feedback test P12): RespawnSystem spawn 1 bệ one-way lơ lửng tại điểm
    /// hồi sinh (đã nâng cao) — player đứng trên đó tới khi TỰ QUYẾT rời (đi ra mép / nhảy). Rời bệ
    /// quá LeaveGraceSeconds → bệ biến mất; có trần maxLifetime chống chiếm bệ vô hạn.
    /// Dựng thuần code (BoxCollider2D + PlatformEffector2D one-way, layer OneWayPlatform — Unity-native
    /// §1.1, giống platform P11); sprite là placeholder tự sinh nếu không gán art (CẦN art sau).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RespawnPlatform : MonoBehaviour
    {
        private const float LeaveGraceSeconds = 0.3f; // rời bệ ngắn hơn (nhún nhảy tại chỗ) chưa tính là bỏ đi.

        private static Sprite _fallbackSprite; // cache — sinh 1 lần cho cả trận.

        private Collider2D _platCollider;
        private Collider2D _playerBody;
        private float _lifeLeft;
        private float _leaveTimer;
        private bool _everTouched; // chỉ tính "rời bệ" sau khi ĐÃ đứng lên (spawn rơi xuống bệ mất vài frame).

        /// <summary>Spawn bệ ngay dưới chân vị trí hồi sinh. <paramref name="playerBody"/> = collider
        /// thân player (để biết khi nào rời bệ); <paramref name="maxLifetime"/> = trần tồn tại (giây).</summary>
        public static RespawnPlatform Spawn(Vector3 playerFootPos, Collider2D playerBody,
                                            Sprite sprite, float maxLifetime)
        {
            var go = new GameObject("RespawnPlatform") { layer = GameLayers.OneWayPlatform };
            go.transform.position = playerFootPos + Vector3.down * 0.25f;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite != null ? sprite : GetFallbackSprite();
            sr.sortingOrder = 40;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(3.4f, 0.45f);
            col.usedByEffector = true;
            var eff = go.AddComponent<PlatformEffector2D>(); // one-way: đòn/người khác đi xuyên từ dưới.
            eff.useOneWay = true;
            eff.surfaceArc = 180f;

            var p = go.AddComponent<RespawnPlatform>();
            p._platCollider = col;
            p._playerBody = playerBody;
            p._lifeLeft = Mathf.Max(1f, maxLifetime);
            return p;
        }

        private void FixedUpdate()
        {
            _lifeLeft -= Time.fixedDeltaTime;
            if (_lifeLeft <= 0f) { Destroy(gameObject); return; }
            if (_playerBody == null || _platCollider == null) return;

            if (_platCollider.IsTouching(_playerBody))
            {
                _everTouched = true;
                _leaveTimer = 0f;
                return;
            }
            if (!_everTouched) return; // đang rơi xuống bệ lúc mới spawn — chưa tính.

            _leaveTimer += Time.fixedDeltaTime;
            if (_leaveTimer >= LeaveGraceSeconds)
                Destroy(gameObject); // player đã quyết định rời bệ.
        }

        /// <summary>Sprite placeholder (thanh sáng bo tròn) — thay bằng art thật qua field của RespawnSystem.</summary>
        private static Sprite GetFallbackSprite()
        {
            if (_fallbackSprite != null) return _fallbackSprite;

            const int w = 68, h = 10;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var top = new Color(0.75f, 0.9f, 1f, 1f);      // mặt trên sáng (xanh băng nhạt).
            var body = new Color(0.35f, 0.55f, 0.8f, 1f);  // thân đậm.
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool corner = (x < 3 || x >= w - 3) && (y < 2 || y >= h - 2); // bo góc thô.
                    tex.SetPixel(x, y, corner ? Color.clear : (y >= h - 3 ? top : body));
                }
            tex.Apply();
            _fallbackSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 20f);
            return _fallbackSprite;
        }
    }
}
