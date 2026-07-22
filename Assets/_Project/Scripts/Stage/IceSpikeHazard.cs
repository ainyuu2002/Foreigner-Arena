using System.Collections.Generic;
using UnityEngine;
using FA.Core;

namespace FA.Stage
{
    /// <summary>
    /// GAI BĂNG v2 — CHẠM LÀ ĐAU MỘT PHÁT (redesign theo feedback test P11, thay DoT LavaFloorZone
    /// vô hình): player chạm vùng gai → +spikeTouchPercent MỘT LẦN + bị HẤT nhẹ lên-và-ra-xa tâm gai
    /// (kiểu Smash — cú nảy tự thân là feedback) + FLASH màu băng (HazardFlashRunner) + miễn nhiễm với
    /// gai spikeImmunitySeconds (chống tick liên tục khi nằm đè lên gai).
    /// Cộng % qua IPercentReceiver (Core §1.3) — KHÔNG gây hitstun/hurt-state (không ref FA.Combat);
    /// cú hất là vật lý thuần: set velocity, state thường sẽ ghi lại vx ngay nhưng vy hop vẫn thấy rõ.
    /// Mọi số ở StageConfigSO (CẦN TEST).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IceSpikeHazard : MonoBehaviour
    {
        [Tooltip("StageConfigSO của map — spikeTouch*/spikeKnock*/spikeImmunity + hazardFlash*. BẮT BUỘC.")]
        [SerializeField] private StageConfigSO config;

        [Tooltip("Vùng gai (BoxCollider2D trigger ôm sát cụm gai).")]
        [SerializeField] private BoxCollider2D spikeArea;

        [Tooltip("Layer player.")]
        [SerializeField] private LayerMask playerLayers;

        [Tooltip("Màu flash nhân vật khi dính gai (xanh băng).")]
        [SerializeField] private Color flashColor = new Color(0.5f, 0.85f, 1f, 1f);

        private readonly List<Collider2D> _hits = new List<Collider2D>(8);
        private readonly Dictionary<Rigidbody2D, float> _immuneUntil = new Dictionary<Rigidbody2D, float>(4);

        private void Awake()
        {
            if (config == null) Debug.LogError("[IceSpikeHazard] Thiếu StageConfigSO.", this);
            if (spikeArea == null) spikeArea = GetComponent<BoxCollider2D>();
        }

        private void FixedUpdate()
        {
            if (config == null || spikeArea == null) return;

            var filter = new ContactFilter2D { useLayerMask = true, layerMask = playerLayers, useTriggers = true };
            _hits.Clear();
            spikeArea.Overlap(filter, _hits);
            foreach (var c in _hits)
            {
                var rb = c.attachedRigidbody;
                if (rb == null) continue;
                var inv = rb.GetComponent<IInvulnerable>(); // bất tử hồi sinh → môi trường bỏ qua.
                if (inv != null && inv.IsInvulnerable) continue;
                if (_immuneUntil.TryGetValue(rb, out float until) && Time.time < until) continue;
                _immuneUntil[rb] = Time.time + config.spikeImmunitySeconds;

                // +% một phát + hất lên-và-ra-xa tâm gai + flash — 3 tín hiệu cùng lúc, không thể miss.
                rb.GetComponent<IPercentReceiver>()?.AddPercent(config.spikeTouchPercent);

                // KHÔNG hất người ĐANG BỊ KNOCKBACK/hitstun (feedback test: xác trượt vào gai bị hất
                // tiếp → chuỗi bay ngoài tầm kiểm soát đẩy thẳng xuống vực). Victim tumble chỉ ăn %
                // + flash rồi trượt tiếp theo đà cũ; cú hất chỉ dành cho người CHỦ ĐỘNG giẫm gai.
                var ctx = rb.GetComponent<ICombatContext>();
                bool inHitstun = ctx != null && ctx.IsInHitstun;
                if (!inHitstun)
                {
                    float side = Mathf.Sign(rb.position.x - transform.position.x);
                    if (side == 0f) side = 1f;
                    rb.linearVelocity = new Vector2(side * config.spikeKnockSideSpeed, config.spikeKnockUpSpeed);
                }

                HazardFlashRunner.Flash(rb, flashColor, config.hazardFlashSeconds);
            }
        }
    }
}
