using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using FA.UI;

namespace FA.EditorTools
{
    /// <summary>
    /// PHẦN SETTINGS + PAUSE (P12 tuần 3) của bộ tool dựng UI production (xem <see cref="UIProductionMenu"/>):
    ///   • <see cref="BuildSettingsPanel"/> — panel SETTINGS 1 NGUỒN (âm lượng Master/BGM/SFX) dựng cùng
    ///     1 hàm cho cả Main Menu (đầy đủ) lẫn Pause Menu (in-match rút gọn) → sửa layout 1 chỗ ăn cả 2.
    ///   • <see cref="BuildPauseMenu"/> — canvas PauseMenu (sorting 400, trên mọi tầng): ESC/Start mở bảng
    ///     TIẾP TỤC / SETTINGS / VỀ MENU. VỀ MENU raise kênh BackToCharSelect có sẵn — KHÔNG sửa gameplay.
    ///   • <see cref="AddMatchRules"/> — hàng LUẬT TRẬN (thời gian/mạng → MatchSettingsSO) gắn vào
    ///     StageSelectPanel: luật là CỦA TRẬN nên chỉnh ngay trước khi vào trận, không nằm Settings toàn cục.
    /// EDITOR-ONLY; pattern chung: SerializedObject → Apply → SetDirty (kẻo ref null khi SaveScene).
    /// </summary>
    internal static class UISettingsBuilder
    {
        public const string PauseMenuName = "PauseMenu";

        private static readonly Color DimCol = new Color(0f, 0f, 0f, 0.62f);
        private static readonly Color HandleCol = new Color(0.85f, 0.88f, 0.95f, 1f);

        // ───────────────────── Settings panel (1 nguồn cho Main Menu + Pause) ─────────────────────

        /// <summary>
        /// Dựng panel SETTINGS (inactive sẵn — mở bằng <see cref="SettingsPanelView.Open"/>). Trả GO root;
        /// view lấy qua GetComponent. Dim nền chặn raycast → không click xuyên xuống menu/nút dưới.
        /// </summary>
        public static GameObject BuildSettingsPanel(Transform parent, bool inMatch)
        {
            var root = UIKit.NewUI("SettingsPanel", parent);
            UIKit.StretchFull(root);

            var dim = UIKit.NewUI("Dim", root.transform);
            UIKit.StretchFull(dim);
            UIKit.AddImage(dim, DimCol, raycastTarget: true);

            var panel = UIKit.NewPanel("Panel", root.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
                                       new Vector2(720f, 560f), UIKit.ColPanelLight);
            UIKit.AddVertical(panel, 18f, 30, TextAnchor.UpperCenter);

            var title = UIKit.AddLabel(panel.transform, "SETTINGS", 40, FontStyle.Bold,
                                       TextAnchor.MiddleCenter);
            UIKit.SetLayoutHeight(title.gameObject, 60f);

            var master = MakeVolumeSlider(panel.transform, "ÂM LƯỢNG CHUNG", out Text masterVal);
            var bgm = MakeVolumeSlider(panel.transform, "NHẠC NỀN", out Text bgmVal);
            var sfx = MakeVolumeSlider(panel.transform, "HIỆU ỨNG", out Text sfxVal);

            // Ghi chú: BGM/SFX mới LƯU giá trị (contract PlayerPrefs cho người làm audio) — nói thẳng trên UI
            // để người test không tưởng slider hỏng khi game chưa có tiếng.
            var note = UIKit.AddLabel(panel.transform,
                "Game chưa có âm thanh — giá trị được lưu sẵn, có audio là chạy.",
                16, FontStyle.Italic, TextAnchor.MiddleCenter, UIKit.ColTxtDim);
            UIKit.SetLayoutHeight(note.gameObject, 34f);

            var keysBtn = UIKit.MakeButton(panel.transform, "CÀI ĐẶT PHÍM", UIKit.ColPanel, 280f, 56f, 24);
            var closeBtn = UIKit.MakeButton(panel.transform, "ĐÓNG", UIKit.ColBtn, 240f, 60f, 26);

            // Panel rebind phím: sibling SAU Panel (vẽ đè lên) trong cùng root — root tắt là ẩn cả cụm.
            var keybind = BuildKeybindPanel(root.transform);
            var keybindView = keybind.GetComponent<KeybindPanelView>();

            var view = root.AddComponent<SettingsPanelView>();
            var so = new SerializedObject(view);
            UIKit.SetObj(so, "masterSlider", master);
            UIKit.SetObj(so, "bgmSlider", bgm);
            UIKit.SetObj(so, "sfxSlider", sfx);
            UIKit.SetObj(so, "masterValueLabel", masterVal);
            UIKit.SetObj(so, "bgmValueLabel", bgmVal);
            UIKit.SetObj(so, "sfxValueLabel", sfxVal);
            UIKit.SetBool(so, "inMatchMode", inMatch);
            UIKit.SetObj(so, "keybindPanel", keybindView);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);

            UnityEventTools.AddPersistentListener(keysBtn.onClick, new UnityAction(keybindView.Open));
            UnityEventTools.AddPersistentListener(closeBtn.onClick, new UnityAction(view.Close));
            EditorUtility.SetDirty(keysBtn);
            EditorUtility.SetDirty(closeBtn);

            root.SetActive(false); // mở qua SettingsPanelView.Open (persistent listener gọi được khi inactive).
            return root;
        }

        /// <summary>
        /// Slider uGUI dựng tay (không dùng DefaultControls — giữ style UIKit): hàng = label + track/fill +
        /// handle + label %. fillRect/handleRect gán trực tiếp — Slider tự điều khiển anchor con theo value.
        /// </summary>
        private static Slider MakeVolumeSlider(Transform parent, string label, out Text valueLabel)
        {
            var row = UIKit.NewUI("Row_" + label, parent);
            UIKit.SetLayoutHeight(row, 48f).flexibleWidth = 1f;
            UIKit.AddHorizontal(row, 14f, controlW: true, expandW: false, TextAnchor.MiddleLeft);

            UIKit.AddLabel(row.transform, label, 22, FontStyle.Bold, TextAnchor.MiddleLeft,
                           UIKit.ColTxtDim, width: 210f);

            var sliderGo = UIKit.NewUI("Slider", row.transform);
            var le = sliderGo.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f; le.preferredHeight = 30f; le.minHeight = 30f;

            var track = UIKit.NewUI("Track", sliderGo.transform);
            UIKit.StretchFull(track);
            var trt = track.GetComponent<RectTransform>();
            trt.offsetMin = new Vector2(0f, 10f); trt.offsetMax = new Vector2(0f, -10f);
            // raycastTarget TRUE: click lên track → event nổi bọt lên Slider (IPointerDown) → nhảy giá trị.
            UIKit.AddImage(track, UIKit.ColBarBack, raycastTarget: true);

            var fillArea = UIKit.NewUI("FillArea", sliderGo.transform);
            UIKit.StretchFull(fillArea);
            var fart = fillArea.GetComponent<RectTransform>();
            fart.offsetMin = new Vector2(6f, 10f); fart.offsetMax = new Vector2(-6f, -10f);

            var fill = UIKit.NewUI("Fill", fillArea.transform);
            var frt = fill.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0f, 1f);
            frt.sizeDelta = new Vector2(10f, 0f);
            UIKit.AddImage(fill, UIKit.ColMana);

            var handleArea = UIKit.NewUI("HandleArea", sliderGo.transform);
            UIKit.StretchFull(handleArea);
            var hart = handleArea.GetComponent<RectTransform>();
            hart.offsetMin = new Vector2(12f, 0f); hart.offsetMax = new Vector2(-12f, 0f);

            var handle = UIKit.NewUI("Handle", handleArea.transform);
            var hrt = handle.GetComponent<RectTransform>();
            hrt.sizeDelta = new Vector2(24f, 0f);
            var handleImg = UIKit.AddImage(handle, HandleCol, raycastTarget: true);

            var slider = sliderGo.AddComponent<Slider>();
            slider.fillRect = frt;
            slider.handleRect = hrt;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;
            EditorUtility.SetDirty(slider);

            valueLabel = UIKit.AddLabel(row.transform, "100%", 22, FontStyle.Bold,
                                        TextAnchor.MiddleRight, UIKit.ColTxt, width: 80f);
            return slider;
        }

        // ───────────────────── Panel cài đặt phím (rebind — con của SettingsPanel) ─────────────────────

        // 3 mảng song song cùng index — bake thẳng vào KeybindPanelView (đổi phím = sửa FA_Controls
        // rồi rebuild; KHÔNG hardcode GUID binding — view tra index theo group "Keyboard" + tên part).
        private static readonly string[] KeyRowActions =
            { "Move", "Move", "Move", "Move", "Jump", "Dash", "L", "M", "H", "Super1", "Super2", "Rage" };
        private static readonly string[] KeyRowParts =
            { "up", "down", "left", "right", "", "", "", "", "", "", "", "" };
        private static readonly string[] KeyRowLabels =
            { "LÊN", "XUỐNG", "TRÁI", "PHẢI", "NHẢY", "LƯỚT",
              "ĐÁNH NHẸ", "ĐÁNH VỪA", "ĐÁNH MẠNH", "TUYỆT CHIÊU 1", "TUYỆT CHIÊU 2", "NỘ" };
        // Phím mặc định (khớp FA_Controls.inputactions) — chỉ để label có chữ ngay trong editor;
        // runtime OnEnable tự Refresh theo overrides đã lưu.
        private static readonly string[] KeyRowDefaults =
            { "W", "S", "A", "D", "W", "H", "J", "K", "L", "U", "I", "O" };

        /// <summary>
        /// Panel CÀI ĐẶT PHÍM (inactive sẵn): 2 tab BÀN PHÍM (P1) / TAY CẦM (P2), 2 cột × 6 hàng
        /// [tên hành động | ô phím], status + MẶC ĐỊNH/ĐÓNG. Cùng 12 hàng cho cả 2 tab (tay cầm:
        /// LÊN/XUỐNG/TRÁI/PHẢI = D-Pad, cần analog giữ nguyên). Dim riêng để che settings phía dưới.
        /// </summary>
        private static GameObject BuildKeybindPanel(Transform parent)
        {
            var root = UIKit.NewUI("KeybindPanel", parent);
            UIKit.StretchFull(root);

            var dim = UIKit.NewUI("Dim", root.transform);
            UIKit.StretchFull(dim);
            UIKit.AddImage(dim, DimCol, raycastTarget: true);

            var panel = UIKit.NewPanel("Panel", root.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
                                       new Vector2(960f, 700f), UIKit.ColPanelLight);
            UIKit.AddVertical(panel, 14f, 26, TextAnchor.UpperCenter);

            var title = UIKit.AddLabel(panel.transform, "CÀI ĐẶT PHÍM", 34, FontStyle.Bold,
                                       TextAnchor.MiddleCenter);
            UIKit.SetLayoutHeight(title.gameObject, 54f);

            // Tab thiết bị: index 0 = bàn phím (P1), 1 = tay cầm (P2) — view tô màu tab đang chọn.
            var tabs = UIKit.NewUI("Tabs", panel.transform);
            UIKit.SetLayoutHeight(tabs, 50f).flexibleWidth = 1f;
            UIKit.AddHorizontal(tabs, 18f, controlW: true, expandW: false, TextAnchor.MiddleCenter);
            var tabKb = UIKit.MakeButton(tabs.transform, "BÀN PHÍM (P1)", UIKit.ColBtn, 280f, 46f, 22);
            var tabGp = UIKit.MakeButton(tabs.transform, "TAY CẦM (P2)", UIKit.ColPanelLight, 280f, 46f, 22);

            var note = UIKit.AddLabel(panel.transform,
                "Cho phép trùng phím trong 1 tab (NHẢY = LÊN là chủ ý) — có nhắc khi trùng.",
                16, FontStyle.Italic, TextAnchor.MiddleCenter, UIKit.ColTxtDim);
            UIKit.SetLayoutHeight(note.gameObject, 30f);

            var columns = UIKit.NewUI("Columns", panel.transform);
            UIKit.SetLayoutHeight(columns, 330f).flexibleWidth = 1f;
            UIKit.AddHorizontal(columns, 44f, controlW: true, expandW: true, TextAnchor.UpperCenter);

            int n = KeyRowActions.Length;
            var keyLabels = new Text[n];
            var keyBacks = new Image[n];
            var keyBtns = new Button[n];
            for (int col = 0; col < 2; col++)
            {
                var column = UIKit.NewUI("Col" + col, columns.transform);
                UIKit.AddVertical(column, 11f, 0, TextAnchor.UpperCenter);
                int half = (n + 1) / 2;
                for (int i = col * half; i < Mathf.Min(n, (col + 1) * half); i++)
                {
                    var row = UIKit.NewUI("Row_" + KeyRowActions[i] + KeyRowParts[i], column.transform);
                    UIKit.SetLayoutHeight(row, 44f).flexibleWidth = 1f;
                    UIKit.AddHorizontal(row, 12f, controlW: true, expandW: false, TextAnchor.MiddleLeft);

                    UIKit.AddLabel(row.transform, KeyRowLabels[i], 21, FontStyle.Bold,
                                   TextAnchor.MiddleLeft, UIKit.ColTxtDim, width: 230f);
                    var btn = UIKit.MakeButton(row.transform, KeyRowDefaults[i], UIKit.ColBtn, 150f, 42f, 22);
                    keyBtns[i] = btn;
                    keyBacks[i] = (Image)btn.targetGraphic;
                    keyLabels[i] = btn.GetComponentInChildren<Text>();
                }
            }

            var status = UIKit.AddLabel(panel.transform,
                "Bấm vào ô phím để đổi. Chỉ đổi phím P1 (bàn phím).",
                18, FontStyle.Normal, TextAnchor.MiddleCenter, UIKit.ColTxtDim);
            UIKit.SetLayoutHeight(status.gameObject, 34f);

            var footer = UIKit.NewUI("Footer", panel.transform);
            UIKit.SetLayoutHeight(footer, 60f).flexibleWidth = 1f;
            UIKit.AddHorizontal(footer, 24f, controlW: true, expandW: false, TextAnchor.MiddleCenter);
            var resetBtn = UIKit.MakeButton(footer.transform, "MẶC ĐỊNH", UIKit.ColBtnAccent, 220f, 56f, 24);
            var closeBtn = UIKit.MakeButton(footer.transform, "ĐÓNG", UIKit.ColBtn, 220f, 56f, 24);

            var view = root.AddComponent<KeybindPanelView>();
            var so = new SerializedObject(view);
            UIKit.SetStringArray(so, "rowActions", KeyRowActions);
            UIKit.SetStringArray(so, "rowParts", KeyRowParts);
            UIKit.SetStringArray(so, "rowLabels", KeyRowLabels);
            UIKit.SetObjArray(so, "rowKeyLabels", keyLabels);
            UIKit.SetObjArray(so, "rowKeyBacks", keyBacks);
            UIKit.SetObjArray(so, "tabBacks", new Object[] { tabKb.targetGraphic, tabGp.targetGraphic });
            UIKit.SetObj(so, "statusLabel", status);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);

            UnityEventTools.AddIntPersistentListener(tabKb.onClick, new UnityAction<int>(view.SelectTab), 0);
            UnityEventTools.AddIntPersistentListener(tabGp.onClick, new UnityAction<int>(view.SelectTab), 1);
            EditorUtility.SetDirty(tabKb);
            EditorUtility.SetDirty(tabGp);

            for (int i = 0; i < n; i++)
            {
                UnityEventTools.AddIntPersistentListener(keyBtns[i].onClick,
                    new UnityAction<int>(view.StartRebind), i);
                EditorUtility.SetDirty(keyBtns[i]);
            }
            UnityEventTools.AddPersistentListener(resetBtn.onClick, new UnityAction(view.ResetDefaults));
            UnityEventTools.AddPersistentListener(closeBtn.onClick, new UnityAction(view.Close));
            EditorUtility.SetDirty(resetBtn);
            EditorUtility.SetDirty(closeBtn);

            root.SetActive(false); // mở qua nút CÀI ĐẶT PHÍM (KeybindPanelView.Open).
            return root;
        }

        // ───────────────────── Pause menu (canvas riêng, trên mọi tầng) ─────────────────────

        /// <summary>
        /// Canvas PauseMenu sorting 400 — trên HUD 100/Menu 200/MainMenu 300 (pause chỉ mở TRONG trận nên
        /// không tranh chấp với menu; 400 để chắc chắn đè HUD + FX). Root luôn active (View nghe ESC);
        /// bảng + settings là con tắt sẵn.
        /// </summary>
        public static GameObject BuildPauseMenu(UIChannels.Bundle ch, GameObject hud)
        {
            // Giữ OVERLAY (không ToCameraSpace): pause chỉ mở TRONG TRẬN — lúc đó scene map có camera
            // riêng render đè camera Lobby, canvas camera-space sẽ vô hình (cùng bệnh với HUD).
            var canvas = UIKit.NewCanvas(PauseMenuName, 400);

            // Bảng pause.
            var panel = UIKit.NewUI("PausePanel", canvas.transform);
            UIKit.StretchFull(panel);

            var dim = UIKit.NewUI("Dim", panel.transform);
            UIKit.StretchFull(dim);
            UIKit.AddImage(dim, DimCol, raycastTarget: true);

            var box = UIKit.NewPanel("Box", panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
                                     new Vector2(480f, 430f), UIKit.ColPanelLight);
            UIKit.AddVertical(box, 20f, 28, TextAnchor.MiddleCenter);

            var title = UIKit.AddLabel(box.transform, "PAUSED", 44, FontStyle.Bold, TextAnchor.MiddleCenter);
            UIKit.SetLayoutHeight(title.gameObject, 64f);

            var resumeBtn = UIKit.MakeButton(box.transform, "TIẾP TỤC", UIKit.ColBtn, 320f, 66f, 26);
            var settingsBtn = UIKit.MakeButton(box.transform, "SETTINGS", UIKit.ColPanel, 320f, 66f, 26);
            var quitBtn = UIKit.MakeButton(box.transform, "VỀ MENU", UIKit.ColBtnAccent, 320f, 66f, 26);

            // Settings bản in-match (sibling của PausePanel — đè lên bảng khi mở).
            var settings = BuildSettingsPanel(canvas.transform, inMatch: true);
            var settingsView = settings.GetComponent<SettingsPanelView>();

            // Applier keybind: root canvas luôn active trong Lobby → áp phím đã lưu cho motor spawn giữa chừng.
            canvas.AddComponent<KeybindApplier>();

            var view = canvas.AddComponent<PauseMenuView>();
            var so = new SerializedObject(view);
            UIKit.SetObj(so, "panel", panel);
            UIKit.SetObj(so, "settingsPanel", settingsView);
            UIKit.SetObj(so, "hudRoot", hud);
            UIKit.SetObj(so, "backToCharSelectChannel", ch.BackToCharSelect);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);

            UnityEventTools.AddPersistentListener(resumeBtn.onClick, new UnityAction(view.Resume));
            UnityEventTools.AddPersistentListener(settingsBtn.onClick, new UnityAction(view.OpenSettings));
            UnityEventTools.AddPersistentListener(quitBtn.onClick, new UnityAction(view.QuitToMenu));
            EditorUtility.SetDirty(resumeBtn);
            EditorUtility.SetDirty(settingsBtn);
            EditorUtility.SetDirty(quitBtn);

            panel.SetActive(false); // View mở khi ESC trong trận.
            return canvas;
        }

        // ───────────────────── Hàng luật trận (StageSelect) ─────────────────────

        /// <summary>
        /// Gắn hàng "THỜI GIAN &lt; 99s &gt; | MẠNG &lt; 3 &gt;" vào cuối StageSelectPanel (panel có sẵn VLG —
        /// hàng tự xếp dưới các nút map). Không có MatchSettingsSO → bỏ qua + cảnh báo (không chặn build).
        /// </summary>
        public static void AddMatchRules(Transform stagePanel, UIChannels.Bundle ch)
        {
            if (ch.MatchSettings == null)
            {
                Debug.LogWarning("[UISettings] Không thấy MatchSettingsSO — bỏ qua hàng luật trận ở StageSelect.");
                return;
            }

            var row = UIKit.NewUI("Row_MatchRules", stagePanel);
            UIKit.SetLayoutHeight(row, 58f).flexibleWidth = 1f;
            UIKit.AddHorizontal(row, 10f, controlW: true, expandW: false, TextAnchor.MiddleCenter);

            UIKit.AddLabel(row.transform, "THỜI GIAN", 22, FontStyle.Bold, TextAnchor.MiddleRight,
                           UIKit.ColTxtDim, width: 150f);
            var timePrev = UIKit.MakeButton(row.transform, "<", UIKit.ColPanelLight, 46f, 46f, 24);
            var timeVal = UIKit.AddLabel(row.transform, "99s", 26, FontStyle.Bold,
                                         TextAnchor.MiddleCenter, UIKit.ColTxt, width: 90f);
            var timeNext = UIKit.MakeButton(row.transform, ">", UIKit.ColPanelLight, 46f, 46f, 24);

            UIKit.AddLabel(row.transform, "MẠNG", 22, FontStyle.Bold, TextAnchor.MiddleRight,
                           UIKit.ColTxtDim, width: 120f);
            var livesPrev = UIKit.MakeButton(row.transform, "<", UIKit.ColPanelLight, 46f, 46f, 24);
            var livesVal = UIKit.AddLabel(row.transform, "3", 26, FontStyle.Bold,
                                          TextAnchor.MiddleCenter, UIKit.ColTxt, width: 60f);
            var livesNext = UIKit.MakeButton(row.transform, ">", UIKit.ColPanelLight, 46f, 46f, 24);

            var view = row.AddComponent<MatchRulesView>();
            var so = new SerializedObject(view);
            UIKit.SetObj(so, "settings", ch.MatchSettings);
            UIKit.SetObj(so, "timeValueLabel", timeVal);
            UIKit.SetObj(so, "livesValueLabel", livesVal);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);

            UnityEventTools.AddPersistentListener(timePrev.onClick, new UnityAction(view.PrevTime));
            UnityEventTools.AddPersistentListener(timeNext.onClick, new UnityAction(view.NextTime));
            UnityEventTools.AddPersistentListener(livesPrev.onClick, new UnityAction(view.PrevLives));
            UnityEventTools.AddPersistentListener(livesNext.onClick, new UnityAction(view.NextLives));
            EditorUtility.SetDirty(timePrev);
            EditorUtility.SetDirty(timeNext);
            EditorUtility.SetDirty(livesPrev);
            EditorUtility.SetDirty(livesNext);
        }
    }
}
