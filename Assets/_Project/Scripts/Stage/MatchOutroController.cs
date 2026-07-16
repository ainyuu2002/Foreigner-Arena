using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using FA.Core;

namespace FA.Stage
{
    /// <summary>
    /// (MỤC D) ĐẠO DIỄN POSE KẾT TRẬN. Nghe <c>onMatchResult</c> (MatchResultEventChannelSO — CHỈ ĐỌC
    /// <see cref="MatchResult.Winner"/>, KHÔNG đụng ScoreSystem/điều kiện hoà): người THẮNG diễn Win
    /// (Animator "StateId"=Win), người THUA diễn Lose; HOÀ (<see cref="MatchWinner.Draw"/>) → CẢ 2 diễn
    /// Draw (StateId=45, playtest #2 điểm 7-8 — dùng chung clip Lose.anim nhưng PoseEffectController mờ
    /// dần rồi biến mất, KHÁC Lose giữ nguyên). Khóa input qua <see cref="CinematicGate"/> để không ai
    /// cựa quậy trong pose. MatchManager giữ pose vài giây (outroHold) rồi mới hiện End screen.
    ///
    /// (NHÓM 3 — fix playtest) Người THUA hết mạng KHÔNG respawn ở mạng cuối → vẫn đang RƠI khi trận kết
    /// thúc, kéo dãn CM_MatchCamera (follow cả 2). Xử lý:
    ///   • ĐÓNG BĂNG vật lý CẢ 2 NGAY khi có kết quả (linearVelocity=0, gravityScale=0) — giữ nguyên vị
    ///     trí hiện tại (kể cả giữa không trung), không rơi tiếp.
    ///   • Camera ZOOM vào NGƯỜI THẮNG — TÁI DÙNG 2 vcam intro có sẵn (CM_IntroP1/P2, đã tắt priority
    ///     sau IntroRoutine nên không xung đột thời gian). Draw → KHÔNG bật vcam nào, để CM_MatchCamera
    ///     (cả 2 đã freeze, hết bị kéo dãn) làm khung hiển thị — cách đơn giản nhất.
    ///
    /// FA.Stage chỉ ref Core (+ UnityEngine + Cinemachine) — tìm player theo LAYER, điều khiển qua Animator
    /// "StateId" (literal 43/44 vì không ref FA.Movement). CÂN BẰNG GATE (chống rò sang trận sau): outro
    /// Acquire khi set pose và KHÔNG Release trong trận (trận kết thúc → về menu, input UI riêng).
    /// Rematch/back = UNLOAD scene map → instance này bị hủy → <see cref="OnDisable"/> Release gate đang
    /// giữ (single-owner theo vòng đời scene) + tắt vcam winner (không kẹt zoom sang trận sau). Round mới
    /// = scene map MỚI → RoundIntroController Acquire lại từ depth 0 sạch.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchOutroController : MonoBehaviour
    {
        // StateId literal (FA.Stage không ref FA.Movement) — khớp CharacterStateId.Win/Lose/Draw.
        private const int WinStateId = 43;
        private const int LoseStateId = 44;
        private const int DrawStateId = 45;

        [Header("Kênh sự kiện")]
        [Tooltip("OnMatchResult (MatchResultEventChannelSO) — WinCondition raise khi trận xong.")]
        [SerializeField] private MatchResultEventChannelSO onMatchResult;

        [Tooltip("Số frame quét tìm 2 player theo layer (an toàn nếu 1 người vừa respawn/di chuyển).")]
        [SerializeField] private int scanFrames = 5;

        [Header("Camera outro (MỤC D — TÁI DÙNG 2 vcam intro, wire qua IntroWinLoseSetupTool)")]
        [Tooltip("Vcam zoom P1 (tái dùng CM_IntroP1 của RoundIntroController) — bật khi P1 thắng.")]
        [SerializeField] private CinemachineCamera introCamP1;
        [Tooltip("Vcam zoom P2 (tái dùng CM_IntroP2) — bật khi P2 thắng.")]
        [SerializeField] private CinemachineCamera introCamP2;
        [Tooltip("Priority khi vcam ĐANG zoom người thắng (> CM_MatchCamera=10 để Brain blend sang). CẦN TEST.")]
        [SerializeField] private int activePriority = 20;
        [Tooltip("Priority khi vcam TẮT (< CM_MatchCamera=10).")]
        [SerializeField] private int idlePriority = 0;

        private bool _gateHeld;
        private bool _posed;

        private void OnEnable()
        {
            if (onMatchResult != null) onMatchResult.OnEventRaised += OnMatchResult;
        }

        private void OnDisable()
        {
            if (onMatchResult != null) onMatchResult.OnEventRaised -= OnMatchResult;
            StopAllCoroutines();
            ReleaseGate();   // scene unload (rematch/back) → trả gate về depth 0 sạch cho trận sau.
            // Tắt vcam winner (nếu đang bật) — không kẹt zoom sang trận sau (map mới có vcam intro riêng).
            SetCam(introCamP1, idlePriority);
            SetCam(introCamP2, idlePriority);
        }

        private void OnMatchResult(MatchResult result)
        {
            if (_posed) return;   // 1 lần/trận.
            _posed = true;
            StartCoroutine(PoseRoutine(result.Winner));
        }

        private IEnumerator PoseRoutine(MatchWinner winner)
        {
            // Khóa input NGAY trước khi quét (đối xứng RoundIntroController) — không để vài frame cựa
            // quậy trong lúc tìm player.
            AcquireGate();

            // Quét 2 player theo layer — chờ vài frame nếu chưa đủ (respawn/di chuyển giữa frame kết).
            Transform p1 = null, p2 = null;
            for (int i = 0; i < Mathf.Max(1, scanFrames) && (p1 == null || p2 == null); i++)
            {
                FindPlayers(out p1, out p2);
                if (p1 != null && p2 != null) break;
                yield return null;
            }

            // (2.1) ĐÓNG BĂNG vật lý CẢ 2 NGAY khi có kết quả — người thua hết mạng có thể chưa respawn
            // (đang rơi); không freeze thì rơi mãi kéo dãn CM_MatchCamera (follow cả 2), không thấy pose.
            FreezePhysics(p1);
            FreezePhysics(p2);

            // (playtest #2, điểm 7-8) Draw → CẢ 2 = Draw(45, mờ dần); P1 thắng → P1 Win/P2 Lose;
            // P2 thắng → ngược lại.
            if (winner == MatchWinner.Draw)
            {
                SetStateId(p1, DrawStateId);
                SetStateId(p2, DrawStateId);
            }
            else
            {
                int idP1 = winner == MatchWinner.P1 ? WinStateId : LoseStateId;
                int idP2 = winner == MatchWinner.P2 ? WinStateId : LoseStateId;
                SetStateId(p1, idP1);
                SetStateId(p2, idP2);
            }

            // (2.2) Camera outro — zoom vào NGƯỜI THẮNG. Draw → không bật vcam nào (CM_MatchCamera đã hết
            // bị kéo dãn vì cả 2 đã freeze).
            if (winner == MatchWinner.P1) SetCam(introCamP1, activePriority, p1);
            else if (winner == MatchWinner.P2) SetCam(introCamP2, activePriority, p2);
        }

        private static void FreezePhysics(Transform who)
        {
            if (who == null) return;
            Rigidbody2D rb = who.GetComponent<Rigidbody2D>();
            if (rb == null) return;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.gravityScale = 0f;
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

        private void SetCam(CinemachineCamera cam, int priority, Transform follow = null)
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
