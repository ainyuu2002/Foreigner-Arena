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

        [Header("Kênh sự kiện (PHÁT)")]
        [Tooltip("MatchStart (VoidEventChannelSO) — RAISE ở CUỐI LoadMapAndStart (sau load+spawn+timer, sau 1 " +
                 "frame). ItemSpawner nghe để StartSpawning. Void KHÔNG replay → object map phải subscribe trước.")]
        [SerializeField] private VoidEventChannelSO onMatchStart;

        [Header("MỤC D — Intro vào trận (tuần tự P1→P2, zoom camera)")]
        [Tooltip("OnRoundIntroStart (VoidEventChannelSO) — RAISE sau SpawnPlayers; RoundIntroController " +
                 "(FA.Stage) nghe để khóa input + zoom camera + diễn intro. NULL = bỏ qua intro (test scene).")]
        [SerializeField] private VoidEventChannelSO onRoundIntroStart;
        [Tooltip("OnRoundIntroFinished (VoidEventChannelSO) — RoundIntroController raise khi intro P1+P2 xong. " +
                 "MatchManager CHỜ kênh này rồi mới chạy đồng hồ + raise MatchStart (item không rơi lúc pose).")]
        [SerializeField] private VoidEventChannelSO onRoundIntroFinished;
        [Tooltip("Timeout an toàn (giây): nếu KHÔNG có RoundIntroController phản hồi (vd map thiếu RoundDirector) " +
                 "thì vẫn vào trận sau ngần này. LƯỚI AN TOÀN — phải LUÔN > tổng timeout nội bộ của " +
                 "RoundIntroController (2×perPersonTimeout + margin scan) để tránh race khóa input đầu trận. " +
                 "(playtest #2, điểm 1) perPersonTimeout nới lên 9s (intro act191 đầy đủ 3 vòng múa) → " +
                 "introTimeout nới theo 20s. CẦN TEST.")]
        [SerializeField] private float introTimeout = 20f;

        [Header("MỤC D — Giữ pose kết trận trước End screen")]
        [Tooltip("Giữ pose Win/Lose (giây) trước khi hiện End screen. CẦN TEST (~2.5s).")]
        [SerializeField] private float outroHold = 2.5f;

        private bool _introFinished;

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
            if (onRoundIntroFinished != null) onRoundIntroFinished.OnEventRaised += OnRoundIntroFinished;
            // DEBUG harness P12
            DebugLog.Log($"[MatchManager] OnEnable subscribe P1={(onCharSelectedP1 != null)} P2={(onCharSelectedP2 != null)} Stage={(onStageSelected != null)}");
        }

        private void OnDisable()
        {
            if (onCharSelectedP1 != null) onCharSelectedP1.OnEventRaised -= SetCharP1;
            if (onCharSelectedP2 != null) onCharSelectedP2.OnEventRaised -= SetCharP2;
            if (onStageSelected != null) onStageSelected.OnEventRaised -= OnStageSelected;
            if (onRematchPressed != null) onRematchPressed.OnEventRaised -= OnRematch;
            if (onBackToCharSelect != null) onBackToCharSelect.OnEventRaised -= OnBackToCharSelect;
            if (onMatchEnded != null) onMatchEnded.OnEventRaised -= OnMatchEnded;
            if (onRoundIntroFinished != null) onRoundIntroFinished.OnEventRaised -= OnRoundIntroFinished;
        }

        private void OnRoundIntroFinished() => _introFinished = true;

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
            DebugLog.Log("[MatchManager] → StageSelect"); // DEBUG harness P12
            _phase = MatchPhase.StageSelect;
            ShowOnly(stageSelectPanel);
        }

        private void GoToMatch()
        {
            DebugLog.Log("[MatchManager] → Match (spawn players)"); // DEBUG harness P12
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

            // Reset đồng hồ TRƯỚC intro (số hiển thị đúng), NHƯNG chưa cho chạy — chạy sau intro để đồng
            // hồ không đếm & item không rơi trong lúc pose intro. (MỤC D)
            if (matchTimer != null && resetTimerOnMatch) matchTimer.ResetTimer();

            // MỤC D — INTRO vào trận: raise OnRoundIntroStart rồi CHỜ OnRoundIntroFinished (RoundIntroController
            // khóa input + zoom camera P1→P2). Có timeout an toàn nếu map không có RoundDirector phản hồi.
            yield return RunRoundIntro();

            // CHỈ SAU intro: chạy đồng hồ + (chờ 1 frame để object map subscribe) + raise MatchStart.
            if (matchTimer != null) matchTimer.SetRunning(true);
            yield return null;
            if (onMatchStart != null) onMatchStart.RaiseEvent();
        }

        /// <summary>(MỤC D) Phát OnRoundIntroStart rồi chờ OnRoundIntroFinished (hoặc timeout). Kênh chưa
        /// wire (null) → bỏ qua intro ngay (test scene không có RoundDirector).</summary>
        private IEnumerator RunRoundIntro()
        {
            if (onRoundIntroStart == null) yield break;
            _introFinished = false;
            onRoundIntroStart.RaiseEvent();
            float t = 0f;
            while (!_introFinished && t < introTimeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!_introFinished)
                Debug.LogWarning("[MatchManager] Intro timeout — vào trận (map thiếu RoundIntroController?).", this);
        }

        private IEnumerator UnloadMapIfAny()
        {
            // OBJECT POOLING (§3.1): dọn pool VFX/projectile TRƯỚC khi despawn player + unload map — tránh
            // giữ ref object bị Destroy theo scene sang trận sau. PoolBootstrap prewarm lại ở onMatchStart.
            PrefabPool.Clear();
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
            DebugLog.Log($"[MatchManager] SetCharP1={idx} (P2={_charP2}) gate→{(_charP2 >= 0)}"); // DEBUG harness P12
            if (_charP2 >= 0) GoToStageSelect();   // cả 2 đã chọn → sang stage.
        }

        private void SetCharP2(int idx)
        {
            _charP2 = idx;
            DebugLog.Log($"[MatchManager] SetCharP2={idx} (P1={_charP1}) gate→{(_charP1 >= 0)}"); // DEBUG harness P12
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

        // MỤC D — GIỮ pose Win/Lose vài giây trước End screen. onMatchResult & onMatchEnded raise CÙNG
        // frame (WinCondition.Resolve) → MatchOutroController kịp set pose trước khi ta hold. GoToEnd (ẩn
        // HUD/hiện End) chỉ chạy SAU khi hold xong nên pose trình diễn trọn vẹn trên map còn hiển thị.
        private void OnMatchEnded() => StartCoroutine(EndRoutine());

        private IEnumerator EndRoutine()
        {
            if (outroHold > 0f) yield return new WaitForSeconds(outroHold);
            GoToEnd();
        }

        // ---- Tiện ích panel ----

        private void ShowOnly(GameObject panel)
        {
            if (charSelectPanel != null) charSelectPanel.SetActive(panel == charSelectPanel);
            if (stageSelectPanel != null) stageSelectPanel.SetActive(panel == stageSelectPanel);
            if (endScreenPanel != null) endScreenPanel.SetActive(panel == endScreenPanel);
        }
    }
}
