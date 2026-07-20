using System.Collections;
using FA.Core;
using UnityEngine;

namespace FA.Items
{
    /// <summary>Tự chọn nhân vật và map khi mở scene test Items.</summary>
    [DisallowMultipleComponent]
    public sealed class ItemsSceneTestLauncher : MonoBehaviour
    {
        [Header("Phần Lobby cần ẩn trong scene Items")]
        [SerializeField] private GameObject[] menuObjectsToHide;
        [SerializeField] private GameObject flowScreensRoot;

        [Header("Event Channel của GameFlow")]
        [SerializeField] private IntEventChannelSO onCharSelectedP1;
        [SerializeField] private IntEventChannelSO onCharSelectedP2;
        [SerializeField] private IntEventChannelSO onStageSelected;

        [Header("Lựa chọn test (index trong Registry)")]
        [Min(0)] [SerializeField] private int playerOneCharacterIndex;
        [Min(0)] [SerializeField] private int playerTwoCharacterIndex;
        [Min(0)] [SerializeField] private int stageIndex;

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
            // Đợi GameFlow khởi tạo xong.
            yield return null;

            if (onCharSelectedP1 == null || onCharSelectedP2 == null || onStageSelected == null)
            {
                Debug.LogError("[ItemsSceneTestLauncher] Thiếu Event Channel để vào trận test.", this);
                if (flowScreensRoot != null) flowScreensRoot.SetActive(true);
                yield break;
            }

            onCharSelectedP1.RaiseEvent(playerOneCharacterIndex);
            onCharSelectedP2.RaiseEvent(playerTwoCharacterIndex);

            // Chọn map ở frame kế tiếp để GameFlow đổi phase.
            yield return null;
            onStageSelected.RaiseEvent(stageIndex);
            if (flowScreensRoot != null) flowScreensRoot.SetActive(true);
        }

    }
}
