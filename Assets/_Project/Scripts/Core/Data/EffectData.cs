using UnityEngine;

namespace FA.Core
{
    /// <summary>
    /// 1 SLOT hiệu ứng (VFX + SFX) — ScriptableObject (§1.1: dữ liệu trong asset, chỉnh Inspector không
    /// sửa code). Đặt ở FA.Core (type dùng chung §1.3) để MoveData/HitData tham chiếu được — hit effect
    /// PER-MOVE như MUGEN (mỗi HitDef có hitsound + spark riêng, spark scale theo độ nặng); phần PHÁT
    /// (CharacterEffectPlayer, HitEffectPlayer, FlipbookOneShot) vẫn nằm trong module FA.Effects.
    /// Thêm effect cho đòn mới = tạo asset mới + gán vào MoveData/list — KHÔNG sửa code.
    /// </summary>
    [CreateAssetMenu(fileName = "EffectData", menuName = "FA/Effects/Effect Data")]
    public sealed class EffectData : ScriptableObject
    {
        [Header("VFX (để trống nếu slot chỉ có âm thanh)")]
        [Tooltip("Prefab hiệu ứng hình (FlipbookOneShot/ParticleSystem tự hủy). Spawn tại vị trí nhân " +
                 "vật + offset (PlayVfx) hoặc tại điểm chạm (HitEffectPlayer).")]
        public GameObject vfxPrefab;

        [Tooltip("Offset từ gốc nhân vật (facing-relative: x dương = TRƯỚC mặt). Đơn vị Unity units.")]
        public Vector2 offset;

        [Tooltip("Bật = VFX làm CHILD của nhân vật (bám theo khi di chuyển — aura/glow). " +
                 "Tắt = spawn rời tại chỗ (flash/burst).")]
        public bool attachToOwner;

        [Header("SFX (để trống nếu slot chỉ có hình)")]
        [Tooltip("Nhiều clip = chọn NGẪU NHIÊN mỗi lần phát (voice line đa dạng kiểu MUGEN random%N).")]
        public AudioClip[] sfxClips;

        [Range(0f, 1f)] public float volume = 1f;

        [Header("Chung")]
        [Tooltip("Nhân kích thước VFX (MUGEN scale spark theo độ nặng hit: L≈1, M≈1.2, H≈1.5).")]
        [Min(0.05f)] public float scale = 1f;

        /// <summary>Chọn clip ngẫu nhiên (null nếu slot không có âm thanh).</summary>
        public AudioClip PickClip()
        {
            if (sfxClips == null || sfxClips.Length == 0) return null;
            return sfxClips[Random.Range(0, sfxClips.Length)];
        }
    }
}
