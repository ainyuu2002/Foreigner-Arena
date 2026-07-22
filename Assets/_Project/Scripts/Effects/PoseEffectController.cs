using System.Collections;
using FA.Core;
using UnityEngine;

namespace FA.Effects
{
    /// <summary>
    /// (MỤC D — NHÓM 2, tinh chỉnh playtest #2) VFX/SFX cho INTRO/WIN/LOSE/DRAW — MIRROR
    /// <see cref="MovementEffectController"/>: poll Animator "StateId" mỗi frame, edge-detect
    /// (42=Intro/43=Win/44=Lose/45=Draw — literal, FA.Effects không ref FA.Movement/FA.Stage), fire
    /// ĐÚNG 1 LẦN mỗi lần vào state từ <see cref="EffectData"/> slot RIÊNG (introFx/winFx/loseFx/drawFx)
    /// — KHÔNG dùng chung mảng effects[] của <see cref="CharacterEffectPlayer"/> (mảng đó bị các
    /// SetupTool khác ghi đè toàn bộ, không bền để chèn slot mới).
    ///
    /// Nội dung ĐÚNG dữ liệu MUGEN (Archer.air/.cns) — KHÔNG bịa: Intro (statedef 191) voice S191,0/1 +
    /// aura 13-23 (đặt CHÂN — mỏm đá) + VFX chuyển trang phục 1910/1911 (13-25..28, additive) fire lúc
    /// RecoveryEnd; Win (statedef 180/181) voice S181,0..3 (random 4) + aura 786-0..5 (nhỏ, trắng, mũi
    /// kiếm); Lose (statedef 170) voice S170,0 (KHÔNG VFX, KHÔNG fade); Draw (StateId 45, dùng chung
    /// Lose.anim) → MỜ DẦN thân nhân vật rồi voice S171,0 đúng lúc biến mất. Dựng qua
    /// <c>PoseFxSetupTool</c> (Editor).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PoseEffectController : MonoBehaviour
    {
        // StateId dùng chung toàn dự án (memory fighter-stateid-map): Intro=42, Win=43, Lose=44, Draw=45.
        private const int IdIntro = 42, IdWin = 43, IdLose = 44, IdDraw = 45;

        [Header("Tham chiếu (trống = tự tìm trên object)")]
        [SerializeField] private Animator animator;
        [SerializeField] private AudioSource audioSource;
        [Tooltip("AnimationEventRelay cùng object Animator (RecoveryEnd cuối clip Intro) — trống = tự tìm.")]
        [SerializeField] private AnimationEventRelay relay;
        [Tooltip("SpriteRenderer THÂN nhân vật — dùng để MỜ DẦN lúc Draw (StateId 45). Trống = tự tìm.")]
        [SerializeField] private SpriteRenderer bodyRenderer;

        [Header("Slot hiệu ứng (MỤC D — theo MUGEN, KHÔNG bịa)")]
        [Tooltip("Intro (statedef 191): voice S191,0/1 (random 1 trong 2) + VFX aura 13-23 (đặt CHÂN).")]
        [SerializeField] private EffectData introFx;
        [Tooltip("(playtest #2, điểm 5-6) VFX chuyển trang phục KHI HẾT INTRO: anim 1910 (13-25/26) + " +
                 "1911 (13-27/28), blend ADDITIVE. Fire ở RecoveryEnd trong lúc đang StateId=Intro " +
                 "(KHÔNG có voice riêng — dùng chung voice introFx).")]
        [SerializeField] private EffectData introTransitionFx;
        [Tooltip("Win (statedef 181): voice S181,0..3 (random 4) + VFX aura 786-0..5 (nhỏ, trắng, mũi kiếm).")]
        [SerializeField] private EffectData winFx;
        [Tooltip("Lose (statedef 170): voice S170,0 (KHÔNG VFX, KHÔNG fade).")]
        [SerializeField] private EffectData loseFx;
        [Tooltip("Draw (StateId 45): voice S171,0 — phát ĐÚNG LÚC thân nhân vật biến mất (cuối fade), " +
                 "KHÔNG VFX.")]
        [SerializeField] private EffectData drawFx;

        [Header("Draw (CẦN TEST)")]
        [Tooltip("Thời gian MỜ DẦN thân nhân vật khi Draw (giây) trước khi biến mất + phát voice S171,0.")]
        [Min(0.05f)] [SerializeField] private float drawFadeTime = 2f;

        // Facing để lật offset theo hướng nhìn (qua interface Core — không ref module khác §1.3).
        private ICombatContext _ctx;

        private int _prevState = -1;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (relay == null) relay = GetComponentInChildren<AnimationEventRelay>();
            if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>();
            _ctx = GetComponent<ICombatContext>();
        }

        private void OnEnable()
        {
            // Reset baseline theo trạng thái hiện tại — không fire nhầm ngay khi bật (respawn/scene load).
            _prevState = animator != null ? animator.GetInteger(CoreAnimatorParams.StateId) : -1;
            if (relay != null) relay.RecoveryEndEvent += OnRecoveryEnd;
        }

        private void OnDisable()
        {
            if (relay != null) relay.RecoveryEndEvent -= OnRecoveryEnd;
            StopAllCoroutines();
        }

        private void Update()
        {
            if (animator == null) return;

            int state = animator.GetInteger(CoreAnimatorParams.StateId);
            if (state != _prevState)
            {
                switch (state)
                {
                    case IdIntro: Fire(introFx); break;
                    case IdWin: Fire(winFx); break;
                    case IdLose: Fire(loseFx); break;
                    case IdDraw: StartCoroutine(DrawFadeRoutine()); break;
                }
            }
            _prevState = state;
        }

        /// <summary>(playtest #2, điểm 5-6) RecoveryEnd dùng CHUNG cho mọi clip (Core §1.1) — CHỈ fire
        /// VFX chuyển trang phục khi event này đến từ clip Intro (StateId vẫn=42 tại thời điểm bắn, TRƯỚC
        /// khi RoundIntroController chuyển về Idle ở frame sau). Tránh fire nhầm lúc RecoveryEnd của
        /// đòn khác (đòn dùng chung 1 relay duy nhất).</summary>
        private void OnRecoveryEnd()
        {
            if (animator != null && animator.GetInteger(CoreAnimatorParams.StateId) == IdIntro)
                Fire(introTransitionFx);
        }

        /// <summary>(playtest #2, điểm 8) Draw: MỜ DẦN alpha thân nhân vật 1→0 trong drawFadeTime rồi
        /// phát voice S171,0 ĐÚNG LÚC biến mất (cuối fade) — KHÁC Lose (giữ nguyên, không fade).</summary>
        private IEnumerator DrawFadeRoutine()
        {
            if (bodyRenderer == null) { Fire(drawFx); yield break; }

            Color c = bodyRenderer.color;
            float dur = Mathf.Max(0.05f, drawFadeTime);
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                bodyRenderer.color = new Color(c.r, c.g, c.b, Mathf.Lerp(1f, 0f, t / dur));
                yield return null;
            }
            bodyRenderer.color = new Color(c.r, c.g, c.b, 0f);
            Fire(drawFx);   // voice S171,0 đúng lúc biến mất (KHÔNG vfxPrefab → chỉ phát âm).
        }

        /// <summary>Spawn VFX world-space + phát SFX 1 slot (mẫu Fire() của MovementEffectController —
        /// world-space vì pose đứng yên tại chỗ, không cần bám theo qua attachToOwner).</summary>
        private void Fire(EffectData fx)
        {
            if (fx == null) return;

            int facing = _ctx != null ? _ctx.Facing : (transform.lossyScale.x >= 0f ? 1 : -1);
            float ownerScale = Mathf.Abs(transform.lossyScale.y);

            if (fx.vfxPrefab != null)
            {
                Vector3 pos = transform.position +
                              new Vector3(fx.offset.x * facing * ownerScale, fx.offset.y * ownerScale, 0f);
                GameObject vfx = PrefabPool.Spawn(fx.vfxPrefab, pos, fx.vfxPrefab.transform.rotation);
                Vector3 s = vfx.transform.localScale * fx.scale * ownerScale;
                s.x = Mathf.Abs(s.x) * facing;
                vfx.transform.localScale = s;
            }

            if (audioSource != null)
            {
                AudioClip clip = fx.PickClip();
                if (clip != null) audioSource.PlayOneShot(clip, fx.volume);
            }
        }
    }
}
