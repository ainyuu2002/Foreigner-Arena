using System.Collections;
using FA.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FA.Items
{
    /// <summary>
    /// Launcher chỉ dành cho scene Items: ẩn phần trình bày của Lobby rồi phát các lựa chọn cố định
    /// qua đúng Event Channel của GameFlow để vào thẳng map test. Lobby và MatchManager không cần
    /// nhánh test riêng, nên flow production vẫn giữ nguyên.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ItemsSceneTestLauncher : MonoBehaviour
    {
        [Header("Phần Lobby cần ẩn trong scene Items")]
        [SerializeField] private GameObject[] menuObjectsToHide;
        [Tooltip("Ẩn tạm CharSelect/StageSelect trong lúc phát sự kiện để không chớp UI, sau đó bật lại cho EndScreen.")]
        [SerializeField] private GameObject flowScreensRoot;

        [Header("Event Channel của GameFlow")]
        [SerializeField] private IntEventChannelSO onCharSelectedP1;
        [SerializeField] private IntEventChannelSO onCharSelectedP2;
        [SerializeField] private IntEventChannelSO onStageSelected;

        [Header("Lựa chọn test (index trong Registry)")]
        [Min(0)] [SerializeField] private int playerOneCharacterIndex;
        [Min(0)] [SerializeField] private int playerTwoCharacterIndex;
        [Min(0)] [SerializeField] private int stageIndex;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += DisableLegacyDebugText;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= DisableLegacyDebugText;
        }

        private void Awake()
        {
            if (menuObjectsToHide == null) return;

            foreach (GameObject menuObject in menuObjectsToHide)
            {
                if (menuObject != null) menuObject.SetActive(false);
            }

            if (flowScreensRoot != null) flowScreensRoot.SetActive(false);
        }

        private IEnumerator Start()
        {
            // Đợi MatchManager.OnEnable + Start hoàn tất trước khi phát lựa chọn tự động.
            yield return null;

            if (onCharSelectedP1 == null || onCharSelectedP2 == null || onStageSelected == null)
            {
                Debug.LogError("[ItemsSceneTestLauncher] Thiếu Event Channel để vào trận test.", this);
                if (flowScreensRoot != null) flowScreensRoot.SetActive(true);
                yield break;
            }

            onCharSelectedP1.RaiseEvent(playerOneCharacterIndex);
            onCharSelectedP2.RaiseEvent(playerTwoCharacterIndex);

            // Tách stage selection sang frame kế tiếp để phase CharSelect -> StageSelect ổn định.
            yield return null;
            onStageSelected.RaiseEvent(stageIndex);
            if (flowScreensRoot != null) flowScreensRoot.SetActive(true);
        }

        private static void DisableLegacyDebugText(Scene scene, LoadSceneMode mode)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MysteryBlockPickupDebugText debugText in
                         root.GetComponentsInChildren<MysteryBlockPickupDebugText>(true))
                {
                    debugText.enabled = false;
                }
            }
        }
    }
}
