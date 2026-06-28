using System.Collections.Generic;
using UnityEngine;

namespace FA.Core
{
    /// <summary>
    /// BẢNG special/super của 1 nhân vật (P9, research §3.7). ScriptableObject ở FA.Core (KHÔNG nhồi vào
    /// CharacterData của FA.Movement — sẽ buộc FA.Specials ref Movement, vi phạm §1.3). Cầu nối sạch:
    /// FA.Specials chỉ ref Core → đọc bảng này; thêm đòn = thêm 1 ENTRY asset (extensible §1.2), KHÔNG
    /// sửa code.
    ///
    /// Mỗi entry map (motion + nút) → 1 <see cref="MoveData"/>. SpecialController quét list: với mỗi entry
    /// dùng <see cref="CommandParser"/> kiểm motion khớp + nút edge → ra MoveData tương ứng. Entry CHARGE
    /// (charged != null) = biến thể MẠNH HƠN khi giữ nút ≥ <see cref="MoveData.chargeFrames"/> của entry gốc.
    ///
    /// Super (nút riêng Super1/Super2, KHÔNG motion) cũng khai bằng entry motion=None. SpecialController
    /// phân nhánh Special vs Super theo <see cref="MoveData.category"/> (Special/Super) — KHÔNG hardcode.
    /// </summary>
    [CreateAssetMenu(menuName = "FA/Combat/Move Set Data", fileName = "MoveSet")]
    public sealed class MoveSetData : ScriptableObject
    {
        /// <summary>
        /// 1 dòng bảng special/super. motion=None + button=Super1/Super2 → super. motion≠None → special
        /// motion-input. <see cref="charged"/> (tùy chọn) = biến thể giữ-nút-lâu (research §3.6).
        /// </summary>
        [System.Serializable]
        public struct Entry
        {
            [Tooltip("Motion kích đòn (None cho super nút-riêng).")]
            public MotionInput motion;

            [Tooltip("Nút kích (L/M/H cho special; Super1/Super2 cho super).")]
            public InputButton button;

            [Tooltip("MoveData ra khi khớp motion+nút (level cơ bản).")]
            public MoveData move;

            [Tooltip("(Tùy chọn) Biến thể CHARGED — dùng khi giữ nút ≥ move.chargeFrames. Để trống = " +
                     "không có charge level riêng.")]
            public MoveData charged;
        }

        [Tooltip("Danh sách entry. SpecialController quét theo THỨ TỰ — đặt đòn ƯU TIÊN (vd super, DP) " +
                 "lên TRƯỚC QCF để khớp đúng khi motion lồng nhau (DP chứa QCF-con).")]
        [SerializeField] private List<Entry> entries = new List<Entry>();

        /// <summary>Số entry (SpecialController duyệt).</summary>
        public int Count => entries != null ? entries.Count : 0;

        /// <summary>Truy cập entry thứ i (read-only). SpecialController dùng để khớp motion+nút.</summary>
        public Entry Get(int i) => entries[i];
    }
}
