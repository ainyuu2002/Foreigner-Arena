using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FA.UI
{
    /// <summary>
    /// (P12 Menu FX) SÉT LÓE nền — thỉnh thoảng (interval ngẫu nhiên) nháy 1 Image full-screen xanh neon
    /// 2–3 nhịp rất ngắn (kiểu chớp giật trong bão) rồi tắt. Code-only tầng 1: chưa cần sprite tia sét có
    /// nhánh — chỉ "ánh chớp" đã đủ cảm giác bầu trời năng lượng bất ổn. Image nên để raycastTarget=false
    /// (không chặn click). Thuần trình bày (§1.3).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LightningFlashFX : MonoBehaviour
    {
        [Tooltip("Image full-screen màu sét (xanh neon/tím nhạt). Alpha do FX điều khiển, bắt đầu 0.")]
        [SerializeField] private Image flashImage;

        [Tooltip("Khoảng chờ NGẪU NHIÊN giữa 2 đợt sét (giây): x=min, y=max.")]
        [SerializeField] private Vector2 intervalRange = new Vector2(4f, 10f);

        [Tooltip("Số nhịp nháy mỗi đợt (chớp giật 2–3 cái).")]
        [SerializeField] private int flickers = 3;

        [Tooltip("Alpha đỉnh của ánh chớp (0.2–0.4 — đủ thấy, không chói).")]
        [SerializeField] private float maxAlpha = 0.30f;

        private Coroutine _loop;

        private void OnEnable()
        {
            if (flashImage != null)
            {
                SetAlpha(0f);
                _loop = StartCoroutine(Loop());
            }
        }

        private void OnDisable()
        {
            if (_loop != null) StopCoroutine(_loop);
            if (flashImage != null) SetAlpha(0f);
        }

        private IEnumerator Loop()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(Random.Range(intervalRange.x, intervalRange.y));
                int n = Mathf.Max(1, flickers);
                for (int i = 0; i < n; i++)
                {
                    // Nhịp sau yếu dần (chớp chính rồi dư âm) — trông tự nhiên hơn đều tăm tắp.
                    SetAlpha(maxAlpha * Random.Range(0.55f, 1f) * (1f - 0.25f * i));
                    yield return new WaitForSecondsRealtime(Random.Range(0.03f, 0.07f));
                    SetAlpha(0f);
                    yield return new WaitForSecondsRealtime(Random.Range(0.04f, 0.09f));
                }
            }
        }

        private void SetAlpha(float a)
        {
            Color c = flashImage.color; c.a = a; flashImage.color = c;
        }
    }
}
