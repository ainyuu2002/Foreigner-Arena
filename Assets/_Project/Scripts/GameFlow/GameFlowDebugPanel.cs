using UnityEngine;
using FA.Core;

namespace FA.GameFlow
{
    /// <summary>
    /// ⚠ DEBUG-TẠM (KHÔNG phải gameplay deliverable) — bộ "giả lập" cho phép TEST trọn luồng GameFlow
    /// (P12) mà KHÔNG cần combat/animation thật. Editor tool "Build Test_Phase12 (GameFlow harness)"
    /// (FA.EditorTools) dựng UI panel + wire Button.onClick TRỎ vào các method public dưới đây; khi Play,
    /// bấm nút → component này RAISE thẳng các kênh sự kiện Core mà các hệ thống P12 đang nghe.
    ///
    /// VÌ SAO nằm ở FA.GameFlow (KHÔNG ở editor script): cần là MonoBehaviour chạy LÚC PLAY để Button gọi
    /// (editor script không chạy runtime). FA.GameFlow chỉ reference FA.Core → KHÔNG phá cô lập §1.3. Đây là
    /// công cụ test, không phải cơ chế game — production build có thể bỏ object mang component này.
    ///
    /// Mọi thứ Unity-native: chỉ RAISE channel (EventChannelSO), KHÔNG tự tính damage/knockback/mạng — để các
    /// hệ thống thật (LivesSystem/RespawnSystem/ScoreSystem/KnockbackPercent/WinCondition) xử lý như khi chơi.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameFlowDebugPanel : MonoBehaviour
    {
        [Header("Player runtime (tự tìm nếu trống)")]
        [Tooltip("PlayerSpawner để lấy 2 player root đang sống (Attacker/Victim cho HitData giả). " +
                 "Bỏ trống = FindFirstObjectByType khi cần.")]
        [SerializeField] private PlayerSpawner playerSpawner;

        [Header("Kênh CHẾT / KO (giả lập rơi death zone)")]
        [Tooltip("OnPlayerDiedChannel (GameObjectEventChannelSO) — CÙNG asset DeathZone/LivesSystem dùng. " +
                 "RAISE victim root → LivesSystem trừ mạng + RespawnSystem respawn (hoặc hết mạng → WinCondition).")]
        [SerializeField] private GameObjectEventChannelSO onPlayerDied;

        [Header("Kênh ĐÁNH (giả lập hit confirmed)")]
        [Tooltip("OnHitConfirmed (HitEventChannelSO) — CÙNG asset Combat raise. RAISE HitData giả " +
                 "(Attacker/Victim = player root) → ScoreSystem cộng TotalDamage + KnockbackPercent tăng %.")]
        [SerializeField] private HitEventChannelSO onHitConfirmed;

        [Tooltip("% damage mỗi lần bấm nút Damage (giả lập 1 đòn).")]
        [SerializeField] private float fakeDamagePercent = 12f;

        [Header("Kênh COMBO per-player (giả lập combo)")]
        [Tooltip("OnComboChangedChannel_P1 (IntEventChannelSO) — ComboCounter P1 thường raise. " +
                 "RAISE count tăng dần → ScoreSystem cập nhật LongestCombo/TotalComboCount.")]
        [SerializeField] private IntEventChannelSO onComboChangedP1;

        [Tooltip("OnComboChangedChannel_P2 (IntEventChannelSO).")]
        [SerializeField] private IntEventChannelSO onComboChangedP2;

        [Header("Kênh HẾT GIỜ")]
        [Tooltip("OnTimeUpChannel (VoidEventChannelSO) — MatchTimer thường raise. RAISE → WinCondition so mạng/điểm.")]
        [SerializeField] private VoidEventChannelSO onTimeUp;

        private int _comboP1;
        private int _comboP2;

        private void Awake()
        {
            if (playerSpawner == null) playerSpawner = FindFirstObjectByType<PlayerSpawner>();
        }

        // ───────────────────── CHẾT / KO (test mạng + respawn + win) ─────────────────────

        /// <summary>Giả lập P1 rơi death zone → LivesSystem P1 trừ mạng + respawn (% về 0) hoặc hết mạng → End.</summary>
        public void KillP1() => RaiseDied(GetPlayerRoot(true));

        /// <summary>Giả lập P2 rơi death zone.</summary>
        public void KillP2() => RaiseDied(GetPlayerRoot(false));

        private void RaiseDied(GameObject victim)
        {
            if (victim == null)
            {
                Debug.LogWarning("[GameFlowDebugPanel] Chưa có player (vào Match trước khi Kill).", this);
                return;
            }
            if (onPlayerDied != null) onPlayerDied.RaiseEvent(victim);
        }

        // ───────────────────── ĐÁNH (test TotalDamage + % tăng) ─────────────────────

        /// <summary>Giả lập P1 đánh trúng P2 (+fakeDamagePercent) → ScoreSystem P1 TotalDamage + % P2 tăng.</summary>
        public void DamageP1ToP2() => RaiseHit(GetPlayerRoot(true), GetPlayerRoot(false));

        /// <summary>Giả lập P2 đánh trúng P1.</summary>
        public void DamageP2ToP1() => RaiseHit(GetPlayerRoot(false), GetPlayerRoot(true));

        private void RaiseHit(GameObject attacker, GameObject victim)
        {
            if (attacker == null || victim == null)
            {
                Debug.LogWarning("[GameFlowDebugPanel] Chưa đủ 2 player (vào Match trước khi Damage).", this);
                return;
            }
            // HitData giả: chỉ cần Attacker/Victim/DamagePercent + facing để KnockbackPercent/ScoreSystem chạy.
            // KHÔNG launch (causesLaunch=false) để player không bay khỏi sàn khi test mạng.
            var hit = new HitData(
                attacker, victim,
                fakeDamagePercent, launchAngle: 45f, baseKnockback: 4f, knockbackGrowth: 0.1f,
                hitstopFrames: 0, facingSign: 1, isSpike: false, causesLaunch: false,
                hitsLow: false, hitsHigh: true,
                hitstunFrames: 0,
                contactPoint: victim.transform.position);
            if (onHitConfirmed != null) onHitConfirmed.RaiseEvent(hit);
        }

        // ───────────────────── COMBO (test LongestCombo/score) ─────────────────────

        /// <summary>Tăng combo P1 (+1) → kênh combo P1 → ScoreSystem cập nhật Longest/TotalComboCount.</summary>
        public void ComboPlusP1() { _comboP1++; if (onComboChangedP1 != null) onComboChangedP1.RaiseEvent(_comboP1); }

        /// <summary>Tăng combo P2 (+1).</summary>
        public void ComboPlusP2() { _comboP2++; if (onComboChangedP2 != null) onComboChangedP2.RaiseEvent(_comboP2); }

        /// <summary>Reset combo P1 về 0 (giả lập đứt combo) — bắt đầu combo mới ở lần +1 kế.</summary>
        public void ComboResetP1() { _comboP1 = 0; if (onComboChangedP1 != null) onComboChangedP1.RaiseEvent(0); }

        /// <summary>Reset combo P2 về 0.</summary>
        public void ComboResetP2() { _comboP2 = 0; if (onComboChangedP2 != null) onComboChangedP2.RaiseEvent(0); }

        // ───────────────────── HẾT GIỜ (test WinCondition so mạng/điểm) ─────────────────────

        /// <summary>Giả lập hết giờ → WinCondition so mạng còn lại (bằng mạng → so điểm) → End screen.</summary>
        public void ForceTimeUp() { if (onTimeUp != null) onTimeUp.RaiseEvent(); }

        // ───────────────────── tiện ích ─────────────────────

        private GameObject GetPlayerRoot(bool p1)
        {
            if (playerSpawner == null) playerSpawner = FindFirstObjectByType<PlayerSpawner>();
            if (playerSpawner == null) return null;
            return p1 ? playerSpawner.PlayerP1 : playerSpawner.PlayerP2;
        }
    }
}
