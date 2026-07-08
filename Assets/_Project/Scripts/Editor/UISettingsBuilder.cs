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
                                       new Vector2(720f, 470f), UIKit.ColPanelLight);
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

            var closeBtn = UIKit.MakeButton(panel.transform, "ĐÓNG", UIKit.ColBtn, 240f, 60f, 26);

            var view = root.AddComponent<SettingsPanelView>();
            var so = new SerializedObject(view);
            UIKit.SetObj(so, "masterSlider", master);
            UIKit.SetObj(so, "bgmSlider", bgm);
            UIKit.SetObj(so, "sfxSlider", sfx);
            UIKit.SetObj(so, "masterValueLabel", masterVal);
            UIKit.SetObj(so, "bgmValueLabel", bgmVal);
            UIKit.SetObj(so, "sfxValueLabel", sfxVal);
            UIKit.SetBool(so, "inMatchMode", inMatch);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);

            UnityEventTools.AddPersistentListener(closeBtn.onClick, new UnityAction(view.Close));
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

        // ───────────────────── Pause menu (canvas riêng, trên mọi tầng) ─────────────────────

        /// <summary>
        /// Canvas PauseMenu sorting 400 — trên HUD 100/Menu 200/MainMenu 300 (pause chỉ mở TRONG trận nên
        /// không tranh chấp với menu; 400 để chắc chắn đè HUD + FX). Root luôn active (View nghe ESC);
        /// bảng + settings là con tắt sẵn.
        /// </summary>
        public static GameObject BuildPauseMenu(UIChannels.Bundle ch, GameObject hud)
        {
            var canvas = UIKit.NewCanvas(PauseMenuName, 400);
            UILobbyBuilder.ToCameraSpace(canvas);

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
