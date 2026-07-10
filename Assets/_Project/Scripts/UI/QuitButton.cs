using UnityEngine;
using UnityEngine.UI;

namespace FA.UI
{
    /// <summary>
    /// (P12 Main Menu) Nút THOÁT game — gắn cùng GameObject với <see cref="Button"/>, tự đăng ký onClick →
    /// <see cref="Application.Quit"/>. Trong Editor (Play mode) thì dừng Play thay vì quit (Application.Quit
    /// không có tác dụng trong Editor). KHÔNG dùng Event Channel: thoát app là hành vi cục bộ của nút,
    /// không hệ thống nào khác cần nghe (§1.3 — chỉ decouple những gì cần decouple).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class QuitButton : MonoBehaviour
    {
        private void Awake() => GetComponent<Button>().onClick.AddListener(Quit);

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
