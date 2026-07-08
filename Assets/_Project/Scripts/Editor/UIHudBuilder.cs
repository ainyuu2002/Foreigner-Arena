using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using FA.Core;
using FA.UI;

namespace FA.EditorTools
{
    /// <summary>
    /// PHẦN HUD TRONG TRẬN (P8 + P12) — layout PHONG CÁCH STREET FIGHTER 6 (xem <see cref="UIProductionMenu"/>).
    /// Map tài nguyên game (KHÔNG có HP) vào khung SF6:
    ///   • Trên cùng "Vitality" = **% knockback** — thanh LUÔN ĐẦY, đổi MÀU trắng→đỏ theo % (PercentColorView).
    ///   • Thanh "Drive" = **Nộ** (fill 0..1, NGẮN hơn thanh %, chia 2 ô) + Portrait + badge "C" + Tên + Lives.
    ///   • Combo counter (số HITS) — ngay DƯỚI cụm trên, sát mép góc; chỉ hiện khi combo ≥ 2.
    ///   • Gauge dưới "Super Art" = **Mana** (chia ô thật, ManaSegmentView) — MỎNG; P1 dưới-trái / P2 dưới-phải.
    ///   • Timer ở trên-giữa.
    /// Gắn View FA.UI + wire kênh per-player (UIChannels). Art = placeholder màu/slot Image trống → bạn swap
    /// sprite sau trong Inspector, KHÔNG sửa code. EDITOR-ONLY. Trả root Canvas để menu lưu prefab.
    /// </summary>
    internal static class UIHudBuilder
    {
        public const string HudName = "HUD_InMatch";

        // ── Palette kiểu SF6 ──
        private static readonly Color VitLow   = Color.white;                       // % = 0  (trắng)
        private static readonly Color VitHigh  = new Color(0.92f, 0.16f, 0.18f, 1f);// % = max (đỏ)
        private static readonly Color DriveCol = new Color(0.70f, 0.90f, 0.22f, 1f);// Nộ (vàng-lục Drive)
        private static readonly Color SuperCol = new Color(0.95f, 0.25f, 0.62f, 1f);// Mana (hồng Super Art)
        private static readonly Color SuperTrack = new Color(0.22f, 0.10f, 0.18f, 0.95f);
        private static readonly Color ComboCol = new Color(0.97f, 0.32f, 0.55f, 1f);// số combo (hồng)
        private static readonly Color BarBack  = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color Notch    = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color Portrait = new Color(0.10f, 0.11f, 0.15f, 1f);
        private static readonly Color AccP1    = new Color(0.93f, 0.23f, 0.30f, 1f);// nhấn P1 (đỏ)
        private static readonly Color AccP2    = new Color(0.20f, 0.55f, 0.95f, 1f);// nhấn P2 (xanh)
        private static readonly Color LifeCol  = new Color(0.96f, 0.83f, 0.22f, 1f);
        private const float Skew = 0.5f; // tỉ lệ nghiêng parallelogram (theo chiều cao) — kiểu SF6.

        /// <summary>Dấu nghiêng ĐỐI XỨNG: P1 (trái) nghiêng vào giữa (+), P2 (phải) nghiêng ngược (−) → mirror.</summary>
        private static float SkewFor(bool isP1) => isP1 ? Skew : -Skew;

        // ── helpers nghiêng (parallelogram SF6) ──

        /// <summary>Bỏ sprite + Type=Simple → quad TRẮNG SẮC CẠNH (không bo góc) để khi shear ra parallelogram nét.</summary>
        private static void Sharpen(Image img) { img.sprite = null; img.type = Image.Type.Simple; }

        /// <summary>Gắn <see cref="UISkew"/> (bẻ nghiêng mesh) vào GameObject có Graphic.</summary>
        private static void AddSkew(GameObject go, float slant = Skew)
        {
            var s = go.AddComponent<UISkew>();
            var so = new SerializedObject(s);
            UIKit.SetFloat(so, "slant", slant);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(s);
        }

        public static GameObject Build(UIChannels.Bundle ch)
        {
            int maxSegments = ResolveMaxSegments();
            int maxLives = ResolveMaxLives();

            var hud = UIKit.NewCanvas(HudName, 100);

            BuildTimer(hud.transform, ch.Timer);
            BuildTopCluster(hud.transform, true, ch, maxLives);
            BuildTopCluster(hud.transform, false, ch, maxLives);
            BuildCombo(hud.transform, true, ch);
            BuildCombo(hud.transform, false, ch);
            BuildManaCluster(hud.transform, true, ch, maxSegments);
            BuildManaCluster(hud.transform, false, ch, maxSegments);
            return hud;
        }

        // ───────────────────── Timer (trên-giữa) ─────────────────────

        private static void BuildTimer(Transform parent, FloatEventChannelSO timerCh)
        {
            var panel = UIKit.NewPanel("TimerPanel", parent, new Vector2(0.5f, 1f), new Vector2(0f, -14f),
                                       new Vector2(150f, 96f), new Color(0f, 0f, 0f, 0.55f));
            var label = UIKit.AddLabel(panel.transform, "99", 64, FontStyle.Bold, TextAnchor.MiddleCenter);
            UIKit.StretchFull(label.gameObject);

            var view = panel.AddComponent<TimerView>();
            var so = new SerializedObject(view);
            UIKit.SetObj(so, "channel", timerCh);
            UIKit.SetObj(so, "label", label);
            UIKit.SetBool(so, "showAsMinutes", false);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
        }

        // ───────────────────── Cụm TRÊN (Vitality + Drive + Portrait + Lives) ─────────────────────

        private static void BuildTopCluster(Transform parent, bool isP1, UIChannels.Bundle ch, int maxLives)
        {
            string side = isP1 ? "P1" : "P2";
            Color acc = isP1 ? AccP1 : AccP2;
            Vector2 anchor = new Vector2(isP1 ? 0f : 1f, 1f);
            Vector2 pos = new Vector2(isP1 ? 20f : -20f, -16f);

            var root = Container($"HUDTop_{side}", parent, anchor, pos, new Vector2(840f, 168f));
            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f; hlg.childControlWidth = false; hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            hlg.childAlignment = isP1 ? TextAnchor.UpperLeft : TextAnchor.UpperRight;

            var portrait = MakePortrait($"Portrait_{side}", root.transform, acc);
            var info = MakeTopInfo($"Info_{side}", root.transform, isP1, ch, maxLives);

            // Mirror: P2 để Info trước, Portrait sau (portrait nằm ngoài-phải).
            if (!isP1) { info.transform.SetSiblingIndex(0); portrait.transform.SetSiblingIndex(1); }
        }

        private static GameObject MakePortrait(string name, Transform parent, Color accent)
        {
            var holder = UIKit.NewUI(name, parent);
            var le = holder.AddComponent<LayoutElement>();
            le.preferredWidth = 112f; le.preferredHeight = 112f; le.minWidth = 112f; le.minHeight = 112f;
            holder.GetComponent<RectTransform>().sizeDelta = new Vector2(112f, 112f);
            UIKit.AddImage(holder, accent); // viền accent (sau swap khung sprite).

            var pic = UIKit.NewUI("Pic", holder.transform);
            var prt = pic.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = new Vector2(4f, 4f); prt.offsetMax = new Vector2(-4f, -4f);
            UIKit.AddImage(pic, Portrait); // ← GÁN SPRITE PORTRAIT VÀO ĐÂY (Image của 'Pic').

            // Badge "C" góc trên-trong.
            var badge = UIKit.NewUI("Badge_C", holder.transform);
            var brt = badge.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(0f, 1f); brt.pivot = new Vector2(0f, 1f);
            brt.anchoredPosition = new Vector2(2f, -2f); brt.sizeDelta = new Vector2(30f, 30f);
            UIKit.AddImage(badge, accent);
            var bl = UIKit.AddLabel(badge.transform, "C", 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            UIKit.StretchFull(bl.gameObject);
            return holder;
        }

        private static GameObject MakeTopInfo(string name, Transform parent, bool isP1, UIChannels.Bundle ch,
                                              int maxLives)
        {
            var col = UIKit.NewUI(name, parent);
            var le = col.AddComponent<LayoutElement>();
            le.preferredWidth = 700f; le.minWidth = 700f; le.preferredHeight = 150f;
            col.GetComponent<RectTransform>().sizeDelta = new Vector2(700f, 150f);
            var vlg = col.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 5f; vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.childAlignment = isP1 ? TextAnchor.UpperLeft : TextAnchor.UpperRight;

            // Tên người chơi (đặt tên nhân vật thật sau).
            var nameLbl = UIKit.AddLabel(col.transform, isP1 ? "PLAYER 1" : "PLAYER 2", 30, FontStyle.Bold,
                                         isP1 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, Color.white);
            UIKit.SetLayoutHeight(nameLbl.gameObject, 36f);

            // Vitality = % — thanh LUÔN đầy, đổi màu trắng→đỏ theo %.
            BuildColorBar(col.transform, "Vitality_%", ch.Percent(isP1), ch.PercentCap, 32f, isP1);

            // Drive = Nộ — thanh fill NGẮN hơn (55% bề ngang), DÀY hơn, chia 2 ô bằng vạch NGHIÊNG (SF6).
            BuildBar(col.transform, "Drive_Rage", ch.Rage(isP1), DriveCol, false, 1f, 24f, 2, 0.55f, isP1);

            // Lives.
            BuildLivesRow(col.transform, isP1, ch, maxLives);
            return col;
        }

        // ───────────────────── Thanh % đổi MÀU (luôn đầy) ─────────────────────

        private static void BuildColorBar(Transform parent, string label, FloatEventChannelSO channel,
                                          float maxRaw, float height, bool isP1)
        {
            float slant = SkewFor(isP1);
            var wrapper = UIKit.NewUI(label, parent);
            UIKit.SetLayoutHeight(wrapper, height);       // full width trong VLG
            Sharpen(UIKit.AddImage(wrapper, BarBack));    // nền tối (parallelogram).
            AddSkew(wrapper, slant);

            var bar = UIKit.NewUI("Bar", wrapper.transform);
            UIKit.StretchFull(bar);                       // LUÔN đầy khung.
            var barImg = UIKit.AddImage(bar, VitLow);
            Sharpen(barImg);
            AddSkew(bar, slant);

            var view = wrapper.AddComponent<PercentColorView>();
            var so = new SerializedObject(view);
            UIKit.SetObj(so, "channel", channel);
            UIKit.SetObj(so, "barImage", barImg);
            UIKit.SetFloat(so, "maxRawValue", Mathf.Max(1f, maxRaw));
            var lo = so.FindProperty("lowColor"); if (lo != null) lo.colorValue = VitLow;
            var hi = so.FindProperty("highColor"); if (hi != null) hi.colorValue = VitHigh;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
        }

        // ───────────────────── Thanh fill (Drive/Nộ) ─────────────────────

        private static void BuildBar(Transform parent, string label, FloatEventChannelSO channel, Color color,
                                     bool normalizeRaw, float maxRaw, float height, int notchSegments,
                                     float widthFrac, bool isP1)
        {
            // wrapper full-width (VLG expand) — thanh thật nằm bên trong theo widthFrac, dồn về phía người chơi.
            var wrapper = UIKit.NewUI(label, parent);
            UIKit.SetLayoutHeight(wrapper, height);

            var bar = UIKit.NewUI("Bar", wrapper.transform);
            var rt = bar.GetComponent<RectTransform>();
            if (widthFrac >= 1f) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; }
            else if (isP1)       { rt.anchorMin = new Vector2(0f, 0f);            rt.anchorMax = new Vector2(widthFrac, 1f); }
            else                 { rt.anchorMin = new Vector2(1f - widthFrac, 0f); rt.anchorMax = new Vector2(1f, 1f); }
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            float slant = SkewFor(isP1);
            Sharpen(UIKit.AddImage(bar, BarBack));
            AddSkew(bar, slant);

            var fillGo = UIKit.NewUI("Fill", bar.transform);
            UIKit.StretchFull(fillGo);
            var fillImg = UIKit.AddImage(fillGo, color);
            fillImg.type = Image.Type.Filled;          // Filled cần sprite → giữ UISprite (không Sharpen).
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)(isP1 ? Image.OriginHorizontal.Left : Image.OriginHorizontal.Right);
            fillImg.fillAmount = 0f;
            AddSkew(fillGo, slant);

            if (notchSegments > 1) AddNotches(bar, notchSegments, slant);

            // FX đầy thanh: nộ fill = 1 → vầng glow thở + fill sáng trắng theo nhịp.
            var glowImg = MakeFullGlow(bar.transform, color, slant);
            var glowFx = bar.AddComponent<BarFullGlowFX>();
            var soG = new SerializedObject(glowFx);
            UIKit.SetObj(soG, "floatChannel", channel);
            UIKit.SetFloat(soG, "floatFullAt", 1f);
            UIKit.SetObj(soG, "glowImage", glowImg);
            UIKit.SetObjArray(soG, "tintImages", new Object[] { fillImg });
            soG.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(glowFx);

            var view = fillGo.AddComponent<ResourceBarView>();
            var so = new SerializedObject(view);
            UIKit.SetObj(so, "channel", channel);
            UIKit.SetObj(so, "fillImage", fillImg);
            UIKit.SetBool(so, "normalizeRawValue", normalizeRaw);
            UIKit.SetFloat(so, "maxRawValue", Mathf.Max(1f, maxRaw));
            // Reset đầu trận: HUD bật lại là fill về 0 — không giữ giá trị trận trước / trạng thái build.
            UIKit.SetBool(so, "resetOnEnable", true);
            UIKit.SetFloat(so, "initialFill", 0f);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
        }

        /// <summary>
        /// Vạch chia kiểu Drive Gauge SF6 — divider thẳng đứng, SHEAR cùng tỉ lệ <see cref="Skew"/> với thanh
        /// (qua <see cref="UISkew"/>) → song song mép nghiêng, chia thanh thành các ô parallelogram. Không raycast.
        /// </summary>
        private static void AddNotches(GameObject holder, int segments, float slant)
        {
            for (int i = 1; i < segments; i++)
            {
                float f = (float)i / segments;
                var n = UIKit.NewUI($"Notch_{i}", holder.transform);
                var rt = n.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(f, 0f); rt.anchorMax = new Vector2(f, 1f); rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(4f, 0f);
                var img = n.AddComponent<Image>(); img.color = Notch; img.raycastTarget = false;
                AddSkew(n, slant); // nghiêng song song mép thanh.
            }
        }

        // ───────────────────── Lives ─────────────────────

        private static void BuildLivesRow(Transform parent, bool isP1, UIChannels.Bundle ch, int maxLives)
        {
            var row = UIKit.NewUI("LivesRow", parent);
            UIKit.SetLayoutHeight(row, 26f);
            UIKit.AddHorizontal(row, 6f, controlW: false, expandW: false,
                                isP1 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);

            var icons = new List<Image>(maxLives);
            for (int i = 0; i < maxLives; i++)
            {
                var ico = UIKit.NewUI($"Life_{i}", row.transform);
                var img = UIKit.AddImage(ico, LifeCol);
                var le = ico.AddComponent<LayoutElement>();
                le.preferredWidth = 22f; le.preferredHeight = 22f; le.minWidth = 22f; le.minHeight = 22f;
                icons.Add(img);
            }

            var view = row.AddComponent<LivesView>();
            var so = new SerializedObject(view);
            UIKit.SetObj(so, "channel", ch.Lives(isP1));
            UIKit.SetObjArray(so, "icons", icons.ToArray());
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
        }

        // ───────────────────── Combo (dưới cụm trên, sát mép góc; chỉ hiện khi combo) ─────────────────────

        private static void BuildCombo(Transform parent, bool isP1, UIChannels.Bundle ch)
        {
            string side = isP1 ? "P1" : "P2";
            Vector2 anchor = new Vector2(isP1 ? 0f : 1f, 1f);   // neo TOP-side (ngay dưới cụm trên).
            Vector2 pos = new Vector2(isP1 ? 40f : -40f, -200f);

            // ROOT luôn active (giữ ComboView subscribe) → ẩn/hiện CONTENT con.
            var root = Container($"Combo_{side}", parent, anchor, pos, new Vector2(300f, 96f));

            var content = UIKit.NewUI("ComboContent", root.transform);
            UIKit.StretchFull(content);

            // Nền parallelogram (box nghiêng kiểu SF6) sau số combo. ignoreLayout để HLG không xếp/co nó.
            var bg = UIKit.NewUI("ComboBG", content.transform);
            UIKit.StretchFull(bg);
            bg.AddComponent<LayoutElement>().ignoreLayout = true;
            Sharpen(UIKit.AddImage(bg, new Color(0f, 0f, 0f, 0.45f)));
            AddSkew(bg, SkewFor(isP1));

            UIKit.AddHorizontal(content, 8f, controlW: false, expandW: false,
                                isP1 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);

            Text number;
            if (isP1)
            {
                number = UIKit.AddLabel(content.transform, "0", 80, FontStyle.Bold, TextAnchor.MiddleCenter, ComboCol, 120f);
                UIKit.AddLabel(content.transform, "HITS", 28, FontStyle.Bold, TextAnchor.LowerLeft, Color.white, 96f);
            }
            else
            {
                UIKit.AddLabel(content.transform, "HITS", 28, FontStyle.Bold, TextAnchor.LowerRight, Color.white, 96f);
                number = UIKit.AddLabel(content.transform, "0", 80, FontStyle.Bold, TextAnchor.MiddleCenter, ComboCol, 120f);
            }

            var view = root.AddComponent<ComboView>();
            var so = new SerializedObject(view);
            UIKit.SetObj(so, "channel", ch.Combo(isP1));
            UIKit.SetObj(so, "countLabel", number);
            UIKit.SetObj(so, "content", content);
            UIKit.SetInt(so, "minToShow", 2);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
        }

        // ───────────────────── Super Art = Mana (MỎNG, dưới-góc) ─────────────────────

        private static void BuildManaCluster(Transform parent, bool isP1, UIChannels.Bundle ch, int maxSegments)
        {
            string side = isP1 ? "P1" : "P2";
            Vector2 anchor = new Vector2(isP1 ? 0f : 1f, 0f);
            Vector2 pos = new Vector2(isP1 ? 28f : -28f, 28f);

            // Container = chính dãy ô (thanh mỏng 16px).
            var root = Container($"SuperArt_Mana_{side}", parent, anchor, pos, new Vector2(340f, 16f));
            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f; hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
            hlg.childAlignment = isP1 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;

            int n = Mathf.Max(1, maxSegments);
            var fills = new List<Image>(n);
            for (int i = 0; i < n; i++)
            {
                var cell = UIKit.NewUI($"Cell_{i}", root.transform);
                Sharpen(UIKit.AddImage(cell, SuperTrack));
                AddSkew(cell, SkewFor(isP1));
                var fillGo = UIKit.NewUI("Fill", cell.transform);
                UIKit.StretchFull(fillGo);
                var fillImg = UIKit.AddImage(fillGo, SuperCol);
                Sharpen(fillImg);
                AddSkew(fillGo, SkewFor(isP1));
                fills.Add(fillImg);
            }

            // FX đầy ô: đủ n ô mana → vầng glow hồng thở sau cả dãy + các ô sáng trắng theo nhịp.
            var glowImg = MakeFullGlow(root.transform, SuperCol, SkewFor(isP1));
            var glowFx = root.AddComponent<BarFullGlowFX>();
            var soG = new SerializedObject(glowFx);
            UIKit.SetObj(soG, "intChannel", ch.ManaSeg(isP1));
            UIKit.SetInt(soG, "intFullAt", n);
            UIKit.SetObj(soG, "glowImage", glowImg);
            UIKit.SetObjArray(soG, "tintImages", fills.ToArray());
            soG.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(glowFx);

            var view = root.AddComponent<ManaSegmentView>();
            var so = new SerializedObject(view);
            UIKit.SetObj(so, "segmentsChannel", ch.ManaSeg(isP1));
            UIKit.SetObjArray(so, "cells", fills.ToArray());
            // Reset đầu trận: 0 ô — vụ "một bên đầy một bên không": ô hồng lúc build là placeholder,
            // bên nào không có hệ mana raise sẽ đứng nguyên "đầy giả" vĩnh viễn.
            UIKit.SetBool(so, "resetOnEnable", true);
            UIKit.SetInt(so, "initialSegments", 0);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
        }

        // ───────────────────── helpers ─────────────────────

        /// <summary>
        /// Vầng glow "đầy thanh" cho <see cref="BarFullGlowFX"/>: Image cùng dáng nghiêng, TO hơn thanh
        /// vài px, alpha 0 sẵn (FX tự thở khi đầy). SetAsFirstSibling → vẽ DƯỚI ô/fill; ignoreLayout →
        /// HLG của dãy ô mana không cuốn nó vào flow (bài học viền Highlight card random).
        /// </summary>
        private static Image MakeFullGlow(Transform parent, Color barColor, float slant)
        {
            var go = UIKit.NewUI("FullGlow", parent);
            go.transform.SetAsFirstSibling();
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            UIKit.StretchFull(go);
            var rt = go.GetComponent<RectTransform>();
            rt.offsetMin = new Vector2(-9f, -6f);
            rt.offsetMax = new Vector2(9f, 6f);
            Color glowCol = Color.Lerp(barColor, Color.white, 0.45f); // sáng hơn màu thanh — nhìn như phát quang.
            glowCol.a = 0f;
            var img = UIKit.AddImage(go, glowCol);
            AddSkew(go, slant);
            return img;
        }

        private static GameObject Container(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = UIKit.NewUI(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return go;
        }

        private static int ResolveMaxSegments()
        {
            var cfg = FindFirst<FA.Resources.ResourceConfigSO>();
            return cfg != null ? Mathf.Max(1, cfg.MaxSegments) : 3;
        }

        private static int ResolveMaxLives()
        {
            var s = FindFirst<FA.GameFlow.MatchSettingsSO>();
            return s != null ? Mathf.Clamp(s.startingLives, 1, 5) : 3;
        }

        private static T FindFirst<T>() where T : Object
        {
            // Quét System.IO đường dẫn TUYỆT ĐỐI (v9 — CWD của Unity bị file dialog đổi mất, xem
            // UIKit.FindProjectFiles) — KHÔNG tin AssetDatabase.FindAssets (index trượt .asset).
            foreach (string file in UIKit.FindProjectFiles("*.asset"))
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(file);
                if (a != null) return a;
            }
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null) return a;
            }
            return null;
        }
    }
}
