using UnityEngine;
using FA.Core;

namespace FA.GameFlow
{
    /// <summary>
    /// (P12, research §3.2) MẠNG per-player — gắn lên player prefab (giống KnockbackPercent per-victim).
    /// Subscribe <c>OnPlayerDiedChannel</c> (GameObjectEventChannelSO, raise victim root từ DeathZone P11);
    /// nếu victim LÀ MÌNH → <c>lives--</c>. Còn mạng → gọi RespawnSystem (cùng module GameFlow → gọi trực
    /// tiếp OK, §1.3 chỉ cấm CHÉO module). Hết mạng (0) → raise <c>onOutOfLives(self)</c> → WinCondition/
    /// MatchManager kết thúc trận.
    ///
    /// Phát số mạng còn lại qua <see cref="onLivesChanged"/> (IntEventChannelSO per-player) cho LivesView
    /// (FA.UI) hiển thị — KHÔNG code UI ở đây (§1.3). startingLives lấy từ MatchSettingsSO (không hardcode).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LivesSystem : MonoBehaviour
    {
        [Header("Cấu hình")]
        [Tooltip("MatchSettingsSO — đọc startingLives (mặc định 3). BẮT BUỘC gán.")]
        [SerializeField] private MatchSettingsSO settings;

        [Header("Kênh sự kiện (NGHE)")]
        [Tooltip("OnPlayerDiedChannel (GameObjectEventChannelSO) — CÙNG asset DeathZone P11 raise victim root.")]
        [SerializeField] private GameObjectEventChannelSO onPlayerDied;

        [Header("Kênh sự kiện (BẮN)")]
        [Tooltip("OnLivesChanged per-player (IntEventChannelSO) — bắn số mạng còn lại cho LivesView (FA.UI).")]
        [SerializeField] private IntEventChannelSO onLivesChanged;

        [Tooltip("OnPlayerOutOfLives (GameObjectEventChannelSO) — raise SELF root khi hết mạng. WinCondition nghe.")]
        [SerializeField] private GameObjectEventChannelSO onOutOfLives;

        [Header("Tham chiếu cùng module")]
        [Tooltip("RespawnSystem trên CÙNG player — gọi khi chết mà còn mạng. Bỏ trống = GetComponent.")]
        [SerializeField] private RespawnSystem respawnSystem;

        [Tooltip("Root để so 'mình có phải victim không'. Bỏ trống = transform.root.")]
        [SerializeField] private Transform root;

        private int _lives;

        /// <summary>Mạng còn lại (WinCondition/ScoreSystem đọc cho tie-break so mạng).</summary>
        public int Lives => _lives;

        /// <summary>Đã hết mạng chưa (để tránh xử lý trùng khi chết đồng thời).</summary>
        public bool IsOut => _lives <= 0;

        private void Awake()
        {
            if (root == null) root = transform.root;
            if (respawnSystem == null) respawnSystem = GetComponent<RespawnSystem>();
            ResetLives();
        }

        private void OnEnable()
        {
            if (onPlayerDied != null) onPlayerDied.OnEventRaised += OnDied;
        }

        private void OnDisable()
        {
            if (onPlayerDied != null) onPlayerDied.OnEventRaised -= OnDied;
        }

        /// <summary>Đặt lại mạng đầu trận (MatchManager gọi khi vào Match / Rematch).</summary>
        public void ResetLives()
        {
            _lives = settings != null ? settings.startingLives : 3;
            if (onLivesChanged != null) onLivesChanged.RaiseEvent(_lives);
        }

        private void OnDied(GameObject victim)
        {
            GameObject me = root != null ? root.gameObject : gameObject;
            if (!ReferenceEquals(victim, me)) return;

            if (_lives <= 0) return; // đã hết — bỏ qua (chống double-trigger chết đồng thời).

            _lives--;
            if (onLivesChanged != null) onLivesChanged.RaiseEvent(_lives);

            if (_lives <= 0)
            {
                if (onOutOfLives != null) onOutOfLives.RaiseEvent(me);
                // Không respawn — WinCondition sẽ kết thúc trận.
                return;
            }

            // Còn mạng → respawn (reposition + % về 0 + i-frame). Cùng module → gọi trực tiếp.
            if (respawnSystem != null) respawnSystem.Respawn();
        }
    }
}
