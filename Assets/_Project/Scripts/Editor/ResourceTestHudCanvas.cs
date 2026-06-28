using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using FA.Core;
using FA.UI;

namespace FA.EditorTools
{
    /// <summary>
    /// PHẦN CANVAS của tool debug "Setup Resource Test HUD" (xem <see cref="ResourceTestHudSetup"/>).
    /// 1 chức năng: dựng Canvas Screen-Space-Overlay <c>__ResourceTestHUD</c> + 2 panel (P1 trái, P2 phải),
    /// mỗi panel 3 widget (% / Mana ô / Rage) gắn ResourceBarView + ManaSegmentView (FA.UI — Unity-native).
    ///
    /// KHÔNG tự vẽ bar bằng code: % & Rage = Image Type=Filled + ResourceBarView set fillAmount; Mana = N ô
    /// Image + HorizontalLayoutGroup + ManaSegmentView (§1.1). % raise THÔ 0..cap → bật normalizeRawValue +
    /// maxRawValue=cap (đọc KnockbackPercent.cs: RaiseEvent(_percent) là giá trị thô, không /cap). Mana/Rage
    /// raise sẵn fill 0..1 → KHÔNG normalize.
    ///
    /// Ô mana hiện rõ cả đầy/rỗng KỂ CẢ khi không có sprite: mỗi ô = "track" (xám mờ, luôn bật) + "fill"
    /// (trắng) — ManaSegmentView (no-sprite path) toggle enabled fill; track nền vẫn thấy ô rỗng.
    /// </summary>
    internal static class ResourceTestHudCanvas
    {
        public const string HudName = "__ResourceTestHUD";

        private static readonly Color PercentColor = new Color(0.9f, 0.25f, 0.2f, 1f);
        private static readonly Color RageColor = new Color(1f, 0.55f, 0.05f, 1f);
        private static readonly Color BarBackColor = new Color(0f, 0f, 0f, 0.5f);
        private static readonly Color CellFull = Color.white;
        private static readonly Color CellTrack = new Color(0.25f, 0.25f, 0.28f, 0.9f);
        private static readonly Color PanelBack = new Color(0f, 0f, 0f, 0.35f);
        private static readonly Color LabelColor = Color.white;

        /// <summary>Dựng HUD mới. Trả root GameObject (để debug-reset gắn lên + log). percentCap để chuẩn hóa %.</summary>
        public static GameObject Build(ResourceTestHudAssets.Bundle b, int maxSegments, float percentCap)
        {
            var hud = new GameObject(HudName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(hud, "Create Resource Test HUD");

            var canvas = hud.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999; // nổi trên mọi UI khác.
            var scaler = hud.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            BuildPanel(hud.transform, true, b.PercentP1, b.ManaSegP1, b.RageP1, maxSegments, percentCap);
            BuildPanel(hud.transform, false, b.PercentP2, b.ManaSegP2, b.RageP2, maxSegments, percentCap);
            return hud;
        }

        private static void BuildPanel(Transform parent, bool isP1,
                                       FloatEventChannelSO percentCh, IntEventChannelSO segCh,
                                       FloatEventChannelSO rageCh, int maxSegments, float percentCap)
        {
            string side = isP1 ? "P1" : "P2";
            var panel = NewUI($"Panel_{side}", parent);
            var prt = panel.GetComponent<RectTransform>();
            // P1 góc trên-trái, P2 góc trên-phải.
            float ax = isP1 ? 0f : 1f;
            prt.anchorMin = new Vector2(ax, 1f);
            prt.anchorMax = new Vector2(ax, 1f);
            prt.pivot = new Vector2(ax, 1f);
            prt.anchoredPosition = new Vector2(isP1 ? 24f : -24f, -24f);
            prt.sizeDelta = new Vector2(520f, 200f);
            AddImage(panel, PanelBack);

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.padding = new RectOffset(14, 14, 14, 14);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            AddLabel(panel.transform, side, 28, FontStyle.Bold);

            BuildFillRow(panel.transform, $"{side} %", percentCh, PercentColor, true, percentCap);
            BuildManaRow(panel.transform, $"{side} Mana", segCh, maxSegments);
            BuildFillRow(panel.transform, $"{side} Rage", rageCh, RageColor, false, 1f);
        }

        /// <summary>Hàng thanh fill liên tục (% hoặc Rage): label + Image Filled + ResourceBarView.</summary>
        private static void BuildFillRow(Transform parent, string label, FloatEventChannelSO channel,
                                         Color color, bool normalizeRaw, float maxRaw)
        {
            var row = NewUI($"Row_{label}", parent);
            SetHeight(row, 40f);
            AddLabel(row.transform, label, 18, FontStyle.Normal, 110f);

            var barHolder = NewUI("Bar", row.transform);
            StretchWithLeftPad(barHolder, 120f);
            AddImage(barHolder, BarBackColor);

            var fillGo = NewUI("Fill", barHolder.transform);
            StretchFull(fillGo);
            var fillImg = AddImage(fillGo, color);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = (channel != null && normalizeRaw) ? 0f : 0f;

            var view = fillGo.AddComponent<ResourceBarView>();
            var so = new SerializedObject(view);
            so.FindProperty("channel").objectReferenceValue = channel;
            so.FindProperty("fillImage").objectReferenceValue = fillImg;
            so.FindProperty("normalizeRawValue").boolValue = normalizeRaw;
            so.FindProperty("maxRawValue").floatValue = Mathf.Max(1f, maxRaw);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Hàng mana chia ô: label + dãy N ô (track xám + fill trắng) + ManaSegmentView.</summary>
        private static void BuildManaRow(Transform parent, string label, IntEventChannelSO segCh, int maxSegments)
        {
            var row = NewUI($"Row_{label}", parent);
            SetHeight(row, 40f);
            AddLabel(row.transform, label, 18, FontStyle.Normal, 110f);

            var cellsHolder = NewUI("Cells", row.transform);
            StretchWithLeftPad(cellsHolder, 120f);
            var hlg = cellsHolder.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            var fills = new List<Image>(maxSegments);
            int n = Mathf.Max(1, maxSegments);
            for (int i = 0; i < n; i++)
            {
                var cell = NewUI($"Cell_{i}", cellsHolder.transform);
                AddImage(cell, CellTrack); // track xám luôn hiện → ô rỗng vẫn thấy.

                var fill = NewUI("Fill", cell.transform);
                StretchFull(fill);
                var fillImg = AddImage(fill, CellFull); // ô đầy = trắng; ManaSegmentView toggle enabled.
                fills.Add(fillImg);
            }

            var view = cellsHolder.AddComponent<ManaSegmentView>();
            var so = new SerializedObject(view);
            so.FindProperty("segmentsChannel").objectReferenceValue = segCh;
            var cellsProp = so.FindProperty("cells");
            cellsProp.arraySize = fills.Count;
            for (int i = 0; i < fills.Count; i++)
                cellsProp.GetArrayElementAtIndex(i).objectReferenceValue = fills[i];
            // KHÔNG gán full/emptySprite → ManaSegmentView dùng no-sprite path (toggle enabled fill).
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ───────────────────────── UI helpers (Unity-native RectTransform) ─────────────────────────

        private static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Sprite _uiSprite;

        /// <summary>Sprite UI built-in của Unity (UISprite.psd). Image Type=Filled BẮT BUỘC có sprite thì
        /// fillAmount mới có tác dụng — thiếu sprite → thanh vẽ khối đặc full-size, không bao giờ đổi.</summary>
        private static Sprite UiSprite
        {
            get
            {
                if (_uiSprite == null)
                    _uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                return _uiSprite;
            }
        }

        private static Image AddImage(GameObject go, Color c)
        {
            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.sprite = UiSprite;   // BẮT BUỘC cho Image Type=Filled (fill bar %/rage) hoạt động.
            img.type = Image.Type.Simple;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        private static Text AddLabel(Transform parent, string text, int size, FontStyle style, float width = 0f)
        {
            var go = NewUI($"Label_{text}", parent);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = AssetDatabase.GetBuiltinExtraResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.fontStyle = style;
            t.color = LabelColor;
            t.alignment = TextAnchor.MiddleLeft;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            if (width > 0f)
            {
                var le = go.AddComponent<LayoutElement>();
                le.preferredWidth = width;
                le.minWidth = width;
            }
            return t;
        }

        private static void SetHeight(GameObject go, float h)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredHeight = h;
            le.minHeight = h;
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
        }

        private static void StretchFull(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Cho phần tử nằm trong row dùng layout: chỉ cần LayoutElement flexibleWidth để chiếm phần còn lại.</summary>
        private static void StretchWithLeftPad(GameObject go, float leftPad)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
        }
    }
}
