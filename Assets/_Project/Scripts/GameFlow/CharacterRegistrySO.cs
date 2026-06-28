using System.Collections.Generic;
using UnityEngine;

namespace FA.GameFlow
{
    /// <summary>
    /// (P12, research §5) DANH MỤC nhân vật chọn được — extensible (§1.2): thêm nhân vật = thêm 1 entry +
    /// prefab variant P1/P2, KHÔNG sửa code. CharacterSelectView (FA.UI) đọc để dựng lưới button;
    /// PlayerSpawner (FA.GameFlow) đọc để Instantiate ĐÚNG prefab variant theo lựa chọn.
    ///
    /// QUYẾT ĐỊNH (research §3.7): KHÔNG swap CharacterData runtime (field private trong CharacterMotor) →
    /// mỗi nhân vật = prefab HOÀN CHỈNH (đã gán CharacterData/MoveSet/sprite). 2 variant P1/P2 khác nhau ở
    /// inputScheme (Keyboard/Gamepad) + Layer Player1/Player2 → KHÔNG sửa CharacterMotor (giữ Movement nguyên).
    /// prefab field kiểu GameObject (Core/GameFlow không ref Movement nên không gõ kiểu CharacterMotor được —
    /// Instantiate GameObject là đủ, Unity-native).
    /// </summary>
    [CreateAssetMenu(menuName = "FA/GameFlow/Character Registry", fileName = "CharacterRegistry")]
    public sealed class CharacterRegistrySO : ScriptableObject
    {
        [System.Serializable]
        public sealed class Entry
        {
            [Tooltip("Tên hiển thị trên màn chọn.")]
            public string displayName = "Fighter";

            [Tooltip("Icon hiển thị (Sprite). Tùy chọn — chưa có thì để trống.")]
            public Sprite icon;

            [Tooltip("Prefab variant P1 (inputScheme=Keyboard, Layer Player1). PlayerSpawner Instantiate cái này cho P1.")]
            public GameObject prefabP1;

            [Tooltip("Prefab variant P2 (inputScheme=Gamepad, Layer Player2). PlayerSpawner Instantiate cái này cho P2.")]
            public GameObject prefabP2;
        }

        [Tooltip("Danh sách nhân vật. Thêm = thêm entry + 2 prefab variant. KHÔNG sửa code.")]
        [SerializeField] private List<Entry> entries = new List<Entry>();

        public int Count => entries.Count;

        public Entry Get(int index)
            => (index >= 0 && index < entries.Count) ? entries[index] : null;

        /// <summary>Prefab variant theo slot (P1→prefabP1, P2→prefabP2). PlayerSpawner dùng.</summary>
        public GameObject GetPrefab(int index, FA.Core.PlayerSlot slot)
        {
            Entry e = Get(index);
            if (e == null) return null;
            return slot == FA.Core.PlayerSlot.P1 ? e.prefabP1 : e.prefabP2;
        }
    }
}
