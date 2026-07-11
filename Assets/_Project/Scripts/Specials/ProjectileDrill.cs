using UnityEngine;
using FA.Core;

namespace FA.Specials
{
    /// <summary>
    /// MŨI KHOAN (Super 2 Archer, MUGEN helper 2130): projectile GHIM vào victim, gây nhiều hit nhỏ
    /// giọt liên tục (như khoan), rồi NỔ — vụ nổ tiếp tục vài hit nhỏ giọt, hit CUỐI CÙNG mới kích
    /// knockback văng. Port từ MUGEN "hitcount≤14 + VelSet x=1 + TargetBind → anim 2131 nổ".
    ///
    /// 3 pha: (1) KHOAN — <see cref="carryHits"/> hit carryMove, mỗi <see cref="rehitFrames"/> gọi
    /// "ResetIgnore" (OnTriggerStay của HitboxAuthoring đăng ký hit mới); (2) NỔ — dừng hẳn, ẩn thân
    /// mũi tên, spawn <see cref="explosionEffect"/> (VFX + boom), thêm <see cref="blastHits"/> hit
    /// nhỏ giọt nữa; (3) VĂNG — swap "SetMove" sang <see cref="enderMove"/> (causesLaunch → P7 văng
    /// ngay), hit đó xong tự Destroy.
    ///
    /// GHIM VICTIM (TargetBind-lite, user chốt "giữ địch nguyên đó ăn hit liên tục"): hurt-slide mỗi
    /// hit đẩy victim lùi dần khỏi hitbox (mũi tên còn DỪNG khi nổ) → hit sau hụt. Nghe kênh
    /// OnHitConfirmed (block KHÔNG ghim — đúng MUGEN chỉ VelSet khi !moveguarded): hit thật đầu tiên
    /// chốt vị trí victim, mỗi FixedUpdate giữ nguyên vị trí + zero vận tốc; tự NHẢ khi quá
    /// 2×rehitFrames không có hit thật (victim burst/i-frame thoát) hoặc khi ender đánh xong (launch
    /// P7 áp SAU khi drill chết nên không bị ghim đè).
    ///
    /// KHÓA TƯ THẾ (user chốt cân bằng): giữ người bắn nguyên pose tới khi mũi tên biến mất —
    /// Acquire/Release <see cref="ProjectilePoseLock"/>; SuperState hoãn thoát khi IsHeld.
    ///
    /// SendMessage giữ pattern không ref FA.Combat (§1.3); hitbox con phải để moveOverride TRỐNG.
    /// Nhịp hit = số balance trên component (projectile không có clip — như Projectile.lifetime). CẦN TEST.
    /// Projectile cùng prefab PHẢI tắt despawnOnHit (drill tự quản vòng đời).
    /// </summary>
    [RequireComponent(typeof(Projectile), typeof(Rigidbody2D))]
    public sealed class ProjectileDrill : MonoBehaviour
    {
        [Header("Move (tham số gameplay từng hit)")]
        [Tooltip("Move cho các hit KHOAN + hit nhỏ giọt trong vụ nổ (causesLaunch=false, % nhỏ).")]
        [SerializeField] private MoveData carryMove;

        [Tooltip("Move cho hit VĂNG cuối cùng (causesLaunch=true — bay sau khi đã thấy nổ).")]
        [SerializeField] private MoveData enderMove;

        [Header("Vụ nổ")]
        [Tooltip("VFX + SFX vụ nổ — drill spawn TRỰC TIẾP khi vào pha nổ (không chờ hit).")]
        [SerializeField] private EffectData explosionEffect;

        [Header("Ghim victim (TargetBind)")]
        [Tooltip("Kênh OnHitConfirmed — hit THẬT không-block mới ghim victim (block thì bị đẩy như luật block).")]
        [SerializeField] private HitEventChannelSO hitConfirmedChannel;

        [Header("Nhịp hit (số balance — CẦN TEST)")]
        [Tooltip("Số hit khoan trước khi nổ. Tổng hit = carryHits + blastHits + 1 ender.")]
        [Min(1)] [SerializeField] private int carryHits = 6;

        [Tooltip("Số hit nhỏ giọt THÊM trong vụ nổ, trước hit văng cuối.")]
        [Min(0)] [SerializeField] private int blastHits = 3;

        [Tooltip("Cách bao nhiêu fixed-frame mở 1 hit kế (ResetIgnore — OnTriggerStay đăng ký lại).")]
        [Min(1)] [SerializeField] private int rehitFrames = 8;

        [Tooltip("Tốc độ bò xuyên người khi đã ghim (MUGEN VelSet x=1 ≈ 0.6 u/s).")]
        [Min(0f)] [SerializeField] private float drillSpeed = 0.8f;

        [Tooltip("Bán kính nhận 'hit của CHÍNH mũi tên này' (lọc như Projectile.despawnOnHitRadius).")]
        [Min(0.1f)] [SerializeField] private float ownHitRadius = 0.9f;

        [Tooltip("Vào pha nổ mà không kết được (victim thoát) → tự hủy sau bấy nhiêu fixed-frame.")]
        [Min(10)] [SerializeField] private int explosionFailsafeFrames = 90;

        // Độ phóng theo NGƯỜI BẮN (nhân vật ×2.5) — ProjectileSpawner BroadcastMessage khi spawn.
        private float _speedScale = 1f;

        /// <summary>ProjectileSpawner gọi (BroadcastMessage) — nhân drillSpeed theo scale người bắn.</summary>
        public void SetSpeedScale(float s) => _speedScale = Mathf.Max(0.01f, s);

        private Projectile _proj;
        private Rigidbody2D _rb;
        private HitEventChannelSO _channel;
        private int _hits;
        private int _frames;
        private int _explosionAge;
        private bool _drilling;
        private bool _exploded;
        private bool _done;
        private bool _poseLocked;

        // Ghim victim: chốt vị trí ở hit THẬT đầu, nhả khi lâu không hit thật (thoát bằng burst/i-frame).
        private Rigidbody2D _victimRb;
        private Vector2 _pinPos;
        private int _lastConfirmFrame;

        private void Awake()
        {
            _proj = GetComponent<Projectile>();
            _rb = GetComponent<Rigidbody2D>();
            // KHÔNG cho rb ngủ: pha nổ đứng im (velocity=0) quá timeToSleep (0.5s) là physics ru ngủ
            // body → OnTriggerStay2D NGỪNG bắn → hit văng cuối không bao giờ xảy ra.
            _rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        }

        private void Start()
        {
            // Cấp move CARRY làm move hiện hành cho hitbox con. KHÔNG dùng moveOverride (per-hitbox,
            // thắng SetMove) — drill cần swap carry→ender lúc chạy nên hitbox phải để override trống.
            BroadcastMessage("SetMove", carryMove, SendMessageOptions.DontRequireReceiver);
            _channel = _proj.HitChannel;   // spawner đã Init trước Start.
            if (_channel != null) _channel.OnEventRaised += OnHitLanded;
            if (hitConfirmedChannel != null) hitConfirmedChannel.OnEventRaised += OnHitConfirmed;
            ProjectilePoseLock.Acquire(_proj.Owner);   // giữ người bắn nguyên pose tới khi tên biến mất.
            _poseLocked = true;
        }

        private void OnDisable()
        {
            if (_channel != null) { _channel.OnEventRaised -= OnHitLanded; _channel = null; }
            if (hitConfirmedChannel != null) hitConfirmedChannel.OnEventRaised -= OnHitConfirmed;
            if (_poseLocked) { ProjectilePoseLock.Release(_proj.Owner); _poseLocked = false; }
        }

        /// <summary>Hit này có phải CỦA CHÍNH mũi tên này không (đúng owner + điểm chạm sát mình —
        /// chưởng khác cùng owner không được tính nhầm, cùng lý do Projectile.despawnOnHitRadius).</summary>
        private bool IsOwnHit(HitData hit)
        {
            if (_proj.Owner == null || !ReferenceEquals(hit.Attacker, _proj.Owner)) return false;
            return ((Vector2)transform.position - hit.ContactPoint).sqrMagnitude
                   <= ownHitRadius * ownHitRadius;
        }

        private void OnHitLanded(HitData hit)
        {
            if (_done || !IsOwnHit(hit)) return;

            _hits++;
            if (!_drilling)
            {
                _drilling = true;   // GHIM: bò chậm xuyên người — victim bị khoan tại chỗ.
                float v = drillSpeed * _speedScale; // × độ phóng người bắn (nhân vật ×2.5).
                _rb.linearVelocity = _rb.linearVelocity.sqrMagnitude > 0.0001f
                    ? _rb.linearVelocity.normalized * v
                    : new Vector2(v * Mathf.Sign(transform.lossyScale.x), 0f);
            }

            if (_hits == carryHits) Explode();                       // khoan đủ → NỔ NGAY (chưa văng).
            if (_hits == carryHits + blastHits)
                BroadcastMessage("SetMove", enderMove, SendMessageOptions.DontRequireReceiver);   // hit kế = VĂNG.
            else if (_hits > carryHits + blastHits)
            {
                _done = true;   // ender đã đánh — dừng ghim NGAY để launch P7 (áp sau hitstop) không bị đè.
                Destroy(gameObject);
            }
        }

        /// <summary>Hit THẬT (không-block) của chính mũi tên → ghim/duy trì ghim victim tại chỗ.</summary>
        private void OnHitConfirmed(HitData hit)
        {
            if (_done || !IsOwnHit(hit)) return;
            if (_victimRb == null && hit.Victim != null)
            {
                _victimRb = hit.Victim.GetComponent<Rigidbody2D>();
                if (_victimRb != null) _pinPos = _victimRb.position;
            }
            _lastConfirmFrame = _frames;
        }

        /// <summary>Vào pha NỔ: dừng hẳn, ẩn thân mũi tên (giữ hitbox cho blast hit), spawn vụ nổ.</summary>
        private void Explode()
        {
            _exploded = true;
            _rb.linearVelocity = Vector2.zero;
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;

            if (explosionEffect == null) return;
            if (explosionEffect.vfxPrefab != null)
            {
                GameObject vfx = Instantiate(explosionEffect.vfxPrefab, transform.position, Quaternion.identity);
                vfx.transform.localScale *= explosionEffect.scale;
            }
            AudioClip boom = explosionEffect.PickClip();
            if (boom != null)
            {
                // AudioSource RỜI (không PlayClipAtPoint — cái đó spatialBlend 3D): sống độc lập với
                // mũi tên (ender Destroy không cắt tiếng nổ đang vang).
                var go = new GameObject("Super2Boom");
                go.transform.position = transform.position;
                var src = go.AddComponent<AudioSource>();
                src.spatialBlend = 0f;
                src.PlayOneShot(boom, explosionEffect.volume);
                Destroy(go, boom.length + 0.2f);
            }
        }

        private void FixedUpdate()
        {
            if (!_drilling || _done) return;
            _frames++;
            if (_frames % rehitFrames == 0)
                BroadcastMessage("ResetIgnore", SendMessageOptions.DontRequireReceiver);   // mở hit kế.

            // GHIM victim (TargetBind-lite): giữ vị trí + zero vận tốc để hurt-slide không đẩy họ ra
            // khỏi hitbox giữa chuỗi. Nhả khi lâu không có hit thật (burst/i-frame thoát → tự do).
            if (_victimRb != null)
            {
                if (_frames - _lastConfirmFrame > rehitFrames * 2) _victimRb = null;
                else
                {
                    _victimRb.linearVelocity = Vector2.zero;
                    _victimRb.position = _pinPos;
                }
            }

            if (_exploded && ++_explosionAge > explosionFailsafeFrames)
            {
                _done = true;   // victim thoát giữa vụ nổ — không kết được thì tự dọn.
                Destroy(gameObject);
            }
        }
    }
}
