using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using FA.Core;
using FA.UI;
using FA.GameFlow;

namespace FA.EditorTools
{
    /// <summary>
    /// PHẦN MÀN HÌNH MENU (P12) của bộ tool dựng UI production (xem <see cref="UIProductionMenu"/>). Dựng 1
    /// Canvas Overlay <c>Menu_Screens</c> gồm 3 panel (MatchManager SetActive đúng 1 theo phase):
    ///   • CharSelect — 2 hàng nút nhân vật (P1/P2) → <see cref="IntButtonRaiser"/> CharSelectedChannel_Pn (value=index).
    ///   • StageSelect — 1 hàng nút map → IntButtonRaiser StageSelectedChannel.
    ///   • EndScreen — <see cref="EndScreenView"/> (winner + stats) + nút Rematch/Back → <see cref="VoidButtonRaiser"/>.
    ///
    /// Đọc <see cref="CharacterRegistrySO"/>/<see cref="StageRegistrySO"/> cho TÊN/icon thật (extensible §1.2:
    /// thêm nhân vật/map = thêm entry → tự thêm nút khi dựng lại). KHÔNG ref GameFlow runtime (chỉ raise kênh
    /// Core §1.3). Mặc định CHỈ CharSelect active để dễ tinh chỉnh; StageSelect/EndScreen tắt (bật khi cần sửa).
    /// EDITOR-ONLY. Trả root Canvas để menu lưu prefab.
    /// </summary>
    internal static class UIScreensBuilder
    {
        public const string MenuName = "Menu_Screens";

        private static readonly Color PanelBig = new Color(0.06f, 0.07f, 0.10f, 0.94f);

        public static GameObject Build(UIChannels.Bundle ch)
        {
            var menu = UIKit.NewCanvas(MenuName, 200);

            var charSel = BuildCharSelect(menu.transform, ch);
            var stageSel = BuildStageSelect(menu.transform, ch);
            var end = BuildEndScreen(menu.transform, ch);

            // Mặc định hiện CharSelect; ẩn 2 panel còn lại (chỉ 1 active mỗi phase — MatchManager điều khiển runtime).
            charSel.SetActive(true);
            stageSel.SetActive(false);
            end.SetActive(false);
            return menu;
        }

        // ───────────────────── CharSelect ─────────────────────

        private static GameObject BuildCharSelect(Transform parent, UIChannels.Bundle ch)
        {
            var panel = UIKit.NewPanel("CharSelectPanel", parent, new Vector2(0.5f, 0.5f), Vector2.zero,
                                       new Vector2(1100f, 600f), PanelBig);
            UIKit.AddVertical(panel, 20f, 28);
            UIKit.AddLabel(panel.transform, "CHARACTER SELECT", 44, FontStyle.Bold, TextAnchor.MiddleCenter);
            UIKit.AddLabel(panel.transform, "P1 chọn (hàng trên) + P2 chọn (hàng dưới) → vào Stage Select",
                           18, FontStyle.Normal, TextAnchor.MiddleCenter, UIKit.ColTxtDim);

            int n = ch.CharacterRegistry != null ? Mathf.Max(1, ch.CharacterRegistry.Count) : 2;
            BuildCharRow(panel.transform, "P1", UIKit.ColP1Accent, n, ch, true);
            BuildCharRow(panel.transform, "P2", UIKit.ColP2Accent, n, ch, false);
            return panel;
        }

        private static void BuildCharRow(Transform parent, string label, Color accent, int count,
                                         UIChannels.Bundle ch, bool isP1)
        {
            var row = UIKit.NewUI($"Row_{label}", parent);
            UIKit.SetLayoutHeight(row, 180f).flexibleWidth = 1f;
            UIKit.AddHorizontal(row, 16f, controlW: false, expandW: false, TextAnchor.MiddleCenter);

            UIKit.AddLabel(row.transform, label, 26, FontStyle.Bold, TextAnchor.MiddleRight, accent, 70f);

            for (int i = 0; i < count; i++)
            {
                string nm = NameOf(ch.CharacterRegistry, i, $"Fighter {i + 1}");
                Sprite icon = IconOf(ch.CharacterRegistry, i);
                MakeCharButton(row.transform, nm, icon, accent, ch.CharSel(isP1), i);
            }
        }

        /// <summary>Nút nhân vật: icon (nếu có) + tên, gắn IntButtonRaiser raise CharSelectedChannel_Pn value=index.</summary>
        private static void MakeCharButton(Transform parent, string name, Sprite icon, Color accent,
                                           IntEventChannelSO channel, int index)
        {
            var btn = UIKit.MakeButton(parent, name, UIKit.ColPanelLight, 160f, 170f, 18);
            // Viền accent mỏng (Image con stretch + accent) để phân biệt P1/P2.
            var frame = UIKit.NewUI("Accent", btn.transform);
            UIKit.StretchFull(frame);
            var frameImg = UIKit.AddImage(frame, new Color(accent.r, accent.g, accent.b, 0.18f));
            frame.transform.SetAsFirstSibling();
            frameImg.raycastTarget = false;

            if (icon != null)
            {
                var iconGo = UIKit.NewUI("Icon", btn.transform);
                var irt = iconGo.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.5f, 1f); irt.anchorMax = new Vector2(0.5f, 1f);
                irt.pivot = new Vector2(0.5f, 1f); irt.anchoredPosition = new Vector2(0f, -14f);
                irt.sizeDelta = new Vector2(110f, 110f);
                var iimg = iconGo.AddComponent<Image>();
                iimg.sprite = icon; iimg.preserveAspect = true; iimg.raycastTarget = false;
            }

            var raiser = btn.gameObject.AddComponent<IntButtonRaiser>();
            var so = new SerializedObject(raiser);
            UIKit.SetObj(so, "channel", channel);
            UIKit.SetInt(so, "value", index);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(raiser);
        }

        // ───────────────────── StageSelect ─────────────────────

        private static GameObject BuildStageSelect(Transform parent, UIChannels.Bundle ch)
        {
            var panel = UIKit.NewPanel("StageSelectPanel", parent, new Vector2(0.5f, 0.5f), Vector2.zero,
                                       new Vector2(1000f, 460f), PanelBig);
            UIKit.AddVertical(panel, 20f, 28);
            UIKit.AddLabel(panel.transform, "STAGE SELECT", 44, FontStyle.Bold, TextAnchor.MiddleCenter);
            UIKit.AddLabel(panel.transform, "Chọn map → vào Match (load scene map additive)",
                           18, FontStyle.Normal, TextAnchor.MiddleCenter, UIKit.ColTxtDim);

            var row = UIKit.NewUI("Row_Stage", panel.transform);
            UIKit.SetLayoutHeight(row, 220f).flexibleWidth = 1f;
            UIKit.AddHorizontal(row, 20f, controlW: false, expandW: false, TextAnchor.MiddleCenter);

            int n = ch.StageRegistry != null ? Mathf.Max(1, ch.StageRegistry.Count) : 2;
            for (int i = 0; i < n; i++)
            {
                string nm = NameOf(ch.StageRegistry, i, $"Stage {i + 1}");
                var btn = UIKit.MakeButton(row.transform, nm, UIKit.ColPanelLight, 220f, 200f, 22);
                var raiser = btn.gameObject.AddComponent<IntButtonRaiser>();
                var so = new SerializedObject(raiser);
                UIKit.SetObj(so, "channel", ch.StageSel);
                UIKit.SetInt(so, "value", i);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(raiser);
            }
            return panel;
        }

        // ───────────────────── EndScreen ─────────────────────

        private static GameObject BuildEndScreen(Transform parent, UIChannels.Bundle ch)
        {
            var panel = UIKit.NewPanel("EndScreenPanel", parent, new Vector2(0.5f, 0.5f), Vector2.zero,
                                       new Vector2(1100f, 700f), PanelBig);
            UIKit.AddVertical(panel, 16f, 30);

            var winner = UIKit.AddLabel(panel.transform, "PLAYER 1 WINS", 52, FontStyle.Bold, TextAnchor.MiddleCenter);
            UIKit.SetLayoutHeight(winner.gameObject, 70f);

            var statsRow = UIKit.NewUI("StatsRow", panel.transform);
            UIKit.SetLayoutHeight(statsRow, 360f).flexibleHeight = 1f;
            var shlg = UIKit.AddHorizontal(statsRow, 40f, controlW: true, expandW: true, TextAnchor.UpperCenter);
            shlg.childForceExpandHeight = true;
            var s1 = MakeStatsCard(statsRow.transform, "P1", UIKit.ColP1Accent);
            var s2 = MakeStatsCard(statsRow.transform, "P2", UIKit.ColP2Accent);

            var view = panel.AddComponent<EndScreenView>();
            var so = new SerializedObject(view);
            UIKit.SetObj(so, "channel", ch.MatchResult);
            UIKit.SetObj(so, "winnerLabel", winner);
            UIKit.SetObj(so, "statsP1Label", s1);
            UIKit.SetObj(so, "statsP2Label", s2);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);

            var btnRow = UIKit.NewUI("Buttons", panel.transform);
            UIKit.SetLayoutHeight(btnRow, 72f);
            UIKit.AddHorizontal(btnRow, 24f, controlW: false, expandW: false, TextAnchor.MiddleCenter);
            MakeVoidButton(btnRow.transform, "REMATCH", UIKit.ColBtn, ch.Rematch);
            MakeVoidButton(btnRow.transform, "BACK", UIKit.ColBtnAccent, ch.BackToCharSelect);
            return panel;
        }

        /// <summary>Card stats 1 người: nền + Text (EndScreenView set nội dung runtime). Trả Text để wire.</summary>
        private static Text MakeStatsCard(Transform parent, string who, Color accent)
        {
            var card = UIKit.NewUI($"Card_{who}", parent);
            var le = card.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f; le.flexibleHeight = 1f;
            UIKit.AddImage(card, new Color(accent.r, accent.g, accent.b, 0.14f));

            // Text đặt trên GameObject CON (KHÔNG chung object với Image nền — 2 Graphic cùng GO xung đột → AddComponent null).
            var label = UIKit.AddLabel(card.transform, $"{who}\n...", 24, FontStyle.Normal, TextAnchor.UpperLeft);
            label.gameObject.name = $"Stats_{who}";
            var rt = label.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(20f, 20f); rt.offsetMax = new Vector2(-20f, -20f);
            EditorUtility.SetDirty(label);
            return label;
        }

        private static void MakeVoidButton(Transform parent, string text, Color color, VoidEventChannelSO channel)
        {
            var btn = UIKit.MakeButton(parent, text, color, 240f, 60f);
            var raiser = btn.gameObject.AddComponent<VoidButtonRaiser>();
            var so = new SerializedObject(raiser);
            UIKit.SetObj(so, "channel", channel);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(raiser);
        }

        // ───────────────────── registry read ─────────────────────

        private static string NameOf(CharacterRegistrySO reg, int i, string fallback)
        {
            var e = reg != null ? reg.Get(i) : null;
            return (e != null && !string.IsNullOrEmpty(e.displayName)) ? e.displayName : fallback;
        }

        private static Sprite IconOf(CharacterRegistrySO reg, int i)
        {
            var e = reg != null ? reg.Get(i) : null;
            return e != null ? e.icon : null;
        }

        private static string NameOf(StageRegistrySO reg, int i, string fallback)
        {
            var e = reg != null ? reg.Get(i) : null;
            return (e != null && !string.IsNullOrEmpty(e.displayName)) ? e.displayName : fallback;
        }
    }
}
