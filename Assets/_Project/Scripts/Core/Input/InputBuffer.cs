namespace FA.Core
{
    /// <summary>
    /// Ring buffer lưu ~10 frame input gần nhất (Phase 4, §3.2) để combo/motion KHÔNG "miss"
    /// khi người chơi nhập sớm vài frame ("buffer window" của fighting game).
    ///
    /// Class THUẦN (KHÔNG MonoBehaviour, KHÔNG ScriptableObject): Motor sở hữu 1 cặp
    /// InputBuffer+CommandParser/Motor → 2 người chơi độc lập. Lưu mảng <see cref="InputSnapshot"/>
    /// cố định (struct) → Push/Peek KHÔNG cấp phát, KHÔNG box, KHÔNG LINQ (hot path FixedUpdate).
    ///
    /// Push gọi ĐÚNG 1 lần / FixedUpdate (research §4 "Buffer push sai nhịp"). Frame trong snapshot
    /// là số fixed-step để parser đo window theo frame (deterministic, KHÔNG Time.time).
    /// </summary>
    public sealed class InputBuffer
    {
        private readonly InputSnapshot[] _ring;
        private readonly int _capacity;
        private int _head;     // chỉ số slot sẽ GHI tiếp theo (ring)
        private int _count;    // số snapshot hợp lệ đã lưu (≤ capacity)

        /// <summary>Số snapshot hợp lệ hiện có (≤ bufferFrames).</summary>
        public int Count => _count;

        /// <summary>Số fixed-step của snapshot mới nhất (0 nếu rỗng).</summary>
        public int CurrentFrame => _count > 0 ? Peek(0).Frame : 0;

        /// <summary>Sức chứa (bufferFrames). Parser dùng để giới hạn window quét.</summary>
        public int Capacity => _capacity;

        /// <param name="bufferFrames">Sức chứa ring (mặc định 10, §3.2). Clamp tối thiểu 1.</param>
        public InputBuffer(int bufferFrames)
        {
            _capacity = bufferFrames < 1 ? 1 : bufferFrames;
            _ring = new InputSnapshot[_capacity];
            _head = 0;
            _count = 0;
        }

        /// <summary>
        /// Ghi snapshot mới nhất, ghi đè slot CŨ NHẤT khi đầy (ring). Gọi mỗi FixedUpdate.
        /// in-param → KHÔNG copy thừa struct lớn.
        /// </summary>
        public void Push(in InputSnapshot snap)
        {
            _ring[_head] = snap;
            _head = (_head + 1) % _capacity;
            if (_count < _capacity) _count++;
        }

        /// <summary>
        /// Snapshot cách đây N frame: agoFrames=0 = mới nhất, 1 = trước đó, ... Nếu agoFrames vượt
        /// số snapshot hợp lệ → trả snapshot cũ nhất hiện có (clamp), tránh out-of-range. Rỗng → default.
        /// </summary>
        public InputSnapshot Peek(int agoFrames)
        {
            if (_count == 0) return default;
            if (agoFrames < 0) agoFrames = 0;
            if (agoFrames >= _count) agoFrames = _count - 1;
            // slot mới nhất = (head - 1 + capacity) % capacity; lùi thêm agoFrames.
            int idx = (_head - 1 - agoFrames) % _capacity;
            if (idx < 0) idx += _capacity;
            return _ring[idx];
        }

        /// <summary>Xóa toàn bộ (dùng khi respawn/đổi state cần "quên" buffer — P12).</summary>
        public void Clear()
        {
            _head = 0;
            _count = 0;
        }
    }
}
