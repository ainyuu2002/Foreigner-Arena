using FA.Core;
using UnityEngine;

namespace FA.Effects
{
    /// <summary>
    /// VFX/SFX THEO FRAME của nhân vật (tung đòn, kích chiêu, dash...): nghe Animation Event
    /// <c>PlayVfx(i)</c>/<c>PlaySfx(i)</c> qua <see cref="AnimationEventRelay"/> (per-instance — đúng
    /// "nhân vật này"), i = chỉ số slot trong <see cref="effects"/>. Timing nằm TRÊN CLIP (§1.1),
    /// nội dung nằm trong <see cref="EffectData"/> asset → thêm hiệu ứng cho đòn mới = tạo asset +
    /// đặt event, KHÔNG sửa code. Module FA.Effects chỉ NGHE — không module nào reference nó (§1.3).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterEffectPlayer : MonoBehaviour
    {
        [Header("Tham chiếu (trống = tự tìm trên prefab)")]
        [SerializeField] private AnimationEventRelay relay;
        [SerializeField] private AudioSource audioSource;

        [Header("Slot hiệu ứng (index = Int của Animation Event PlayVfx/PlaySfx)")]
        [Tooltip("Danh sách EffectData. Event PlayVfx(2) → spawn effects[2].vfxPrefab; PlaySfx(2) → " +
                 "phát effects[2].sfxClips (random nếu nhiều clip). Thêm slot = thêm phần tử.")]
        [SerializeField] private EffectData[] effects;

        // Facing để lật offset + hiệu ứng ra TRƯỚC mặt (qua interface Core — không ref module khác §1.3).
        private ICombatContext _ctx;

        private void Awake()
        {
            _ctx = GetComponent<ICombatContext>();
            if (relay == null) relay = GetComponentInChildren<AnimationEventRelay>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            if (relay == null) return;
            relay.PlayVfxEvent += OnPlayVfx;
            relay.PlaySfxEvent += OnPlaySfx;
        }

        private void OnDisable()
        {
            if (relay == null) return;
            relay.PlayVfxEvent -= OnPlayVfx;
            relay.PlaySfxEvent -= OnPlaySfx;
        }

        private EffectData Slot(int index) =>
            effects == null || index < 0 || index >= effects.Length ? null : effects[index];

        private void OnPlayVfx(int index)
        {
            EffectData fx = Slot(index);
            if (fx == null || fx.vfxPrefab == null) return;

            int facing = _ctx != null ? _ctx.Facing : (transform.lossyScale.x >= 0f ? 1 : -1);
            Vector3 pos = transform.position + new Vector3(fx.offset.x * facing, fx.offset.y, 0f);
            // GIỮ rotation prefab (VFX xoay nghiêng như cột sáng dọc kiếm/vệt chém bake góc trên root
            // — identity sẽ xóa mất). Prefab cũ đều identity → không đổi hành vi.
            GameObject vfx = Instantiate(fx.vfxPrefab, pos, fx.vfxPrefab.transform.rotation,
                                         fx.attachToOwner ? transform : null);

            // Lật theo hướng nhìn (slash/glow ra đúng phía) + nhân scale của EffectData.
            // attachToOwner: CHA (root nhân vật) đã lật localScale.x theo facing (CharacterMotor) → VFX
            // con THỪA HƯỞNG mirror; nhân thêm dấu facing vào local = lật 2 lần → VFX luôn quay phải
            // (bug "VFX ngược khi đánh sang trái"). Attach → giữ local dương, cha lo mirror; không
            // attach (world) → tự lật theo facing như cũ.
            Vector3 s = vfx.transform.localScale * fx.scale;
            s.x = fx.attachToOwner ? Mathf.Abs(s.x) : Mathf.Abs(s.x) * facing;
            vfx.transform.localScale = s;
        }

        private void OnPlaySfx(int index)
        {
            EffectData fx = Slot(index);
            if (fx == null || audioSource == null) return;
            AudioClip clip = fx.PickClip();
            if (clip != null) audioSource.PlayOneShot(clip, fx.volume);
        }
    }
}
