using UnityEngine;
using UnityEngine.UI;
using FA.Core;
using FA.GameFlow;

namespace FA.UI
{
    /// <summary>
    /// (HUD) TÊN + CHÂN DUNG nhân vật thật trên HUD trận: nghe CharSelectedChannel_P1/_P2 (chỉ raise
    /// khi bấm TIẾP TỤC ở CharSelect) → tra <see cref="CharacterRegistrySO"/> (displayName + icon —
    /// CÙNG nguồn dữ liệu với màn chọn nhân vật) → đổ vào Text/Image trên HUD.
    ///
    /// Đặt trên __GameFlowSystems (LUÔN active) chứ KHÔNG trên HUD: HUD bị SetActive(false) lúc menu
    /// nên component con không nhận event lúc người chơi chốt chọn — set text/sprite lên object
    /// inactive vẫn hợp lệ, HUD bật lên là thấy. FA.UI ref FA.GameFlow chỉ ở mức data-SO (tiền lệ
    /// MatchRulesView), không đụng system (§1.3).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FighterIdentityView : MonoBehaviour
    {
        [Header("Kênh (nghe — raise khi chốt CharSelect)")]
        [SerializeField] private IntEventChannelSO charSelectedP1;
        [SerializeField] private IntEventChannelSO charSelectedP2;

        [Header("Danh mục nhân vật (cùng asset với màn chọn)")]
        [SerializeField] private CharacterRegistrySO registry;

        [Header("HUD P1")]
        [SerializeField] private Image portraitP1;
        [SerializeField] private Text nameP1;

        [Header("HUD P2")]
        [SerializeField] private Image portraitP2;
        [SerializeField] private Text nameP2;

        private void OnEnable()
        {
            if (charSelectedP1 != null) charSelectedP1.OnEventRaised += OnP1;
            if (charSelectedP2 != null) charSelectedP2.OnEventRaised += OnP2;
        }

        private void OnDisable()
        {
            if (charSelectedP1 != null) charSelectedP1.OnEventRaised -= OnP1;
            if (charSelectedP2 != null) charSelectedP2.OnEventRaised -= OnP2;
        }

        private void OnP1(int index) => Apply(index, portraitP1, nameP1);
        private void OnP2(int index) => Apply(index, portraitP2, nameP2);

        private void Apply(int index, Image portrait, Text nameLabel)
        {
            CharacterRegistrySO.Entry e = registry != null ? registry.Get(index) : null;
            if (e == null) return;

            if (nameLabel != null && !string.IsNullOrEmpty(e.displayName))
                nameLabel.text = e.displayName;

            if (portrait != null && e.icon != null)
            {
                portrait.sprite = e.icon;
                portrait.color = Color.white;   // bỏ màu nền placeholder tối.
                portrait.preserveAspect = true;
            }
            // Chưa gán icon trong registry → giữ ô placeholder (không xóa gì).
        }
    }
}
