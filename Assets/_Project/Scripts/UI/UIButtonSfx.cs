using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FA.UI
{
    /// <summary>
    /// (P12 Audio) SFX cho 1 nút — <see cref="UIAudioManager"/> tự gắn lúc runtime cho mọi Button,
    /// KHÔNG thêm tay vào prefab/builder (không dirty asset, không diff rác). Click = chuột/tap;
    /// hover = trỏ chuột vào HOẶC được chọn bằng gamepad/bàn phím (ISelectHandler — dự án có tay cầm).
    /// Nút disabled thì im lặng (khớp cảm nhận: bấm không ăn thì không kêu).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIButtonSfx : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, ISelectHandler
    {
        private Selectable _selectable;

        private void Awake() => _selectable = GetComponent<Selectable>();

        private bool Interactable => _selectable == null || _selectable.IsInteractable();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Interactable) UIAudioManager.PlayClick();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Interactable) UIAudioManager.PlayHover();
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (Interactable) UIAudioManager.PlayHover();
        }
    }
}
