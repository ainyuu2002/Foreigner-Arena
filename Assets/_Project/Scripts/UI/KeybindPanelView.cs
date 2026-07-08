using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FA.UI
{
    /// <summary>
    /// (P12 Settings — rebind phím) PANEL CÀI ĐẶT PHÍM 2 TAB — BÀN PHÍM (P1) / TAY CẦM (P2): mỗi hàng
    /// 1 action, bấm ô phím → chờ phím/nút mới qua <c>PerformInteractiveRebinding</c> (ESC hủy; thiết bị
    /// NGOÀI tab bị loại — tab tay cầm còn loại stick/start/select để không bind nhầm cần analog hay nút
    /// pause), gán xong LƯU ngay + áp lên trận đang chạy (<see cref="KeyBindings.SaveAndApply"/>).
    /// Nút MẶC ĐỊNH trả phím gốc CẢ 2 tab.
    ///
    /// Cùng 12 hàng dùng cho cả 2 tab (mọi action đều có binding cả 2 nhóm — LÊN/XUỐNG/TRÁI/PHẢI ở tay cầm
    /// là D-Pad, cần analog giữ nguyên). Cho phép TRÙNG phím trong tab (mặc định NHẢY và LÊN cùng W/stick-up
    /// — trùng là chủ ý), chỉ nhắc qua <see cref="statusLabel"/>. Các mảng row* do builder bake cùng index.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KeybindPanelView : MonoBehaviour
    {
        [Header("Bảng hàng phím (builder bake — cùng index)")]
        [Tooltip("Tên action trong FA_Controls (Move/Jump/Dash/L/M/H/Super1/Super2/Rage).")]
        [SerializeField] private string[] rowActions;
        [Tooltip("Phần composite của Move (up/down/left/right) — rỗng nếu binding lẻ.")]
        [SerializeField] private string[] rowParts;
        [Tooltip("Tên hiển thị tiếng Việt (cho status).")]
        [SerializeField] private string[] rowLabels;
        [SerializeField] private Text[] rowKeyLabels;
        [SerializeField] private Image[] rowKeyBacks;

        [Header("Tab thiết bị (0 = Bàn phím P1, 1 = Tay cầm P2)")]
        [SerializeField] private Image[] tabBacks;

        [Header("Trạng thái")]
        [SerializeField] private Text statusLabel;

        private const string GroupKeyboard = "Keyboard";
        private const string GroupGamepad = "Gamepad";
        private const string HintKeyboard = "Bấm vào ô phím để đổi — bàn phím của P1.";
        private const string HintGamepad =
            "Bấm vào ô rồi nhấn nút tay cầm (P2). LÊN/XUỐNG/TRÁI/PHẢI là D-Pad — cần analog giữ nguyên.";

        private static readonly Color WaitingCol = new Color(0.95f, 0.65f, 0.15f, 1f);
        private static readonly Color TabOnCol = new Color(0.17f, 0.33f, 0.55f, 1f);   // = UIKit.ColBtn
        private static readonly Color TabOffCol = new Color(0.13f, 0.15f, 0.20f, 1f);  // = UIKit.ColPanelLight

        private InputActionRebindingExtensions.RebindingOperation _op;
        private int _activeRow = -1;
        private string _group = GroupKeyboard;
        private Color _backNormal = Color.gray;
        private bool _backCaptured;

        /// <summary>Nút CÀI ĐẶT PHÍM (persistent listener — gọi được khi panel inactive).</summary>
        public void Open() => gameObject.SetActive(true);

        /// <summary>Nút ĐÓNG.</summary>
        public void Close()
        {
            CancelRebind();
            gameObject.SetActive(false);
        }

        /// <summary>Tab thiết bị (persistent listener int): 0 = bàn phím P1, 1 = tay cầm P2.</summary>
        public void SelectTab(int index)
        {
            CancelRebind();
            _group = index == 1 ? GroupGamepad : GroupKeyboard;
            RefreshAll();
            SetStatus(Hint());
        }

        /// <summary>Nút MẶC ĐỊNH — xóa mọi override (cả 2 tab), về phím gốc.</summary>
        public void ResetDefaults()
        {
            CancelRebind();
            KeyBindings.ResetAll();
            RefreshAll();
            SetStatus("Đã khôi phục phím mặc định (cả bàn phím lẫn tay cầm).");
        }

        /// <summary>Ô phím hàng <paramref name="row"/> (persistent listener int do builder gắn).</summary>
        public void StartRebind(int row)
        {
            CancelRebind();
            var action = ActionOf(row);
            int idx = action != null ? KeyBindings.BindingIndex(action, _group, rowParts[row]) : -1;
            if (idx < 0)
            {
                SetStatus($"Không tìm thấy binding {_group} cho {LabelOf(row)}.");
                return;
            }

            _activeRow = row;
            KeyBindings.RebindActive = true;
            bool gamepad = _group == GroupGamepad;
            SetStatus(gamepad
                ? $"Nhấn NÚT TAY CẦM mới cho {LabelOf(row)}…  (ESC để hủy)"
                : $"Nhấn phím mới cho {LabelOf(row)}…  (ESC để hủy)");
            TintBack(row, WaitingCol);

            var op = action.PerformInteractiveRebinding(idx)
                .WithControlsExcluding("<Mouse>")
                .WithControlsExcluding("<Pointer>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.05f)
                .OnComplete(_ => FinishRebind(applied: true))
                .OnCancel(_ => FinishRebind(applied: false));

            if (gamepad)
            {
                // Tab tay cầm: loại bàn phím + cần analog (gạt nhẹ là dính) + start/select (start = pause).
                op.WithControlsExcluding("<Keyboard>")
                  .WithControlsExcluding("<Gamepad>/leftStick")
                  .WithControlsExcluding("<Gamepad>/rightStick")
                  .WithControlsExcluding("<Gamepad>/start")
                  .WithControlsExcluding("<Gamepad>/select");
            }
            else
            {
                op.WithControlsExcluding("<Gamepad>");
            }

            _op = op.Start();
        }

        private void FinishRebind(bool applied)
        {
            if (_op != null) { _op.Dispose(); _op = null; }
            int row = _activeRow;
            _activeRow = -1;
            KeyBindings.RebindActive = false;
            KeyBindings.LastRebindEnd = Time.unscaledTime;

            if (applied && row >= 0)
            {
                KeyBindings.SaveAndApply();
                string dup = DuplicateNote(row);
                SetStatus($"Đã gán {LabelOf(row)} = {KeyNameOf(row)}." +
                          (dup != null ? $"  Lưu ý: trùng với {dup}." : ""));
            }
            else
            {
                SetStatus(Hint());
            }
            RefreshAll();
        }

        private void CancelRebind()
        {
            // Cancel() gọi OnCancel → FinishRebind(false) dọn cờ + dispose.
            if (_op != null) _op.Cancel();
        }

        private void OnEnable()
        {
            _group = GroupKeyboard; // mở panel luôn về tab bàn phím.
            RefreshAll();
            SetStatus(Hint());
        }

        private void OnDisable()
        {
            CancelRebind();
        }

        // ───────────────────── hiển thị ─────────────────────

        private void RefreshAll()
        {
            if (rowActions == null) return;
            for (int i = 0; i < rowActions.Length; i++)
            {
                if (rowKeyLabels != null && i < rowKeyLabels.Length && rowKeyLabels[i] != null)
                    rowKeyLabels[i].text = KeyNameOf(i);
                TintBack(i, Color.clear, restore: true); // restore → màu param bị bỏ qua
            }
            if (tabBacks != null)
                for (int t = 0; t < tabBacks.Length; t++)
                    if (tabBacks[t] != null)
                        tabBacks[t].color = (t == 1) == (_group == GroupGamepad) ? TabOnCol : TabOffCol;
        }

        private string Hint() => _group == GroupGamepad ? HintGamepad : HintKeyboard;

        private string KeyNameOf(int row)
        {
            var action = ActionOf(row);
            int idx = action != null ? KeyBindings.BindingIndex(action, _group, rowParts[row]) : -1;
            if (idx < 0) return "—";
            string s = action.GetBindingDisplayString(idx);
            return string.IsNullOrEmpty(s) ? "?" : s.ToUpperInvariant();
        }

        /// <summary>Liệt kê các hàng khác trong CÙNG TAB đang dùng cùng phím với hàng vừa gán.</summary>
        private string DuplicateNote(int row)
        {
            var action = ActionOf(row);
            int idx = action != null ? KeyBindings.BindingIndex(action, _group, rowParts[row]) : -1;
            if (idx < 0) return null;
            string path = action.bindings[idx].effectivePath;

            System.Text.StringBuilder sb = null;
            for (int i = 0; i < rowActions.Length; i++)
            {
                if (i == row) continue;
                var a2 = ActionOf(i);
                int i2 = a2 != null ? KeyBindings.BindingIndex(a2, _group, rowParts[i]) : -1;
                if (i2 < 0) continue;
                if (!string.Equals(a2.bindings[i2].effectivePath, path,
                                   System.StringComparison.OrdinalIgnoreCase)) continue;
                if (sb == null) sb = new System.Text.StringBuilder();
                else sb.Append(", ");
                sb.Append(LabelOf(i));
            }
            return sb != null ? sb.ToString() : null;
        }

        private InputAction ActionOf(int row)
        {
            if (rowActions == null || row < 0 || row >= rowActions.Length) return null;
            return KeyBindings.DisplayAsset.FindAction(rowActions[row], throwIfNotFound: false);
        }

        private string LabelOf(int row) =>
            rowLabels != null && row < rowLabels.Length ? rowLabels[row] : rowActions[row];

        private void SetStatus(string msg)
        {
            if (statusLabel != null) statusLabel.text = msg;
        }

        private void TintBack(int row, Color c, bool restore = false)
        {
            if (rowKeyBacks == null || row < 0 || row >= rowKeyBacks.Length) return;
            var img = rowKeyBacks[row];
            if (img == null) return;
            if (!_backCaptured) { _backNormal = img.color; _backCaptured = true; }
            img.color = restore ? _backNormal : c;
        }
    }
}
