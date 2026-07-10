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
    ///   • Dưới thanh % = **Mana 4 ô** MÀU PHE (P1 đỏ/P2 xanh), dày bằng thanh %, 80% bề ngang
    ///     (v10.1 đổi chỗ với nộ) + Portrait + badge "C" + Tên + Lives (icon TIM, tint vàng).
    ///   • Combo counter (số HITS) — ngay DƯỚI cụm trên, sát mép góc; chỉ hiện khi combo ≥ 2.
    ///   • Gauge dưới-góc = **Nộ** (bar CAM 2 ô, ResourceBarView); P1 dưới-trái / P2 dưới-phải.
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
        private static readonly Color DriveCol = new Color(1f, 0.55f, 0.10f, 1f);   // Nộ (CAM — team chốt tuần 3)
        private static readonly Color ManaP1Col = new Color(0.95f, 0.25f, 0.30f, 1f);   // Mana P1 (ĐỎ — màu phe)
        private static readonly Color ManaP2Col = new Color(0.25f, 0.58f, 0.98f, 1f);   // Mana P2 (XANH — màu phe)
        private static readonly Color ManaTrackP1 = new Color(0.24f, 0.08f, 0.09f, 0.95f);
        private static readonly Color ManaTrackP2 = new Color(0.06f, 0.13f, 0.24f, 0.95f);
        private static readonly Color DriveTrack = new Color(0.24f, 0.14f, 0.04f, 0.95f); // track nộ (cam sậm)
        private static readonly Color ComboCol = new Color(0.97f, 0.32f, 0.55f, 1f);// số combo (hồng)
        private static readonly Color BarBack  = new Color(0f, 0f, 0f, 0.55f);
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
            BuildTopCluster(hud.transform, true, ch, maxLives, maxSegments);
            BuildTopCluster(hud.transform, false, ch, maxLives, maxSegments);
            BuildCombo(hud.transform, true, ch);
            BuildCombo(hud.transform, false, ch);
            // v10.1 ĐỔI CHỖ mana ↔ nộ: mana 4 ô nằm trong cụm trên (MakeTopInfo), nộ xuống góc dưới.
            BuildBottomRage(hud.transform, true, ch);
            BuildBottomRage(hud.transform, false, ch);
            return hud;
        }

        // ───────────────────── Timer (trên-giữa) ─────────────────────

        private static void BuildTimer(Transform parent, FloatEventChannelSO timerCh)
        {
            // Trận 180-600s (mốc team tuần 3) → hiện M:SS thay vì giây trần; panel nới cho "10:00".
            var panel = UIKit.NewPanel("TimerPanel", parent, new Vector2(0.5f, 1f), new Vector2(0f, -14f),
                                       new Vector2(210f, 96f), new Color(0f, 0f, 0f, 0.55f));
            var label = UIKit.AddLabel(panel.transform, "3:00", 56, FontStyle.Bold, TextAnchor.MiddleCenter);
            UIKit.StretchFull(label.gameObject);

            var view = panel.AddComponent<TimerView>();
            var so = new SerializedObject(view);
            UIKit.SetObj(so, "channel", timerCh);
            UIKit.SetObj(so, "label", label);
            UIKit.SetBool(so, "showAsMinutes", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
        }

        // ───────────────────── Cụm TRÊN (Vitality + Drive + Portrait + Lives) ─────────────────────

        private static void BuildTopCluster(Transform parent, bool isP1, UIChannels.Bundle ch, int maxLives,
                                            int maxSegments)
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
            var info = MakeTopInfo($"Info_{side}", root.transform, isP1, ch, maxLives, maxSegments);

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
                                              int maxLives, int maxSegments)
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

            // Mana 4 ô MÀU PHE — đổi chỗ với nộ (v10.1): dày bằng thanh % (32), 80% bề ngang.
            BuildTopMana(col.transform, isP1, ch, maxSegments);

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

        // ───────────────────── Mana 4 ô — cụm TRÊN (đổi chỗ với nộ, v10.1) ─────────────────────

        /// <summary>
        /// Dãy ô mana trong CỤM TRÊN, ngay dưới thanh %: dày bằng thanh % (32), chiếm 80% bề ngang dồn về
        /// phía người chơi, chia đều <paramref name="maxSegments"/> ô parallelogram MÀU PHE (P1 đỏ/P2 xanh)
        /// + glow đầy dãy + <see cref="ManaSegmentView"/> (reset 0 đầu trận).
        /// </summary>
        private static void BuildTopMana(Transform parent, bool isP1, UIChannels.Bundle ch, int maxSegments)
        {
            float slant = SkewFor(isP1);
            // wrapper full-width (VLG expand) — dãy ô thật nằm bên trong 80%, dồn về phía người chơi.
            var wrapper = UIKit.NewUI("SuperArt_Mana", parent);
            UIKit.SetLayoutHeight(wrapper, 32f);

            var area = UIKit.NewUI("Cells", wrapper.transform);
            var rt = area.GetComponent<RectTransform>();
            if (isP1) { rt.anchorMin = Vector2.zero;             rt.anchorMax = new Vector2(0.80f, 1f); }
            else      { rt.anchorMin = new Vector2(0.20f, 0f);   rt.anchorMax = Vector2.one; }
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var hlg = area.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f; hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

            Color fillCol = isP1 ? ManaP1Col : ManaP2Col;
            Color trackCol = isP1 ? ManaTrackP1 : ManaTrackP2;

            int n = Mathf.Max(1, maxSegments);
            var fills = new List<Image>(n);
            for (int i = 0; i < n; i++)
            {
                var cell = UIKit.NewUI($"Cell_{i}", area.transform);
                Sharpen(UIKit.AddImage(cell, trackCol));
                AddSkew(cell, slant);
                var fillGo = UIKit.NewUI("Fill", cell.transform);
                UIKit.StretchFull(fillGo);
                var fillImg = UIKit.AddImage(fillGo, fillCol);
                Sharpen(fillImg);
                AddSkew(fillGo, slant);
                fills.Add(fillImg);
            }

            // FX đầy ô: đủ n ô mana → vầng glow màu phe thở sau cả dãy + các ô sáng trắng theo nhịp.
            var glowImg = MakeFullGlow(area.transform, fillCol, slant);
            var glowFx = area.AddComponent<BarFullGlowFX>();
            var soG = new SerializedObject(glowFx);
            UIKit.SetObj(soG, "intChannel", ch.ManaSeg(isP1));
            UIKit.SetInt(soG, "intFullAt", n);
            UIKit.SetObj(soG, "glowImage", glowImg);
            UIKit.SetObjArray(soG, "tintImages", fills.ToArray());
            soG.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(glowFx);

            // Mirror P2: ManaSegmentView đầy cells[0] trước — P2 phải đầy từ phía portrait (PHẢI) vào
            // trong nên đảo thứ tự mảng (v10.4); thứ tự GameObject trên màn giữ nguyên.
            if (!isP1) fills.Reverse();

            var view = area.AddComponent<ManaSegmentView>();
            var so = new SerializedObject(view);
            UIKit.SetObj(so, "segmentsChannel", ch.ManaSeg(isP1));
            UIKit.SetObjArray(so, "cells", fills.ToArray());
            // Reset đầu trận: 0 ô — không giữ trạng thái placeholder/trận trước (bài học "đầy giả").
            UIKit.SetBool(so, "resetOnEnable", true);
            UIKit.SetInt(so, "initialSegments", 0);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
        }

        // ───────────────────── Lives ─────────────────────

        private static void BuildLivesRow(Transform parent, bool isP1, UIChannels.Bundle ch, int maxLives)
        {
            var row = UIKit.NewUI("LivesRow", parent);
            UIKit.SetLayoutHeight(row, 26f);
            UIKit.AddHorizontal(row, 6f, controlW: false, expandW: false,
                                isP1 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);

            // Ô mạng = TRÁI TIM (v10.0 — user đổi từ thanh nghiêng): sprite tim tự vẽ / art "HUD_Heart",
            // tint vàng LifeCol, KHÔNG skew (tim nghiêng nhìn méo).
            var heart = EnsureHeartSprite();
            var icons = new List<Image>(maxLives);
            for (int i = 0; i < maxLives; i++)
            {
                var ico = UIKit.NewUI($"Life_{i}", row.transform);
                var img = UIKit.AddImage(ico, LifeCol);
                if (heart != null) { img.sprite = heart; img.type = Image.Type.Simple; img.preserveAspect = true; }
                var le = ico.AddComponent<LayoutElement>();
                le.preferredWidth = 26f; le.preferredHeight = 24f; le.minWidth = 26f; le.minHeight = 24f;
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

        // ───────────────────── Nộ (dưới-góc — đổi chỗ với mana, v10.1) ─────────────────────

        /// <summary>
        /// Thanh NỘ ở góc dưới (vị trí cũ của dãy mana) — DỰNG NHƯ Ô MANA (v10.3, user muốn "sáng kiểu
        /// mana"): 2 Ô RỜI parallelogram sắc cạnh có khe hở, mỗi ô là 1 <see cref="ResourceBarView"/> nhận
        /// KHOẢNG giá trị (ô đầu 0–0.5, ô sau 0.5–1 → đầy lần lượt từ phía người chơi) + glow mềm sau cả
        /// dãy khi full. Trước đó là 1 bar liền → glow bọc quanh nhìn thành khối nhòe, không giống mana.
        /// </summary>
        private static void BuildBottomRage(Transform parent, bool isP1, UIChannels.Bundle ch)
        {
            string side = isP1 ? "P1" : "P2";
            float slant = SkewFor(isP1);
            Vector2 anchor = new Vector2(isP1 ? 0f : 1f, 0f);
            Vector2 pos = new Vector2(isP1 ? 28f : -28f, 28f);
            var root = Container($"Drive_Rage_{side}", parent, anchor, pos, new Vector2(340f, 22f));
            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f; hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

            const int n = 2;
            var fills = new Image[n];
            for (int j = 0; j < n; j++) // j = thứ tự TRÁI→PHẢI trên màn (thứ tự con của HLG).
            {
                var cell = UIKit.NewUI($"Cell_{j}", root.transform);
                Sharpen(UIKit.AddImage(cell, DriveTrack));
                AddSkew(cell, slant);
                var fillGo = UIKit.NewUI("Fill", cell.transform);
                UIKit.StretchFull(fillGo);
                var fillImg = UIKit.AddImage(fillGo, DriveCol);
                fillImg.type = Image.Type.Filled;
                // Filled BẮT BUỘC có sprite — UISprite mặc định BO GÓC + viền mềm làm ô sáng thành "viên
                // thuốc" nhòe (v10.4): thay bằng quad trắng sắc cạnh tự sinh → sáng kín khung như ô mana.
                var solid = EnsureSolidSprite();
                if (solid != null) fillImg.sprite = solid;
                fillImg.fillMethod = Image.FillMethod.Horizontal;
                fillImg.fillOrigin = (int)(isP1 ? Image.OriginHorizontal.Left : Image.OriginHorizontal.Right);
                fillImg.fillAmount = 0f;
                AddSkew(fillGo, slant);
                fills[j] = fillImg;

                // Ô nào đầy trước: P1 fill từ trái vào (ô trái = 0–0.5); P2 từ phải vào (ô phải = 0–0.5).
                float lo = (isP1 ? j : n - 1 - j) * (1f / n);
                var view = fillGo.AddComponent<ResourceBarView>();
                var so = new SerializedObject(view);
                UIKit.SetObj(so, "channel", ch.Rage(isP1));
                UIKit.SetObj(so, "fillImage", fillImg);
                UIKit.SetBool(so, "normalizeRawValue", false);
                UIKit.SetFloat(so, "maxRawValue", 1f);
                UIKit.SetFloat(so, "rangeStart", lo);
                UIKit.SetFloat(so, "rangeEnd", lo + 1f / n);
                // Reset đầu trận: HUD bật lại là fill về 0 — không giữ giá trị trận trước.
                UIKit.SetBool(so, "resetOnEnable", true);
                UIKit.SetFloat(so, "initialFill", 0f);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(view);
            }

            // FX đầy thanh: nộ = 1 → glow mềm cam thở sau CẢ DÃY + 2 ô sáng trắng theo nhịp (y hệt mana).
            var glowImg = MakeFullGlow(root.transform, DriveCol, slant);
            var glowFx = root.AddComponent<BarFullGlowFX>();
            var soG = new SerializedObject(glowFx);
            UIKit.SetObj(soG, "floatChannel", ch.Rage(isP1));
            UIKit.SetFloat(soG, "floatFullAt", 1f);
            UIKit.SetObj(soG, "glowImage", glowImg);
            UIKit.SetObjArray(soG, "tintImages", fills);
            soG.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(glowFx);
        }

        // ───────────────────── helpers ─────────────────────

        /// <summary>
        /// Sprite TRÁI TIM cho ô mạng (v10.0): ưu tiên art slot "HUD_Heart" (thả PNG vào Art/UI là dùng
        /// ảnh thật, không cần sửa code); chưa có → TỰ VẼ tim trắng 64×64 bằng đường cong ẩn
        /// (x²+y²−1)³ − x²y³ ≤ 0, supersample 3×3 cho mép mượt, lưu PNG vào Art/UI rồi import như art
        /// thường (chạy lại không vẽ lại — file đã tồn tại thì FindSprite trả ngay). Tim TRẮNG để tint
        /// màu qua Image.color.
        /// </summary>
        private static Sprite EnsureHeartSprite()
        {
            var sp = UILobbyBuilder.FindSprite("HUD_Heart");
            if (sp != null) return sp;

            const int size = 64;
            const int ss = 3; // supersample mỗi chiều.
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                float cover = 0f;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                {
                    // Map pixel → hệ tọa độ tim: x ∈ [−1.3, 1.3]; y lệch xuống (tâm 0.42) vì lobe
                    // trên của tim cao ~1.3 còn mũi dưới chỉ −1.
                    float x = ((px + (sx + 0.5f) / ss) / size - 0.5f) * 2.6f;
                    float y = ((py + (sy + 0.5f) / ss) / size - 0.42f) * 2.6f;
                    float a = x * x + y * y - 1f;
                    if (a * a * a - x * x * y * y * y <= 0f) cover += 1f;
                }
                tex.SetPixel(px, py, new Color(1f, 1f, 1f, cover / (ss * ss)));
            }
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            System.IO.File.WriteAllBytes(
                System.IO.Path.Combine(Application.dataPath, "_Project/Art/UI/HUD_Heart.png"), png);
            AssetDatabase.ImportAsset("Assets/_Project/Art/UI/HUD_Heart.png");
            return UILobbyBuilder.FindSprite("HUD_Heart"); // FindSprite ép import Single + alpha chuẩn.
        }

        /// <summary>
        /// Vầng glow "đầy thanh" cho <see cref="BarFullGlowFX"/>: Image sprite GLOW MỀM (lõi đặc, tan dần
        /// ra mép — <see cref="EnsureGlowSprite"/>) cùng dáng nghiêng, loe quanh thanh, alpha 0 sẵn (FX tự
        /// thở khi đầy). v10.2: trước đây là quad ĐẶC → trên thanh nộ liền khối nhìn như "viên gạch 2 tông"
        /// (dãy mana có khe hở ô nên che được khuyết điểm này). SetAsFirstSibling → vẽ DƯỚI ô/fill;
        /// ignoreLayout → HLG của dãy ô mana không cuốn nó vào flow.
        /// </summary>
        private static Image MakeFullGlow(Transform parent, Color barColor, float slant)
        {
            var go = UIKit.NewUI("FullGlow", parent);
            go.transform.SetAsFirstSibling();
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            UIKit.StretchFull(go);
            var rt = go.GetComponent<RectTransform>();
            rt.offsetMin = new Vector2(-16f, -12f); // loe rộng hơn quad đặc cũ (±9/±6) — mép đằng nào cũng tan.
            rt.offsetMax = new Vector2(16f, 12f);
            Color glowCol = Color.Lerp(barColor, Color.white, 0.45f); // sáng hơn màu thanh — nhìn như phát quang.
            glowCol.a = 0f;
            var img = UIKit.AddImage(go, glowCol);
            var soft = EnsureGlowSprite();
            if (soft != null) { img.sprite = soft; img.type = Image.Type.Simple; }
            AddSkew(go, slant);
            return img;
        }

        /// <summary>
        /// Sprite TRẮNG ĐẶC SẮC CẠNH 8×8 (v10.4) cho Image Type=Filled (Filled bắt buộc có sprite; UISprite
        /// mặc định bo góc + viền mềm → ô sáng thành viên thuốc). Ưu tiên art slot "HUD_White" nếu có.
        /// </summary>
        private static Sprite EnsureSolidSprite()
        {
            var sp = UILobbyBuilder.FindSprite("HUD_White");
            if (sp != null) return sp;

            const int size = 8;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            System.IO.File.WriteAllBytes(
                System.IO.Path.Combine(Application.dataPath, "_Project/Art/UI/HUD_White.png"), png);
            AssetDatabase.ImportAsset("Assets/_Project/Art/UI/HUD_White.png");
            return UILobbyBuilder.FindSprite("HUD_White");
        }

        /// <summary>
        /// Sprite GLOW MỀM dùng chung (v10.2): trắng 128×128, lõi đặc ~30% giữa rồi alpha tan dần ra mép
        /// theo 2 trục (nhân falloff X × Y, mũ 1.6 cho đuôi mượt) — kéo giãn theo thanh vẫn ra vệt sáng.
        /// Ưu tiên art slot "HUD_GlowSoft" nếu bạn thả ảnh riêng; tự vẽ 1 lần rồi tái dùng như HUD_Heart.
        /// </summary>
        private static Sprite EnsureGlowSprite()
        {
            var sp = UILobbyBuilder.FindSprite("HUD_GlowSoft");
            if (sp != null) return sp;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                float dx = Mathf.Abs(px / (size - 1f) - 0.5f) * 2f; // 0 tâm → 1 mép.
                float dy = Mathf.Abs(py / (size - 1f) - 0.5f) * 2f;
                float ax = Mathf.Clamp01((1f - dx) / 0.7f);         // 30% lõi đặc, 70% rìa tan.
                float ay = Mathf.Clamp01((1f - dy) / 0.7f);
                tex.SetPixel(px, py, new Color(1f, 1f, 1f, Mathf.Pow(ax * ay, 1.6f)));
            }
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            System.IO.File.WriteAllBytes(
                System.IO.Path.Combine(Application.dataPath, "_Project/Art/UI/HUD_GlowSoft.png"), png);
            AssetDatabase.ImportAsset("Assets/_Project/Art/UI/HUD_GlowSoft.png");
            return UILobbyBuilder.FindSprite("HUD_GlowSoft"); // FindSprite ép import Single + alpha chuẩn.
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
