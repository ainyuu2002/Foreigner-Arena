using System.Collections;
using UnityEngine;
using FA.Core;

namespace FA.UI
{
    /// <summary>
    /// (P12) BANNER "FIGHT!" ĐẦU TRẬN — nghe <c>OnRoundIntroFinished</c> (RoundIntroController raise sau
    /// khi CẢ 2 nhân vật diễn xong animation Intro, đúng lúc mở input) → pop chữ FIGHT! giữa màn
    /// (scale thu về + hold + fade) rồi tự ẩn. KHÔNG đụng code gameplay (§1.3) — chỉ subscribe kênh Core.
    ///
    /// Đặt trên ROOT LUÔN ACTIVE (canvas Menu_EndScreen / canvas harness) — CÙNG LÝ DO EndScreenView:
    /// HUD được MatchManager bật ĐÚNG frame event này raise → component nằm trong HUD subscribe muộn là
    /// lỡ sự kiện. Banner là CHILD riêng (inactive mặc định), view bật/tắt nó khi diễn.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FightBannerView : MonoBehaviour
    {
        [Header("Kênh sự kiện")]
        [Tooltip("OnRoundIntroFinished (VoidEventChannelSO) — RoundIntroController raise khi intro P1+P2 xong.")]
        [SerializeField] private VoidEventChannelSO channel;

        [Header("Hiển thị")]
        [Tooltip("Child chứa nội dung FIGHT! (inactive mặc định, cần CanvasGroup — builder dựng sẵn).")]
        [SerializeField] private GameObject banner;

        [Header("VFX sprite sequence (tùy chọn — builder wire khi có PNG seq tại Art/UI/FightVFX)")]
        [Tooltip("Image hiện frame VFX (child của banner). Null hoặc frames trống → chế độ text pop.")]
        [SerializeField] private UnityEngine.UI.Image frameImage;
        [Tooltip("Chuỗi frame VFX (đã có sẵn pha vào trong hiệu ứng; kết thúc còn hiện → view tự fade out).")]
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float framesPerSecond = 30f;

        [Header("Nhịp diễn (giây)")]
        [Tooltip("Pha pop: scale 2.2 → 1 (ease-out) + hiện dần.")]
        [SerializeField] private float popDuration = 0.25f;
        [Tooltip("Giữ nguyên cỡ thật cho người chơi đọc.")]
        [SerializeField] private float holdDuration = 0.55f;
        [Tooltip("Mờ dần rồi tắt.")]
        [SerializeField] private float fadeDuration = 0.3f;

        /// <summary>Tiếng hô "FIGHT!" — nạp từ Resources theo tên (cùng convention UI_Click/BGM_*).
        /// Thiếu file → im lặng, banner vẫn diễn bình thường.</summary>
        private const string SfxResource = "SFX_Fight";

        private Coroutine _routine;
        private AudioClip _sfx;
        private bool _sfxLoaded; // nạp lười 1 lần — không Resources.Load mỗi trận.

        private void OnEnable() { if (channel != null) channel.OnEventRaised += OnIntroFinished; }

        private void OnDisable()
        {
            if (channel != null) channel.OnEventRaised -= OnIntroFinished;
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            if (banner != null) banner.SetActive(false);
        }

        private void OnIntroFinished()
        {
            if (banner == null) return;
            if (!_sfxLoaded) { _sfx = Resources.Load<AudioClip>(SfxResource); _sfxLoaded = true; }
            UIAudioManager.PlaySfx(_sfx); // tiếng hô FIGHT — route group SFX, ăn slider Settings.
            if (_routine != null) StopCoroutine(_routine); // rematch nhanh — diễn lại từ đầu.
            _routine = StartCoroutine(ShowRoutine());
            DebugLog.Log($"[FA.UI] FIGHT! banner (sfx: {(_sfx != null ? "OK" : "thiếu " + SfxResource)})"); // marker test.
        }

        private IEnumerator ShowRoutine()
        {
            var t = banner.transform;
            var group = banner.GetComponent<CanvasGroup>();
            banner.SetActive(true);

            bool vfxMode = frameImage != null && frames != null && frames.Length > 0;
            if (vfxMode)
            {
                // VFX tự animate pha VÀO (frame đầu trống) → không pop scale, chỉ chiếu tuần tự.
                t.localScale = Vector3.one;
                if (group != null) group.alpha = 1f;
                float frameTime = 1f / Mathf.Max(1f, framesPerSecond);
                for (int i = 0; i < frames.Length; i++)
                {
                    frameImage.sprite = frames[i];
                    yield return new WaitForSeconds(frameTime);
                }
                yield return new WaitForSeconds(holdDuration); // frame cuối còn hiện chữ — giữ thêm chút cho đọc.
            }
            else
            {
                // Chế độ text: pop to → cỡ thật, ease-out quadratic (cảm giác "đập" xuống màn).
                for (float e = 0f; e < popDuration; e += Time.deltaTime)
                {
                    float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(e / popDuration), 2f);
                    t.localScale = Vector3.one * Mathf.LerpUnclamped(2.2f, 1f, k);
                    if (group != null) group.alpha = k;
                    yield return null;
                }
                t.localScale = Vector3.one;
                if (group != null) group.alpha = 1f;
                yield return new WaitForSeconds(holdDuration);
            }

            for (float e = 0f; e < fadeDuration; e += Time.deltaTime)
            {
                if (group != null) group.alpha = 1f - Mathf.Clamp01(e / fadeDuration);
                yield return null;
            }

            banner.SetActive(false);
            if (group != null) group.alpha = 1f; // trả alpha cho lần diễn sau (rematch).
            _routine = null;
        }
    }
}
