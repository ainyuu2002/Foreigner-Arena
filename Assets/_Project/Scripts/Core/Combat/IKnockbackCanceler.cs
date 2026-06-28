namespace FA.Core
{
    /// <summary>
    /// Hợp đồng NHỎ (P10) để BurstState (FA.Specials) HỦY cú knockback đang CHỜ áp trên CHÍNH MÌNH mà
    /// KHÔNG reference class FA.Combat (§1.3). <see cref="FA.Combat.KnockbackResolver"/> (P7) implement
    /// THÊM (chỉ thêm 1 method <see cref="CancelPending"/> + "<c>: IKnockbackCanceler</c>" — KHÔNG sửa
    /// logic resolve P7). Đối xứng <see cref="IPendingKnockback"/> (P7, getter) nhưng là HÀNH ĐỘNG hủy.
    ///
    /// LÝ DO (research §4 cạm bẫy 3): Burst dùng KHI ĐANG BỊ COMBO/juggle → resolver còn pending knockback
    /// (sẽ bốc mình vào tumble). Burst thoát hiểm → phải HỦY pending đó, nếu không vừa burst xong vẫn bị
    /// bay theo cú đánh cuối. BurstState GetComponent&lt;IKnockbackCanceler&gt;() (null-safe) gọi khi Enter.
    /// </summary>
    public interface IKnockbackCanceler
    {
        /// <summary>Hủy MỌI knockback đang chờ áp trên victim này (pending + lịch applyDelay). Burst gọi.</summary>
        void CancelPending();
    }
}
