using System.Collections.Generic;
using UnityEngine;

namespace FA.GameFlow
{
    /// <summary>
    /// (P12, research §5) DANH MỤC map chọn được — extensible (§1.2): thêm map = thêm 1 entry (tên scene
    /// build settings), KHÔNG sửa code. StageSelectView (FA.UI) đọc để dựng lưới; MatchManager (FA.GameFlow)
    /// đọc <c>sceneName</c> để <c>SceneManager.LoadSceneAsync(..., Additive)</c> map P11 thật (research §3.1).
    ///
    /// Dùng tên scene (string) thay SceneReference (tránh phụ thuộc editor-only API). Scene phải nằm trong
    /// Build Settings — NEEDS-MANUAL khi dựng map.
    /// </summary>
    [CreateAssetMenu(menuName = "FA/GameFlow/Stage Registry", fileName = "StageRegistry")]
    public sealed class StageRegistrySO : ScriptableObject
    {
        [System.Serializable]
        public sealed class Entry
        {
            [Tooltip("Tên hiển thị trên màn chọn map.")]
            public string displayName = "Stage";

            [Tooltip("Icon (Sprite). Tùy chọn.")]
            public Sprite icon;

            [Tooltip("Tên SCENE map (đã thêm vào Build Settings). MatchManager load additive theo tên này.")]
            public string sceneName = "";
        }

        [Tooltip("Danh sách map. Thêm = thêm entry + scene trong Build Settings. KHÔNG sửa code.")]
        [SerializeField] private List<Entry> entries = new List<Entry>();

        public int Count => entries.Count;

        public Entry Get(int index)
            => (index >= 0 && index < entries.Count) ? entries[index] : null;

        public string GetSceneName(int index)
        {
            Entry e = Get(index);
            return e != null ? e.sceneName : null;
        }
    }
}
