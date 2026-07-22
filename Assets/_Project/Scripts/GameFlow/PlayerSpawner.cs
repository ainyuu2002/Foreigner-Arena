using UnityEngine;
using UnityEngine.SceneManagement;
using FA.Core;

namespace FA.GameFlow
{
    /// <summary>
    /// (P12, research §3.7) SPAWN 2 player vào trận — Instantiate prefab variant P1/P2 đã cấu hình sẵn
    /// (inputScheme + Layer + CharacterData) từ <see cref="CharacterRegistrySO"/> theo lựa chọn CharSelect.
    /// KHÔNG sửa CharacterMotor (field private) — dùng prefab variant (research §3.7 quyết định).
    ///
    /// Đặt player VÀO scene map (additive) để unload sạch khi rời map (research §4 cạm bẫy 8) — qua
    /// <c>SceneManager.MoveGameObjectToScene</c>. Gán respawn point cho mỗi player, rồi đăng ký
    /// LivesSystem+ScoreSystem 2 người vào <see cref="WinCondition"/>. Respawn = CHỈ reposition (không spawn
    /// lại) → player object sống suốt trận, giữ mana/nộ/HUD reference.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSpawner : MonoBehaviour
    {
        [Header("Danh mục & cấu hình")]
        [Tooltip("CharacterRegistrySO — tra prefab variant theo index nhân vật đã chọn.")]
        [SerializeField] private CharacterRegistrySO characterRegistry;

        [Tooltip("WinCondition để đăng ký 2 player vừa spawn. Bỏ trống = FindObjectOfType khi spawn.")]
        [SerializeField] private WinCondition winCondition;

        private GameObject _playerP1, _playerP2;

        /// <summary>2 player hiện đang sống trong trận (MatchManager hủy khi unload map).</summary>
        public GameObject PlayerP1 => _playerP1;
        public GameObject PlayerP2 => _playerP2;

        /// <summary>
        /// Spawn 2 player theo lựa chọn. <paramref name="mapScene"/> = scene map additive đã load (để
        /// MoveGameObjectToScene). <paramref name="spawns"/> = locator điểm spawn của map.
        /// </summary>
        public void SpawnPlayers(int charIndexP1, int charIndexP2, Scene mapScene, StageSpawnPoints spawns)
        {
            DespawnPlayers();

            _playerP1 = SpawnOne(charIndexP1, PlayerSlot.P1, mapScene, spawns);
            _playerP2 = SpawnOne(charIndexP2, PlayerSlot.P2, mapScene, spawns);

            // Chốt nguồn điều khiển đúng một lần khi spawn. Hot-plug giữa trận không đổi CPU/human;
            // rematch spawn object mới nên tự đánh giá lại thiết bị.
            bool cpuP2 = ShouldEnableCpu(PlayerSlot.P2, LocalInputDevices.HasGamepad);
            if (_playerP2 != null)
                _playerP2.BroadcastMessage("SetCpuControl", cpuP2, SendMessageOptions.DontRequireReceiver);

            WireOpponents(_playerP1, _playerP2);
            WireOpponents(_playerP2, _playerP1);

            // (Feedback test) 2 người chọn TRÙNG nhân vật → P2 đổi bộ màu alt (PaletteSwapper —
            // FA.Effects; gọi qua BroadcastMessage như SetOpponent, không ref chéo §1.3).
            if (charIndexP1 == charIndexP2 && _playerP2 != null)
                _playerP2.BroadcastMessage("ApplyAltPalette", SendMessageOptions.DontRequireReceiver);

            RegisterToWinCondition();
        }

        public static bool ShouldEnableCpu(PlayerSlot slot, bool hasGamepad)
            => slot == PlayerSlot.P2 && !hasGamepad;

        /// <summary>
        /// Gán chéo đối thủ sau spawn — FacingController.SetOpponent (auto-face P6) +
        /// KnockbackController.SetOpponentCollider (pushbox/tumble P7). Hai component nằm ở FA.Combat
        /// mà GameFlow không ref (§1.3) → gọi qua BroadcastMessage (pattern SetCurrentMove/SetAttacker
        /// đã dùng trong codebase). Chỉ cần gọi 1 lần/trận: respawn là reposition, không spawn lại.
        /// </summary>
        private static void WireOpponents(GameObject self, GameObject other)
        {
            if (self == null || other == null) return;

            self.BroadcastMessage("SetOpponent", other.transform, SendMessageOptions.DontRequireReceiver);

            Collider2D body = other.GetComponent<Collider2D>(); // capsule thân trên root prefab
            if (body != null)
                self.BroadcastMessage("SetOpponentCollider", body, SendMessageOptions.DontRequireReceiver);
        }

        /// <summary>Hủy 2 player (MatchManager gọi trước khi unload map / rematch reload).</summary>
        public void DespawnPlayers()
        {
            if (_playerP1 != null) Destroy(_playerP1);
            if (_playerP2 != null) Destroy(_playerP2);
            _playerP1 = _playerP2 = null;
        }

        private GameObject SpawnOne(int charIndex, PlayerSlot slot, Scene mapScene, StageSpawnPoints spawns)
        {
            GameObject prefab = characterRegistry != null ? characterRegistry.GetPrefab(charIndex, slot) : null;
            if (prefab == null)
            {
                Debug.LogError($"[PlayerSpawner] Thiếu prefab cho char index {charIndex} slot {slot}.", this);
                return null;
            }

            Transform spawn = spawns != null ? spawns.SpawnFor(slot) : null;
            Vector3 pos = spawn != null ? spawn.position : new Vector3(slot == PlayerSlot.P1 ? -3f : 3f, 1f, 0f);

            GameObject player = Instantiate(prefab, pos, Quaternion.identity);

            // Đưa vào scene map để unload theo map (Unity-native).
            if (mapScene.IsValid() && mapScene.isLoaded)
                SceneManager.MoveGameObjectToScene(player, mapScene);

            // Gán respawn point (giữa map) cho RespawnSystem trên player.
            RespawnSystem respawn = player.GetComponentInChildren<RespawnSystem>();
            if (respawn != null && spawns != null)
                respawn.SetRespawnPoint(spawns.RespawnPoint != null ? spawns.RespawnPoint : spawn);

            return player;
        }

        private void RegisterToWinCondition()
        {
            if (winCondition == null) winCondition = FindFirstObjectByType<WinCondition>();
            if (winCondition == null) return;

            LivesSystem l1 = _playerP1 != null ? _playerP1.GetComponentInChildren<LivesSystem>() : null;
            ScoreSystem sc1 = _playerP1 != null ? _playerP1.GetComponentInChildren<ScoreSystem>() : null;
            LivesSystem l2 = _playerP2 != null ? _playerP2.GetComponentInChildren<LivesSystem>() : null;
            ScoreSystem sc2 = _playerP2 != null ? _playerP2.GetComponentInChildren<ScoreSystem>() : null;

            winCondition.RegisterPlayers(l1, sc1, l2, sc2);
        }
    }
}
