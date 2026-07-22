using System.Collections.Generic;
using UnityEngine;
using FA.Core;

namespace FA.Stage
{
    /// <summary>
    /// NẤM NHÚN v2 — "MỀN NHẢY" (redesign theo feedback test P11):
    /// (a) Lực bật SCALE THEO ĐỘ CAO RƠI: mũ nấm là collider ĐẶC one-way (PlatformEffector2D) +
    ///     PhysicsMaterial2D bounciness → Unity vật lý tự nảy tỉ lệ tốc độ va (§1.1); rơi càng cao
    ///     bật càng mạnh. Code chỉ GIA CỐ sàn nảy tối thiểu (đi bộ vào mũ cũng nhún nhẹ).
    /// (b) BẤM JUMP ĐÚNG NHỊP chạm mũ (cửa sổ mushroomTimedWindowFrames) → cộng thêm lực; đúng nhịp
    ///     LIÊN TIẾP → cộng dồn mushroomStackBoostStep mỗi lần (càng nhún càng cao), trần
    ///     mushroomMaxBounceSpeed, hụt nhịp là mất chuỗi. Đọc cú bấm qua <see cref="IJumpIntent"/>
    ///     (Core §1.3 — không ref FA.Movement); CONSUME cú bấm để DoubleJump không nuốt lại.
    /// (c) Victim bị ĐÁNH VĂNG trúng mũ → bounciness phản xạ theo lực/góc va tự nhiên (tumble không
    ///     ghi velocity nên vật lý toàn quyền); code chỉ đảm bảo nảy tối thiểu, GIỮ vx (lái như Smash).
    /// Cơ chế ĐỊA HÌNH CỐ ĐỊNH, deterministic — GDD Trang 9. Mọi số ở StageConfigSO (CẦN TEST).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MushroomBouncer : MonoBehaviour
    {
        [Tooltip("StageConfigSO của map — bộ tham số mushroom*. BẮT BUỘC.")]
        [SerializeField] private StageConfigSO config;

        [Tooltip("Collider ĐẶC mũ nấm (one-way + PhysicsMaterial2D bounciness — KHÔNG còn trigger như v1).")]
        [SerializeField] private BoxCollider2D capArea;

        [Tooltip("Layer player (lọc va chạm; 0 = nhận mọi Rigidbody2D).")]
        [SerializeField] private LayerMask playerLayers;

        [Tooltip("Transform co giãn khi nhún (bỏ trống = chính object này).")]
        [SerializeField] private Transform squashTarget;

        private sealed class BounceStreak { public int stacks; public float lastTime; }

        private readonly Dictionary<Rigidbody2D, BounceStreak> _streaks = new Dictionary<Rigidbody2D, BounceStreak>(4);
        private readonly Dictionary<Rigidbody2D, float> _cooldownUntil = new Dictionary<Rigidbody2D, float>(4);
        private Vector3 _baseScale;
        private float _squashT = 1f; // 1 = đứng yên, 0 = vừa bị đáp.

        private void Awake()
        {
            if (config == null) Debug.LogError("[MushroomBouncer] Thiếu StageConfigSO.", this);
            if (capArea == null) capArea = GetComponent<BoxCollider2D>();
            if (squashTarget == null) squashTarget = transform;
            _baseScale = squashTarget.localScale;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (config == null) return;
            var rb = collision.rigidbody;
            if (rb == null) return;
            if (playerLayers.value != 0 && (playerLayers.value & (1 << rb.gameObject.layer)) == 0) return;

            // Chỉ nhận va TỪ TRÊN (one-way đã chặn từ dưới; chặn nốt va cạnh): thân phải ở trên mặt mũ.
            if (capArea != null && rb.position.y < capArea.bounds.max.y - 0.1f) return;
            if (_cooldownUntil.TryGetValue(rb, out float until) && Time.time < until) return;
            _cooldownUntil[rb] = Time.time + 0.15f; // chặn double-fire khi nhiều contact cùng cú va.

            // Bounciness đã tự nảy theo tốc độ rơi (vy sau va = impact × bounciness). Gia cố:
            float target = Mathf.Max(rb.linearVelocity.y, config.mushroomMinBounceSpeed);

            var streak = GetStreak(rb);
            var intent = rb.GetComponent<IJumpIntent>();
            if (intent != null && intent.TryConsumeRecentJumpPress(config.mushroomTimedWindowFrames))
            {
                streak.stacks++;
                target += config.mushroomTimedBoostSpeed
                          + (streak.stacks - 1) * config.mushroomStackBoostStep;
            }
            else
            {
                streak.stacks = 0; // hụt nhịp → mất chuỗi cộng dồn.
            }
            streak.lastTime = Time.time;
            // Tắt double-jump cho cú bay này (feedback test): J giữa chuỗi nhún là để CANH NHỊP,
            // không bị nuốt thành double-jump làm đứt chuỗi. Đáp đất thật thì motor tự phục hồi.
            intent?.SuppressAirJumps();

            target = Mathf.Min(target, config.mushroomMaxBounceSpeed);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, target); // GIỮ vx — lái hướng bay như Smash.
            _squashT = 0f; // kích hiệu ứng nhún.
        }

        private BounceStreak GetStreak(Rigidbody2D rb)
        {
            if (!_streaks.TryGetValue(rb, out var s))
                _streaks[rb] = s = new BounceStreak();
            else if (Time.time - s.lastTime > config.mushroomStackResetSeconds)
                s.stacks = 0; // để lâu không nhún tiếp → chuỗi nguội.
            return s;
        }

        private void Update()
        {
            if (_squashT >= 1f) return;
            _squashT = Mathf.Min(1f, _squashT + Time.deltaTime * 3.2f);
            float k = Mathf.Sin(_squashT * Mathf.PI); // 0→1→0: ép dẹt rồi nảy về nguyên trạng.
            squashTarget.localScale = new Vector3(
                _baseScale.x * (1f + 0.22f * k),
                _baseScale.y * (1f - 0.3f * k), 1f);
        }
    }
}
