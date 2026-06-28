using System;
using UnityEngine;

namespace FA.Core
{
    /// <summary>
    /// CẦU NỐI DUY NHẤT Unity-native ↔ code cho toàn bộ frame data (§1.1 "quy tắc vàng", §3.5).
    /// MonoBehaviour đặt CÙNG GameObject với Animator (Unity gọi Animation Event theo tên method
    /// trên component cùng object). Mọi clip CHỈ gọi các tên method chuẩn hóa dưới đây → 1 nguồn sự
    /// thật cho frame data; designer chỉnh timing trên CLIP, code chỉ NHẬN (KHÔNG đếm frame).
    ///
    /// Mỗi sự kiện raise theo 2 đường (research §3.5 — tránh "2 player chung 1 kênh hitbox"):
    ///  (a) C# event CỤC BỘ (per-instance) → component cùng prefab (P5 HitboxAuthoring) subscribe trực
    ///      tiếp → đúng "nhân vật này". Đây là đường dùng cho HITBOX/RECOVERY/CANCEL (per-character).
    ///  (b) VoidEventChannelSO GLOBAL (optional, gán Inspector) → VFX/Audio/UI subscribe — dùng cho
    ///      Footstep/SpawnProjectile (sự kiện toàn cục, không phụ thuộc "ai").
    ///
    /// Danh sách tên method CỐ ĐỊNH (research §5): HitboxOn, HitboxOff, RecoveryEnd, CancelWindowOpen,
    /// CancelWindowClose, Footstep, SpawnProjectile. Thêm tên mới = thêm 1 method ở đây + (tùy) 1 kênh.
    /// Animation Event gọi tên KHÔNG có ở đây → Unity log warning → giữ danh sách này là duy nhất.
    /// </summary>
    public sealed class AnimationEventRelay : MonoBehaviour
    {
        [Header("Kênh GLOBAL (optional — VFX/Audio/UI). Để trống nếu chỉ cần C# event cục bộ.")]
        [Tooltip("Raise khi HitboxOn. Per-instance hitbox nên dùng C# event Action thay vì kênh này.")]
        [SerializeField] private VoidEventChannelSO hitboxOnChannel;
        [SerializeField] private VoidEventChannelSO hitboxOffChannel;
        [SerializeField] private VoidEventChannelSO recoveryEndChannel;
        [SerializeField] private VoidEventChannelSO cancelWindowOpenChannel;
        [SerializeField] private VoidEventChannelSO cancelWindowCloseChannel;
        [SerializeField] private VoidEventChannelSO footstepChannel;
        [SerializeField] private VoidEventChannelSO spawnProjectileChannel;
        [Tooltip("(P10) Raise khi HurtboxOff/On (i-frame né). Per-instance nên dùng C# event thay kênh này.")]
        [SerializeField] private VoidEventChannelSO hurtboxOffChannel;
        [SerializeField] private VoidEventChannelSO hurtboxOnChannel;

        // ===== C# event CỤC BỘ (per-instance) — P5+ subscribe trực tiếp trên prefab này =====
        /// <summary>Bật hitbox (đầu cửa sổ active). Tham số int = chỉ số hitbox (0 nếu không truyền).</summary>
        public event Action<int> HitboxOnEvent;
        /// <summary>Tắt hitbox (cuối cửa sổ active). int = chỉ số hitbox.</summary>
        public event Action<int> HitboxOffEvent;
        /// <summary>Hết recovery → mở lại input (P5 AttackState về Idle).</summary>
        public event Action RecoveryEndEvent;
        /// <summary>Mở cửa sổ cancel/combo (P7).</summary>
        public event Action CancelWindowOpenEvent;
        /// <summary>Đóng cửa sổ cancel/combo (P7).</summary>
        public event Action CancelWindowCloseEvent;
        /// <summary>Bước chân (P4 verify pipeline; P11 audio/dust).</summary>
        public event Action FootstepEvent;
        /// <summary>Spawn projectile (P9). int = id biến thể nếu cần.</summary>
        public event Action<int> SpawnProjectileEvent;
        /// <summary>(P10) TẮT hurtbox = bắt đầu cửa sổ i-frame né (HurtboxController disable collider).</summary>
        public event Action HurtboxOffEvent;
        /// <summary>(P10) BẬT lại hurtbox = kết thúc cửa sổ i-frame né.</summary>
        public event Action HurtboxOnEvent;

        // ===================== Method được Animation Event gọi =====================
        // Mỗi method CHỈ raise (decoupling §3.3). KHÔNG đếm frame, KHÔNG logic gameplay.

        /// <summary>Animation Event: bật hitbox (overload không tham số → index 0).</summary>
        public void HitboxOn() => HitboxOn(0);

        /// <summary>Animation Event: bật hitbox theo chỉ số (clip truyền Int).</summary>
        public void HitboxOn(int index)
        {
            HitboxOnEvent?.Invoke(index);
            if (hitboxOnChannel != null) hitboxOnChannel.RaiseEvent();
        }

        /// <summary>Animation Event: tắt hitbox (overload không tham số → index 0).</summary>
        public void HitboxOff() => HitboxOff(0);

        /// <summary>Animation Event: tắt hitbox theo chỉ số.</summary>
        public void HitboxOff(int index)
        {
            HitboxOffEvent?.Invoke(index);
            if (hitboxOffChannel != null) hitboxOffChannel.RaiseEvent();
        }

        /// <summary>Animation Event: hết recovery.</summary>
        public void RecoveryEnd()
        {
            RecoveryEndEvent?.Invoke();
            if (recoveryEndChannel != null) recoveryEndChannel.RaiseEvent();
        }

        /// <summary>Animation Event: mở cửa sổ cancel.</summary>
        public void CancelWindowOpen()
        {
            CancelWindowOpenEvent?.Invoke();
            if (cancelWindowOpenChannel != null) cancelWindowOpenChannel.RaiseEvent();
        }

        /// <summary>Animation Event: đóng cửa sổ cancel.</summary>
        public void CancelWindowClose()
        {
            CancelWindowCloseEvent?.Invoke();
            if (cancelWindowCloseChannel != null) cancelWindowCloseChannel.RaiseEvent();
        }

        /// <summary>Animation Event: bước chân (P4 verify; đặt thử trên clip Walk).</summary>
        public void Footstep()
        {
            FootstepEvent?.Invoke();
            if (footstepChannel != null) footstepChannel.RaiseEvent();
        }

        /// <summary>Animation Event: spawn projectile (overload không tham số → id 0).</summary>
        public void SpawnProjectile() => SpawnProjectile(0);

        /// <summary>Animation Event: spawn projectile theo id biến thể.</summary>
        public void SpawnProjectile(int variantId)
        {
            SpawnProjectileEvent?.Invoke(variantId);
            if (spawnProjectileChannel != null) spawnProjectileChannel.RaiseEvent();
        }

        /// <summary>(P10) Animation Event: TẮT hurtbox (đầu cửa sổ né — i-frame Unity-native, §3.1).</summary>
        public void HurtboxOff()
        {
            HurtboxOffEvent?.Invoke();
            if (hurtboxOffChannel != null) hurtboxOffChannel.RaiseEvent();
        }

        /// <summary>(P10) Animation Event: BẬT lại hurtbox (cuối cửa sổ né).</summary>
        public void HurtboxOn()
        {
            HurtboxOnEvent?.Invoke();
            if (hurtboxOnChannel != null) hurtboxOnChannel.RaiseEvent();
        }
    }
}
