using FA.Core;
using UnityEngine;

namespace FA.Specials
{
    /// <summary>
    /// UBW INSTALL-MODE (Ultimate Archer, MUGEN helper 3500): nghe Animation Event
    /// <c>ChantStart</c> (đầu clip chant — duck BGM map, xem <see cref="OnChantStart"/>) rồi
    /// <c>UltimateInstall</c> (cuối clip chant) →
    ///  1. THAY BACKGROUND: spawn prefab bg UBW (bánh răng/đồi kiếm, sprite nhóm 25, sortingOrder RẤT
    ///     ÂM — nằm DƯỚI toàn bộ tilemap, chỉ hiện ở phần nền trống; map thật sau này có layer bg riêng
    ///     sẽ bị che đúng chỗ) tại tâm camera, KÍCH THƯỚC LỚN phủ vùng camera đi được (user chốt — kẻo
    ///     lộ khoảng xanh), rồi TỰ BÁM CAMERA mỗi frame trong <see cref="Update"/> (map thật to hơn scene
    ///     test cũ, CM_MatchCamera vẫn pan/zoom theo 2 người chơi suốt UBW nên bg đứng yên sẽ lộ khoảng
    ///     trống ở rìa nếu không bám); BGM UBW loop (AudioSource riêng của <see cref="music"/>) — BGM map
    ///     ĐƯỢC DUCK (Pause) qua kênh <see cref="onBgmDuckRequested"/>/<see cref="onBgmRestoreRequested"/>
    ///     (UIAudioManager FA.UI nghe, KHÔNG ref thẳng module UI) NGAY LÚC CHANT BẮT ĐẦU — cùng lúc
    ///     MusicSequencePlayer phát nhạc UBW, không đợi tới Install. KHÔNG ẩn TilemapRenderer (sàn/tường giữ nguyên).
    ///  2. BUFF (implement <see cref="IBuffSource"/> → BuffAggregator tự gom vì component nằm sẵn trên
    ///     prefab): +DMG gây ra (<see cref="damageDealtMult"/>), −DMG nhận vào
    ///     (<see cref="damageTakenMult"/> — thành viên MỚI của IBuffSource, KnockbackPercent nhân).
    ///  3. DRAIN NỘ: nạp đầy thanh nộ lúc install (RefundRage 1) rồi tiêu dần qua
    ///     <see cref="IRageSpender"/> trong <see cref="drainSeconds"/> giây — thanh nộ chính là đồng hồ
    ///     UBW trên HUD. Hết nộ → <see cref="ForceEnd"/> tự revert (bg/BGM/buff về như cũ).
    ///  4. <see cref="ForceEnd"/> public: 2 super-UBW tương lai gọi để "dùng chiêu là hết nộ luôn"
    ///     (user chốt). <see cref="IsInstalled"/> cho SpecialController tra khi swap moveset (làm sau).
    /// Đặt FA.Specials (cạnh RageMode/Defense), chỉ ref Core + Unity. Số drain/mult = balance
    /// (Inspector, CẦN TEST).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UBWInstallController : MonoBehaviour, IBuffSource
    {
        [Header("Tham chiếu")]
        [Tooltip("AnimationEventRelay cùng prefab. Trống = tìm trong children.")]
        [SerializeField] private AnimationEventRelay relay;

        [Tooltip("Prefab background UBW (SpriteRenderer nhóm 25, sortingOrder -13..-7 — feedback test: " +
                 "Swamp có BG_Water(-30)/BG_Mist(-20) tràn lên che mất UBW khi để quá âm. PHẢI ở dưới " +
                 "ground tilemap mọi map (Ice -5, Fire/Swamp -6) và trên MỌI lớp bg xa nhất từng map.")]
        [SerializeField] private GameObject backgroundPrefab;

        [Tooltip("Nhạc nền chuỗi UBW (MusicSequencePlayer cùng prefab — tự start ở ChantStart; " +
                 "controller này CHỈ stop khi thoát UBW).")]
        [SerializeField] private MusicSequencePlayer music;

        [Tooltip("Aura đỏ hiện TRONG UBW (user chốt: aura sau khi anim 3 part xong, KHÔNG SFX). " +
                 "Play khi install, Stop khi thoát.")]
        [SerializeField] private ParticleSystem auraParticles;

        [Tooltip("Component RageBar (IRageSpender). Trống = tìm children.")]
        [SerializeField] private MonoBehaviour rageSpenderBehaviour;

        [Header("BGM map (duck/restore — UIAudioManager FA.UI nghe qua kênh Core, KHÔNG ref thẳng FA.UI)")]
        [Tooltip("OnBgmDuckRequested (VoidEventChannelSO, Data/Audio) — raise lúc Install để tạm dừng BGM map.")]
        [SerializeField] private VoidEventChannelSO onBgmDuckRequested;
        [Tooltip("OnBgmRestoreRequested (VoidEventChannelSO, Data/Audio) — raise lúc revert để BGM map phát tiếp.")]
        [SerializeField] private VoidEventChannelSO onBgmRestoreRequested;

        [Header("Buff + thời lượng (CẦN TEST)")]
        [Tooltip("Nhân DMG GÂY RA khi trong UBW.")]
        [Min(1f)] [SerializeField] private float damageDealtMult = 1.3f;

        [Tooltip("Nhân DMG NHẬN VÀO khi trong UBW (<1 = trâu hơn).")]
        [Range(0.1f, 1f)] [SerializeField] private float damageTakenMult = 0.7f;

        [Tooltip("Thanh nộ tụt từ đầy về 0 trong bấy nhiêu giây (10-15s theo GDD-user).")]
        [Min(1f)] [SerializeField] private float drainSeconds = 12f;

        private IRageSpender _rage;
        private RageModeController _rageMode;   // tra IsUltimateActive — ngừng drain khi đòn kết đang chạy.
        private GameObject _bg;
        private Camera _cam;
        private bool _installed;
        private bool _ending;      // đang fade đen chờ revert — chặn double-end.
        private bool _chantActive; // đã raise duck ở ChantStart, chờ Install — hoặc chant bị huỷ giữa chừng chưa kịp Install.

        /// <summary>Đang trong UBW (2 super mới tra để biết dùng bộ chiêu nào — làm sau).</summary>
        public bool IsInstalled => _installed;

        // ===== IBuffSource (BuffAggregator gom — không active thì ×1.0 vô hại) =====
        public float DamageDealtMult => _installed ? damageDealtMult : 1f;
        public float KnockbackDealtMult => 1f;
        public float HitstunTakenMult => 1f;
        public float MoveSpeedMult => 1f;
        public float DamageTakenMult => _installed ? damageTakenMult : 1f;
        public bool IsRageActive => false;
        public bool IsUltimateActive => false;

        private void Awake()
        {
            if (relay == null) relay = GetComponentInChildren<AnimationEventRelay>();
            _rage = rageSpenderBehaviour as IRageSpender;
            if (_rage == null) _rage = GetComponentInChildren<IRageSpender>();
            _rageMode = GetComponent<RageModeController>();
        }

        private void OnEnable()
        {
            if (relay != null)
            {
                relay.ChantStartEvent += OnChantStart;
                relay.UltimateInstallEvent += Install;
            }
        }

        private void OnDisable()
        {
            if (relay != null)
            {
                relay.ChantStartEvent -= OnChantStart;
                relay.UltimateInstallEvent -= Install;
            }
            RevertNow();   // disable/đổi scene: dọn tức thời, không chuyển cảnh.
        }

        /// <summary>(v2) Duck BGM map NGAY LÚC CHANT BẮT ĐẦU — cùng lúc MusicSequencePlayer.StartSequence()
        /// phát nhạc UBW (nghe cùng ChantStart), không đợi tới cuối chant (UltimateInstall) mới tắt như
        /// trước (feedback test: nhạc UBW + giọng chant đã vang suốt clip chant mà BGM map vẫn kêu chồng).</summary>
        private void OnChantStart()
        {
            if (_chantActive) return;
            _chantActive = true;
            onBgmDuckRequested?.RaiseEvent();
        }

        private void Install()
        {
            if (_installed) return;
            _installed = true;
            _ending = false;
            _chantActive = true; // Install luôn xảy ra SAU ChantStart — đã duck từ đó rồi.

            // Thanh nộ = đồng hồ UBW: nạp đầy rồi drain (HUD sẵn có hiển thị, không cần UI mới).
            _rage?.RefundRage(1f);

            if (backgroundPrefab != null)
            {
                _cam = Camera.main;
                Vector3 center = _cam != null
                    ? new Vector3(_cam.transform.position.x, _cam.transform.position.y, 0f)
                    : transform.position;
                _bg = Instantiate(backgroundPrefab, center, Quaternion.identity);
            }

            if (auraParticles != null) auraParticles.Play();
        }

        /// <summary>Kết thúc UBW CÓ CHUYỂN CẢNH (user chốt): đen màn nhanh → revert lúc đen kịt →
        /// hiện lại như chưa có gì. Dùng cho hết-nộ LẪN sau super-UBW (sắp làm). Cũng an toàn khi gọi
        /// lúc chant bị huỷ TRƯỚC khi kịp Install (chưa có bg/buff gì) — chỉ cần trả BGM map lại, KHÔNG
        /// chuyển cảnh (UltimateState.Exit gọi vô điều kiện, xem class đó). Idempotent.</summary>
        public void ForceEnd()
        {
            if (_ending || (!_installed && !_chantActive)) return;
            if (_installed)
            {
                _ending = true;
                // Buff tắt NGAY (fairness — phần kéo dài chỉ là chuyển cảnh hình ảnh).
                _installed = false;
                ScreenFadeQuad.Play(0.2f, 0.15f, 0.45f, RevertNow);
            }
            else
            {
                // Đã duck ở ChantStart nhưng chant bị huỷ (vd bị đánh trúng) trước khi tới Install —
                // chưa có gì để chuyển cảnh, chỉ cần dọn ngay.
                RevertNow();
            }
        }

        /// <summary>Revert tức thời KHÔNG chuyển cảnh (OnDisable/đổi scene/chant bị huỷ giữa chừng).</summary>
        private void RevertNow()
        {
            _installed = false;
            _ending = false;
            bool wasDucked = _chantActive;
            _chantActive = false;
            if (_bg != null) { Destroy(_bg); _bg = null; }
            if (music != null) music.StopMusic();
            if (auraParticles != null) auraParticles.Stop();
            if (wasDucked) onBgmRestoreRequested?.RaiseEvent(); // chỉ trả BGM nếu trước đó thật sự đã duck.
        }

        private void Update()
        {
            // Background bám theo camera mỗi frame — spawn 1 lần tại Install() nhưng KHÔNG tự follow,
            // trong khi CM_MatchCamera (CinemachineGroupFraming) vẫn zoom/pan theo 2 người chơi suốt UBW
            // → map càng lớn, camera càng dễ đi xa khỏi điểm spawn ban đầu, lộ khoảng trống ở rìa (feedback
            // test — bug thấy khi có map thật, size lớn hơn hẳn scene test cũ).
            if (_installed && _bg != null && _cam != null)
            {
                Vector3 p = _cam.transform.position;
                Vector3 bgPos = _bg.transform.position;
                _bg.transform.position = new Vector3(p.x, p.y, bgPos.z);
            }

            if (!_installed || _rage == null) return;
            // Ultimate (đòn kết) đang chạy → NGỪNG drain: UBW không được "hết giờ" giữa cinematic
            // (UltimateState tự ForceEnd khi xong — cùng lý do RageModeController khóa timer).
            if (_rageMode != null && _rageMode.IsUltimateActive) return;
            // Drain đều: thanh nộ tụt hết trong drainSeconds. TrySpendRage fail = còn dưới 1 nhịp → hết.
            float step = Time.deltaTime / drainSeconds;
            if (step <= 0f) return;
            if (!_rage.TrySpendRage(step) || _rage.Fraction <= 0f) ForceEnd();
        }
    }
}
