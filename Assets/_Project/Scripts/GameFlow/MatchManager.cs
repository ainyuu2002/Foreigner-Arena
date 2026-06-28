using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using FA.Core;

namespace FA.GameFlow
{
    /// <summary>
    /// (P12, research §3.1/§3.7) ĐIỀU PHỐI VÒNG ĐỜI TRẬN — state machine enum (<see cref="MatchPhase"/>):
    /// CharSelect → StageSelect → Match → End → (Rematch | back CharSelect). KHÔNG dùng IState (per-frame
    /// gameplay) — chuyển phase theo SỰ KIỆN UI.
    ///
    /// Unity-native (research §3.1):
    ///   • Menu (CharSelect/StageSelect/End) = PANEL trong CÙNG scene, bật/tắt <c>SetActive</c>.
    ///   • Map P11 = SCENE RIÊNG, <c>SceneManager.LoadSceneAsync(..., Additive)</c> khi vào Match, unload
    ///     khi rời → tilemap nặng + Cinemachine Confiner giữ sạch trong scene riêng (research §3.1).
    ///
    /// Decoupling (§1.3): UI panel RAISE CharSelected/StageSelected/RematchPressed (IntEventChannelSO/Void);
    /// MatchManager SUBSCRIBE — UI KHÔNG ref MatchManager. MatchEnded (từ WinCondition) → vào End.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchManager : MonoBehaviour
    {
        [Header("Panels (menu — cùng scene)")]
        [SerializeField] private GameObject charSelectPanel;
        [SerializeField] private GameObject stageSelectPanel;
        [SerializeField] private GameObject endScreenPanel;
        [Tooltip("(Tùy) HUD trận — bật khi Match, tắt khi menu.")]
        [SerializeField] private GameObject matchHud;

        [Header("Hệ thống trận (cùng module)")]
        [SerializeField] private PlayerSpawner playerSpawner;
        [SerializeField] private MatchTimer matchTimer;
        [SerializeField] private StageRegistrySO stageRegistry;

        [Header("Reset đầu trận")]
        [Tooltip("LivesSystem/ScoreSystem được reset qua spawn player mới mỗi Match (Instantiate prefab sạch). " +
                 "MatchTimer reset ở đây. % reset theo spawn (KnockbackPercent khởi tạo 0).")]
        [SerializeField] private bool resetTimerOnMatch = true;

        [Header("Kênh sự kiện (NGHE)")]
        [Tooltip("CharSelected (IntEventChannelSO) — đóng gói 2 lựa chọn? P12 đơn giản: dùng 2 kênh P1/P2.")]
        [SerializeField] private IntEventChannelSO onCharSelectedP1;
        [SerializeField] private IntEventChannelSO onCharSelectedP2;
        [Tooltip("StageSelected (IntEventChannelSO) — index map đã chọn → vào Match.")]
        [SerializeField] private IntEventChannelSO onStageSelected;
        [Tooltip("RematchPressed (VoidEventChannelSO) — chơi lại cùng nhân vật/map.")]
        [SerializeField] private VoidEventChannelSO onRematchPressed;
        [Tooltip("BackToCharSelect (VoidEventChannelSO) — về màn chọn nhân vật.")]
        [SerializeField] private VoidEventChannelSO onBackToCharSelect;
        [Tooltip("MatchEnded (VoidEventChannelSO) — WinCondition raise → vào End.")]
        [SerializeField] private VoidEventChannelSO onMatchEnded;

        private MatchPhase _phase;
        private int _charP1 = -1, _charP2 = -1;
        private int _stageIndex = -1;
        private string _loadedMapScene;

        public MatchPhase Phase => _phase;

        private void Awake()
        {
            // MatchManager sống xuyên suốt (giữ state); player KHÔNG (theo scene map).
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            if (onCharSelectedP1 != null) onCharSelectedP1.OnEventRaised += SetCharP1;
            if (onCharSelectedP2 != null) onCharSelectedP2.OnEventRaised += SetCharP2;
            if (onStageSelected != null) onStageSelected.OnEventRaised += OnStageSelected;
            if (onRematchPressed != null) onRematchPressed.OnEventRaised += OnRematch;
            if (onBackToCharSelect != null) onBackToCharSelect.OnEventRaised += OnBackToCharSelect;
            if (onMatchEnded != null) onMatchEnded.OnEventRaised += OnMatchEnded;
            // DEBUG harness P12
            Debug.Log($"[MatchManager] OnEnable subscribe P1={(onCharSelectedP1 != null)} P2={(onCharSelectedP2 != null)} Stage={(onStageSelected != null)}");
        }

        private void OnDisable()
        {
            if (onCharSelectedP1 != null) onCharSelectedP1.OnEventRaised -= SetCharP1;
            if (onCharSelectedP2 != null) onCharSelectedP2.OnEventRaised -= SetCharP2;
            if (onStageSelected != null) onStageSelected.OnEventRaised -= OnStageSelected;
            if (onRematchPressed != null) onRematchPressed.OnEventRaised -= OnRematch;
            if (onBackToCharSelect != null) onBackToCharSelect.OnEventRaised -= OnBackToCharSelect;
            if (onMatchEnded != null) onMatchEnded.OnEventRaised -= OnMatchEnded;
        }

        private void Start() => GoToCharSelect();

        // ---- Chuyển phase ----

        public void GoToCharSelect()
        {
            _phase = MatchPhase.CharSelect;
            ShowOnly(charSelectPanel);
            if (matchTimer != null) matchTimer.SetRunning(false);
        }

        private void GoToStageSelect()
        {
            Debug.Log("[MatchManager] → StageSelect"); // DEBUG harness P12
            _phase = MatchPhase.StageSelect;
            ShowOnly(stageSelectPanel);
        }

        private void GoToMatch()
        {
            Debug.Log("[MatchManager] → Match (spawn players)"); // DEBUG harness P12
            _phase = MatchPhase.Match;
            ShowOnly(null);                 // ẩn mọi panel menu
            if (matchHud != null) matchHud.SetActive(true);
            StartCoroutine(LoadMapAndStart());
        }

        private void GoToEnd()
        {
            _phase = MatchPhase.End;
            if (matchTimer != null) matchTimer.SetRunning(false);
            if (matchHud != null) matchHud.SetActive(false);
            ShowOnly(endScreenPanel);
        }

        // ---- Load/unload map (Unity-native additive) ----

        private IEnumerator LoadMapAndStart()
        {
            string sceneName = stageRegistry != null ? stageRegistry.GetSceneName(_stageIndex) : null;
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[MatchManager] Stage scene name rỗng — kiểm tra StageRegistry/Build Settings.", this);
                yield break;
            }

            yield return UnloadMapIfAny();

            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            while (load != null && !load.isDone) yield return null;

            Scene mapScene = SceneManager.GetSceneByName(sceneName);
            _loadedMapScene = sceneName;

            StageSpawnPoints spawns = StageSpawnPoints.Active;
            if (playerSpawner != null)
                playerSpawner.SpawnPlayers(_charP1, _charP2, mapScene, spawns);

            if (matchTimer != null)
            {
                if (resetTimerOnMatch) matchTimer.ResetTimer();
                matchTimer.SetRunning(true);
            }
        }

        private IEnumerator UnloadMapIfAny()
        {
            if (playerSpawner != null) playerSpawner.DespawnPlayers();
            if (!string.IsNullOrEmpty(_loadedMapScene))
            {
                Scene s = SceneManager.GetSceneByName(_loadedMapScene);
                if (s.IsValid() && s.isLoaded)
                {
                    AsyncOperation unload = SceneManager.UnloadSceneAsync(s);
                    while (unload != null && !unload.isDone) yield return null;
                }
                _loadedMapScene = null;
            }
        }

        // ---- Handlers kênh UI ----

        private void SetCharP1(int idx)
        {
            _charP1 = idx;
            Debug.Log($"[MatchManager] SetCharP1={idx} (P2={_charP2}) gate→{(_charP2 >= 0)}"); // DEBUG harness P12
            if (_charP2 >= 0) GoToStageSelect();   // cả 2 đã chọn → sang stage.
        }

        private void SetCharP2(int idx)
        {
            _charP2 = idx;
            Debug.Log($"[MatchManager] SetCharP2={idx} (P1={_charP1}) gate→{(_charP1 >= 0)}"); // DEBUG harness P12
            if (_charP1 >= 0) GoToStageSelect();
        }

        private void OnStageSelected(int idx)
        {
            _stageIndex = idx;
            GoToMatch();
        }

        private void OnRematch()
        {
            // Rematch = trận MỚI cùng nhân vật/map → reload map (spawn lại player sạch: % + mana/nộ về mặc
            // định vì player Instantiate mới). KHÁC respawn (respawn giữ mana/nộ). research §3.8.
            GoToMatch();
        }

        private void OnBackToCharSelect()
        {
            StartCoroutine(BackToCharSelectRoutine());
        }

        private IEnumerator BackToCharSelectRoutine()
        {
            yield return UnloadMapIfAny();
            _charP1 = _charP2 = -1;
            GoToCharSelect();
        }

        private void OnMatchEnded() => GoToEnd();

        // ---- Tiện ích panel ----

        private void ShowOnly(GameObject panel)
        {
            if (charSelectPanel != null) charSelectPanel.SetActive(panel == charSelectPanel);
            if (stageSelectPanel != null) stageSelectPanel.SetActive(panel == stageSelectPanel);
            if (endScreenPanel != null) endScreenPanel.SetActive(panel == endScreenPanel);
        }
    }
}
