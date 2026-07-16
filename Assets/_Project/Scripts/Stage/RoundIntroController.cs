using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using FA.Core;

namespace FA.Stage
{
    /// <summary>
    /// (MỤC D) ĐẠO DIỄN INTRO VÀO TRẬN — tuần tự P1 → P2 rồi mở input. Nghe <c>onRoundIntroStart</c>
    /// (MatchManager raise sau khi spawn 2 người): khóa toàn cục input qua <see cref="CinematicGate"/>,
    /// zoom camera cận từng người (bật vcam intro priority cao → Cinemachine Brain tự blend), cho người
    /// đó diễn clip Intro (Animator "StateId"=Intro), CHỜ <see cref="AnimationEventRelay.RecoveryEndEvent"/>
    /// ở frame cuối clip báo "xong" (KHÔNG đếm frame — §1.1), rồi chuyển sang người kia. Xong cả 2 →
    /// tắt vcam intro (Brain tự blend về CM_MatchCamera — KHÔNG restore thủ công), mở input, raise
    /// <c>onRoundIntroFinished</c>.
    ///
    /// FA.Stage chỉ ref Core (+ UnityEngine + Cinemachine) — tìm player theo LAYER (như
    /// <see cref="MatchCameraTargets"/>), điều khiển qua Animator "StateId" (literal 42 vì không ref
    /// FA.Movement) + relay (Core). Cân bằng gate: Acquire khi bắt đầu, Release CHÍNH XÁC 1 lần khi xong
    /// (cả nhánh timeout) — rò gate = khóa input cả trận.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoundIntroController : MonoBehaviour
    {
        // StateId literal (FA.Stage không ref FA.Movement) — khớp CharacterStateId.Intro/Idle.
        private const int IntroStateId = 42;
        private const int IdleStateId = 0;

        [Header("Kênh sự kiện")]
        [Tooltip("OnRoundIntroStart (VoidEventChannelSO) — MatchManager raise sau SpawnPlayers.")]
        [SerializeField] private VoidEventChannelSO onRoundIntroStart;
        [Tooltip("OnRoundIntroFinished (VoidEventChannelSO) — raise khi intro P1+P2 xong; MatchManager chờ.")]
        [SerializeField] private VoidEventChannelSO onRoundIntroFinished;

        [Header("Camera intro (vcam Cinemachine, priority cao khi bật)")]
        [SerializeField] private CinemachineCamera introCamP1;
        [SerializeField] private CinemachineCamera introCamP2;
        [Tooltip("Priority khi vcam intro ĐANG diễn (> CM_MatchCamera=10 để Brain blend sang). CẦN TEST.")]
        [SerializeField] private int activePriority = 20;
        [Tooltip("Priority khi vcam intro TẮT (< CM_MatchCamera=10 để Brain blend về khung đánh).")]
        [SerializeField] private int idlePriority = 0;

        [Header("Thời lượng (CẦN TEST)")]
        [Tooltip("Timeout mỗi người (giây) — nếu clip Intro thiếu RecoveryEnd thì vẫn chuyển tiếp. " +
                 "(playtest #2, điểm 1) Intro act191 ĐẦY ĐỦ (3 vòng múa) dwell tới RecoveryEnd ~5.8s/người " +
                 "→ nới lên 9s để có biên an toàn.")]
        [SerializeField] private float perPersonTimeout = 9f;
        [Tooltip("Số frame quét tìm 2 player sau khi intro bắt đầu (player spawn cùng thời điểm).")]
        [SerializeField] private int scanFrames = 30;

        private bool _gateHeld;

        private void OnEnable()
        {
            if (onRoundIntroStart != null) onRoundIntroStart.OnEventRaised += OnRoundIntroStart;
        }

        private void OnDisable()
        {
            if (onRoundIntroStart != null) onRoundIntroStart.OnEventRaised -= OnRoundIntroStart;
            StopAllCoroutines();
            ReleaseGate();   // scene unload giữa intro → không rò gate (khóa input trận sau).
        }

        private void OnRoundIntroStart()
        {
            StopAllCoroutines();
            StartCoroutine(IntroRoutine());
        }

        private IEnumerator IntroRoutine()
        {
            // 1. Khóa input NGAY (trước khi quét) — 2 người vừa spawn đứng im, không lỡ nhận input.
            AcquireGate();

            // 2. Quét 2 player theo layer (spawn runtime — chờ vài frame tới khi đủ 2).
            Transform p1 = null, p2 = null;
            for (int i = 0; i < Mathf.Max(1, scanFrames) && (p1 == null || p2 == null); i++)
            {
                FindPlayers(out p1, out p2);
                if (p1 != null && p2 != null) break;
                yield return null;
            }

            // 3. Tắt cả 2 vcam intro (bắt đầu từ khung đánh).
            SetCam(introCamP1, null, idlePriority);
            SetCam(introCamP2, null, idlePriority);

            // 4. Diễn intro tuần tự P1 → P2.
            if (p1 != null) yield return PlayIntro(p1, introCamP1);
            if (p2 != null) yield return PlayIntro(p2, introCamP2);

            // 5. Về Idle + tắt vcam intro (Brain tự blend về CM_MatchCamera) + mở input + báo xong.
            SetStateId(p1, IdleStateId);
            SetStateId(p2, IdleStateId);
            SetCam(introCamP1, null, idlePriority);
            SetCam(introCamP2, null, idlePriority);
            ReleaseGate();
            if (onRoundIntroFinished != null) onRoundIntroFinished.RaiseEvent();
        }

        /// <summary>Zoom cận 1 người + diễn Intro, chờ RecoveryEnd cuối clip (hoặc timeout).</summary>
        private IEnumerator PlayIntro(Transform who, CinemachineCamera cam)
        {
            AnimationEventRelay relay = who.GetComponentInChildren<AnimationEventRelay>();
            bool done = false;
            Action onEnd = () => done = true;
            if (relay != null) relay.RecoveryEndEvent += onEnd;

            SetStateId(who, IntroStateId);
            SetCam(cam, who, activePriority);

            float t = 0f;
            while (!done && t < perPersonTimeout)
            {
                // (FIX P1 không diễn intro) Player VỪA spawn: MatchManager raise OnRoundIntroStart NGAY
                // trong frame Instantiate (không yield), FindPlayers tìm được ngay (Rigidbody2D có sẵn) →
                // ta set StateId=Intro TRƯỚC khi CharacterMotor.Start() chạy. Frame kế Start() gọi
                // ChangeState(Idle) → IdleState.Enter GHI ĐÈ StateId về 0 → AnyState→Intro không kích →
                // P1 đứng im. P2 diễn intro ~6s SAU (Start đã xong) nên không dính. RE-ASSERT mỗi frame để
                // bền với mọi lần đè muộn (Start/init): Animator eval SAU coroutine nên KHÔNG nháy Idle,
                // canTransitionToSelf=false nên set lại 42 khi đang Intro là no-op (không restart clip).
                SetStateId(who, IntroStateId);
                t += Time.deltaTime;
                yield return null;
            }

            if (relay != null) relay.RecoveryEndEvent -= onEnd;
            SetStateId(who, IdleStateId);       // trả về Idle trước khi zoom sang người kế.
            SetCam(cam, null, idlePriority);
        }

        private void FindPlayers(out Transform p1, out Transform p2)
        {
            p1 = p2 = null;
            int layerP1 = LayerMask.NameToLayer("Player1");
            int layerP2 = LayerMask.NameToLayer("Player2");
            foreach (var rb in FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None))
            {
                if (rb.gameObject.layer == layerP1) p1 = rb.transform;
                else if (rb.gameObject.layer == layerP2) p2 = rb.transform;
            }
        }

        private static void SetStateId(Transform who, int stateId)
        {
            if (who == null) return;
            Animator anim = who.GetComponentInChildren<Animator>();
            if (anim != null) anim.SetInteger(CoreAnimatorParams.StateId, stateId);
        }

        private void SetCam(CinemachineCamera cam, Transform follow, int priority)
        {
            if (cam == null) return;
            if (follow != null) cam.Follow = follow;
            cam.Priority = priority;
        }

        private void AcquireGate()
        {
            if (_gateHeld) return;
            CinematicGate.Acquire();
            _gateHeld = true;
        }

        private void ReleaseGate()
        {
            if (!_gateHeld) return;
            CinematicGate.Release();
            _gateHeld = false;
        }
    }
}
