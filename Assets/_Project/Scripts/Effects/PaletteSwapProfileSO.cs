using System;
using System.Collections.Generic;
using UnityEngine;

namespace FA.Effects
{
    /// <summary>
    /// BẢNG ĐỔI MÀU (palette swap kiểu MUGEN) — danh sách cặp màu gốc→màu thay cho BỘ ĐỒ ALT của
    /// nhân vật (P2 khi 2 người chọn trùng char). Lấy màu gốc bằng eyedropper trực tiếp từ sprite
    /// trong Inspector (mỗi mảng màu phẳng của bộ đồ = 1 cặp; sprite MUGEN ít màu nên thường 3-6 cặp
    /// là đủ nguyên bộ áo). Tối đa 8 cặp (giới hạn shader FA/PaletteSwapBake).
    /// </summary>
    [CreateAssetMenu(menuName = "FA/Effects/Palette Swap Profile", fileName = "PaletteSwap_Alt")]
    public sealed class PaletteSwapProfileSO : ScriptableObject
    {
        public const int MaxSwaps = 8;

        [Serializable]
        public struct ColorSwap
        {
            [Tooltip("Màu GỐC trên sprite (eyedropper từ texture nhân vật).")]
            public Color from;
            [Tooltip("Màu THAY cho bộ đồ alt.")]
            public Color to;
        }

        [Tooltip("Các cặp màu gốc→thay (tối đa 8). CẦN chỉnh theo sprite thật.")]
        public List<ColorSwap> swaps = new List<ColorSwap>();

        [Tooltip("Dung sai so màu (khoảng cách RGB 0..~1.7): đủ lớn để ăn cả biên anti-alias nhẹ, " +
                 "đủ nhỏ để không lan sang màu da/tóc. CẦN TEST theo sprite.")]
        [Range(0.01f, 0.5f)] public float tolerance = 0.08f;

        public int SwapCount => Mathf.Min(swaps.Count, MaxSwaps);

        /// <summary>Đổ tham số vào material bake (shader FA/PaletteSwapBake).</summary>
        public void FillMaterial(Material mat)
        {
            var from = new Vector4[MaxSwaps];
            var to = new Vector4[MaxSwaps];
            int n = SwapCount;
            for (int i = 0; i < n; i++)
            {
                from[i] = swaps[i].from;
                to[i] = swaps[i].to;
            }
            mat.SetInt("_SwapCount", n);
            mat.SetFloat("_Tolerance", tolerance);
            mat.SetVectorArray("_FromColors", from);
            mat.SetVectorArray("_ToColors", to);
        }
    }
}
