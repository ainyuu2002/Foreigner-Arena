using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using FA.GameFlow;
using FA.UI;

namespace FA.EditorTools
{
    /// <summary>
    /// PHẦN LOBBY (P12 tuần 2) của bộ tool dựng UI production (xem <see cref="UIProductionMenu"/>). Dựng
    /// scene LOBBY hoàn chỉnh = Main Menu + toàn bộ flow vào trận:
    ///   • Canvas <c>MainMenu</c> (sorting cao nhất, che CharSelect bên dưới): title + START/QUIT.
    ///     START ẩn MainMenu (persistent listener SetActive(false)) → lộ CharSelect mà MatchManager đã bật.
    ///     QUIT = <see cref="QuitButton"/> (FA.UI).
    ///   • GameObject <c>__GameFlowSystems</c>: <see cref="MatchManager"/> + <see cref="PlayerSpawner"/> +
    ///     <see cref="MatchTimer"/> + <see cref="WinCondition"/> wire đủ kênh/panel/registry — pattern PROVEN
    ///     của GameFlowHarnessSystems (P12 harness): SerializedObject + SetDirty (kẻo null lúc Play).
    ///
    /// Flow runtime (code sẵn có, KHÔNG sửa): MatchManager Start→CharSelect; chọn 2 nhân vật → StageSelect;
    /// chọn map → load scene map ADDITIVE + spawn 2 player theo lựa chọn + bật HUD → đánh; hết trận → End
    /// (Rematch/Back). EDITOR-ONLY; chỉ dựng GameObject + wire, KHÔNG sửa code gameplay.
    /// </summary>
    internal static class UILobbyBuilder
    {
        public const string MainMenuName = "MainMenu";
        public const string SystemsName = "__GameFlowSystems";

        /// <summary>4 art slot của Main Menu — thả PNG đúng tên vào Assets là rebuild tự nhận.</summary>
        public static readonly string[] ArtSlots =
            { "MainMenu_BG", "MainMenu_Title", "MainMenu_BtnStart", "MainMenu_BtnSettings", "MainMenu_BtnQuit",
              "CharSelect_BG", "CharSelect_Btn", "CharSelect_Banner",
              "StageSelect_BG", "StageSelect_Random" };

        /// <summary>Trạng thái art slot (cho log build): "tên → CÓ/chưa".</summary>
        public static System.Collections.Generic.List<string> ArtSlotStatus()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var s in ArtSlots)
                list.Add($"{s} → {(FindSprite(s) != null ? "CÓ (đang dùng)" : "chưa có (dùng code-only)")}");
            return list;
        }

        private static readonly Color MenuBack = new Color(0.05f, 0.06f, 0.09f, 1f);
        private static readonly Color TitleCol = new Color(0.95f, 0.30f, 0.35f, 1f);

        // ───────────────────── Main Menu (canvas trên cùng, FX tầng 1 code-only) ─────────────────────

        private static readonly Color HeatCol   = new Color(0.95f, 0.30f, 0.08f, 1f);  // lửa cam-đỏ
        private static readonly Color EmberCol  = new Color(0.80f, 0.12f, 0.05f, 1f);  // than đỏ sậm
        private static readonly Color GlowStart = new Color(0.35f, 0.75f, 1f, 1f);     // glow nút START (điện)
        private static readonly Color GlowSet   = new Color(0.75f, 0.78f, 0.90f, 1f);  // glow nút SETTINGS (thép xám)
        private static readonly Color GlowQuit  = new Color(1f, 0.45f, 0.15f, 1f);     // glow nút QUIT (lửa)

        /// <summary>
        /// Dựng Main Menu + FX. KIẾN TRÚC 3 TẦNG (Screen Space CAMERA — Overlay luôn đè world nên sét/lửa
        /// world sẽ vô hình nếu giữ Overlay):
        ///   • Canvas "MenuBG" (sorting 250): ảnh nền (chặn raycast + che CharSelect ở 200).
        ///   • FX WORLD (sorting 255-256): ProceduralLightning tại orb + 2 FireSparks chân cột lửa —
        ///     chen GIỮA nền và nút.
        ///   • Canvas "MainMenu" (sorting 300): title/nút/TransitionOverlay (v9.7: bỏ lớp chớp lóe).
        /// StartGame ẩn content + alsoHide (BG canvas + FX — chúng nằm ngoài canvas menu).
        /// Lưu ý prefab MainMenu chỉ chứa canvas menu — scene Lobby mới là bản đầy đủ 3 tầng.
        /// </summary>
        public static GameObject BuildMainMenu()
        {
            // ── Tầng 1: canvas NỀN riêng (sorting 250) ──
            var bgCanvas = UIKit.NewCanvas("MenuBG", 250);
            ToCameraSpace(bgCanvas);

            // Nền đặc + CHẶN raycast → không click xuyên xuống CharSelect khi menu đang mở.
            // ART SLOT "MainMenu_BG": có ảnh nền thật (tường + lửa + orb) → dùng thay nền code-only.
            var bg = UIKit.NewUI("BG", bgCanvas.transform);
            UIKit.StretchFull(bg);
            var bgSprite = FindSprite("MainMenu_BG");
            if (bgSprite != null)
            {
                var bgImg = bg.AddComponent<Image>();
                bgImg.sprite = bgSprite; bgImg.type = Image.Type.Simple;
                bgImg.color = Color.white; bgImg.raycastTarget = true;
            }
            else
            {
                UIKit.AddImage(bg, MenuBack, raycastTarget: true);
                BuildHeatLayers(bgCanvas.transform); // lửa giả code-only khi CHƯA có art.
            }

            // ── Tầng 2: FX WORLD (sét + tia lửa) — camera menu ortho size 8 → đáy y=-7, mép x≈±14 ──
            var lightningGo = new GameObject("MenuFX_LightningCore");
            Undo.RegisterCreatedObjectUndo(lightningGo, "Create MenuFX");
            lightningGo.transform.position = new Vector3(0f, 1.4f, 0f); // tâm nexus sét của BG art (hành lang mới: giữa màn, hơi cao hơn tâm).
            lightningGo.AddComponent<ProceduralLightning>(); // syncFlash để trống (v9.7: bỏ chớp màn hình).

            // BG hành lang mới: 2 cột lửa ôm SÁT mép trái/phải (x≈±12.8, bề ngang ~3 world) thay vì
            // 2 góc trong (±11, rộng 5) của BG cũ.
            var sparksL = MakeFireSparks("MenuFX_FireSparks_L", -12.8f);
            var sparksR = MakeFireSparks("MenuFX_FireSparks_R", 12.8f);

            // ── Tầng 3: canvas MENU (sorting 300) ──
            var canvas = UIKit.NewCanvas(MainMenuName, 300);
            ToCameraSpace(canvas);

            var content = UIKit.NewUI("Content", canvas.transform);
            UIKit.StretchFull(content);

            var col = UIKit.NewUI("Column", content.transform);
            var rt = col.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(760f, 640f);
            UIKit.AddVertical(col, 26f, 0, TextAnchor.MiddleCenter);

            BuildTitle(content.transform, col.transform);

            // ART SLOT "MainMenu_BtnStart"/"MainMenu_BtnQuit": khung nút vẽ thật (điện xanh / lửa tối) —
            // sprite thay nền màu, label code vẫn đè lên (đổi chữ không cần sửa ảnh).
            var startBtn = UIKit.MakeButton(col.transform, "START", UIKit.ColBtn, 340f, 74f, 30);
            ApplyButtonArt(startBtn, "MainMenu_BtnStart");
            AddButtonFX(startBtn, GlowStart);
            var settingsBtn = UIKit.MakeButton(col.transform, "SETTINGS", UIKit.ColPanelLight, 340f, 74f, 30);
            ApplyButtonArt(settingsBtn, "MainMenu_BtnSettings");
            AddButtonFX(settingsBtn, GlowSet);
            var quitBtn = UIKit.MakeButton(col.transform, "QUIT", UIKit.ColBtnAccent, 340f, 74f, 30);
            ApplyButtonArt(quitBtn, "MainMenu_BtnQuit");
            AddButtonFX(quitBtn, GlowQuit);

            // (v9.7) BỎ FlashLayer chớp lóe full-screen + syncFlash — user chê giật; tia sét world
            // (ProceduralLightning) + tia lửa GIỮ NGUYÊN, chỉ khung cảnh không bừng nháy nữa.

            // ── SettingsPanel (bản đầy đủ — NGOÀI Content để dim đè cả cột nút; DƯỚI TransitionOverlay) ──
            var settingsPanel = UISettingsBuilder.BuildSettingsPanel(canvas.transform, inMatch: false);
            var settingsView = settingsPanel.GetComponent<SettingsPanelView>();

            // ── TransitionOverlay: phủ chuyển cảnh (trên cùng, NGOÀI Content để sống sau khi ẩn menu) ──
            var overlayGo = UIKit.NewUI("TransitionOverlay", canvas.transform);
            UIKit.StretchFull(overlayGo);
            var overlayImg = overlayGo.AddComponent<Image>();
            overlayImg.color = new Color(1f, 1f, 1f, 0f);
            overlayImg.raycastTarget = false;                 // raycast chặn bằng CanvasGroup khi đang chuyển.
            var group = overlayGo.AddComponent<CanvasGroup>();
            group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;

            var fx = canvas.AddComponent<MenuTransitionFX>();
            var soF = new SerializedObject(fx);
            UIKit.SetObj(soF, "overlay", overlayImg);
            UIKit.SetObj(soF, "overlayGroup", group);
            UIKit.SetObj(soF, "content", content);
            UIKit.SetObj(soF, "shakeTarget", col.GetComponent<RectTransform>());
            // BG canvas + FX world nằm NGOÀI canvas menu → StartGame phải ẩn kèm (kẻo che CharSelect ở 200).
            UIKit.SetObjArray(soF, "alsoHide", new Object[] { bgCanvas, lightningGo, sparksL, sparksR });
            soF.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fx);

            // Wire nút → transition (persistent listener, serialize vào scene/prefab — cùng root nên an toàn).
            UnityEventTools.AddPersistentListener(startBtn.onClick, new UnityAction(fx.StartGame));
            UnityEventTools.AddPersistentListener(settingsBtn.onClick, new UnityAction(settingsView.Open));
            UnityEventTools.AddPersistentListener(quitBtn.onClick, new UnityAction(fx.QuitGame));
            EditorUtility.SetDirty(startBtn);
            EditorUtility.SetDirty(settingsBtn);
            EditorUtility.SetDirty(quitBtn);
            return canvas;
        }

        /// <summary>
        /// Chuyển canvas sang Screen Space CAMERA — bắt buộc để FX world (LineRenderer/Particle) chen được
        /// giữa các tầng UI (Overlay LUÔN đè mọi renderer world). Sorting giữa canvas ↔ renderer world khi đó
        /// tính theo sortingLayer/sortingOrder chung của 2D renderer.
        /// </summary>
        public static void ToCameraSpace(GameObject canvasGo)
        {
            var c = canvasGo.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = Camera.main;
            c.planeDistance = 50f;
            EditorUtility.SetDirty(c);
        }

        /// <summary>Tạo 1 hệ tia lửa ở chân cột lửa (x = ±11 với camera ortho size 8, đáy màn hình y=-7).</summary>
        private static GameObject MakeFireSparks(string name, float x)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create MenuFX");
            go.transform.position = new Vector3(x, -7.6f, 0f); // hơi dưới mép — tia "mọc" từ ngoài vào.
            var fs = go.AddComponent<FireSparks>();            // RequireComponent tự thêm ParticleSystem.
            var so = new SerializedObject(fs);
            UIKit.SetFloat(so, "emitWidth", 3f); // khớp bề ngang cột lửa BG hành lang.
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fs);
            return go;
        }

        /// <summary>Lớp nền "nhiệt" — gradient cam-đỏ tan dần lên + pulse lệch pha (lửa âm ỉ đáy/2 bên).</summary>
        private static void BuildHeatLayers(Transform parent)
        {
            MakeHeat(parent, "Heat_Bottom", new Vector2(0f, 0f), new Vector2(1f, 0.42f), HeatCol,
                     alphaMin: 0.20f, alphaMax: 0.38f, speed: 0.7f, phase: 0f);
            MakeHeat(parent, "Heat_Left", new Vector2(0f, 0f), new Vector2(0.18f, 0.85f), EmberCol,
                     alphaMin: 0.10f, alphaMax: 0.22f, speed: 0.5f, phase: 1.7f);
            MakeHeat(parent, "Heat_Right", new Vector2(0.82f, 0f), new Vector2(1f, 0.85f), EmberCol,
                     alphaMin: 0.10f, alphaMax: 0.22f, speed: 0.55f, phase: 3.9f);
        }

        private static void MakeHeat(Transform parent, string name, Vector2 aMin, Vector2 aMax, Color col,
                                     float alphaMin, float alphaMax, float speed, float phase)
        {
            var go = UIKit.NewUI(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = col; img.raycastTarget = false;

            var grad = go.AddComponent<UIGradient>();
            var soG = new SerializedObject(grad);
            var top = soG.FindProperty("topColor"); if (top != null) top.colorValue = new Color(1f, 1f, 1f, 0f);
            var bot = soG.FindProperty("bottomColor"); if (bot != null) bot.colorValue = Color.white;
            soG.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(grad);

            AddPulse(go, img, alphaMin, alphaMax, speed, phase, col, col, 0f);
        }

        /// <summary>
        /// Logo — ART SLOT "MainMenu_Title": banner NEO TUYỆT ĐỐI trên-giữa màn hình (vùng trần TỐI của BG)
        /// + lớp LÓT ĐEN mờ sau chữ. Trước đây title nằm trong VLG cột giữa → rơi đúng vùng orb sét TRẮNG
        /// của BG art, chữ đỏ nhạt chìm nghỉm ("không thấy chữ"). Neo tuyệt đối = không phụ thuộc layout,
        /// lót tối = tương phản chắc trên MỌI nền. CHƯA có art → chữ code-only glow trong cột (như cũ).
        /// </summary>
        private static void BuildTitle(Transform contentRoot, Transform parent)
        {
            var titleSprite = FindSprite("MainMenu_Title");
            if (titleSprite != null)
            {
                var banner = UIKit.NewUI("Title_Art", contentRoot);
                var brt = banner.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0.5f, 1f); brt.anchorMax = new Vector2(0.5f, 1f);
                brt.pivot = new Vector2(0.5f, 1f);
                brt.anchoredPosition = new Vector2(0f, -30f);
                brt.sizeDelta = new Vector2(950f, 240f);

                // Lót đen mờ vùng CHỮ (nửa dưới banner — lửa phía trên vẫn "thò" ra ngoài, đẹp kiểu SF6).
                var backing = UIKit.NewUI("Backing", banner.transform);
                var krt = backing.GetComponent<RectTransform>();
                krt.anchorMin = new Vector2(0f, 0f); krt.anchorMax = new Vector2(1f, 0.55f);
                krt.offsetMin = new Vector2(-30f, -10f); krt.offsetMax = new Vector2(30f, 0f);
                UIKit.AddImage(backing, new Color(0f, 0f, 0f, 0.45f));

                var artGo = UIKit.NewUI("Art", banner.transform);
                UIKit.StretchFull(artGo);
                var img = artGo.AddComponent<Image>();
                img.sprite = titleSprite; img.preserveAspect = true; img.raycastTarget = false;
                AddPulse(artGo, img, 0.94f, 1f, 1.4f, 0.8f, Color.white, Color.white, 0.015f);

                var subA = UIKit.AddLabel(parent, "2v2 ARENA FIGHTER", 26, FontStyle.Bold,
                                          TextAnchor.MiddleCenter, UIKit.ColTxtDim);
                UIKit.SetLayoutHeight(subA.gameObject, 60f);
                return;
            }

            var holder = UIKit.NewUI("Title", parent);
            UIKit.SetLayoutHeight(holder, 120f);

            // Glow sau (bản sao chữ, to hơn nhẹ, alpha pulse) — vẽ TRƯỚC → nằm dưới title.
            var glow = UIKit.AddLabel(holder.transform, "FOREIGNER ARENA", 88, FontStyle.Bold,
                                      TextAnchor.MiddleCenter, new Color(1f, 0.45f, 0.10f, 0.35f));
            UIKit.StretchFull(glow.gameObject);
            glow.gameObject.transform.localScale = new Vector3(1.03f, 1.06f, 1f);
            AddPulse(glow.gameObject, glow, 0.15f, 0.45f, 1.6f, 0.8f,
                     new Color(1f, 0.45f, 0.10f, 1f), new Color(1f, 0.20f, 0.05f, 1f), 0.02f);

            var title = UIKit.AddLabel(holder.transform, "FOREIGNER ARENA", 88, FontStyle.Bold,
                                       TextAnchor.MiddleCenter, TitleCol);
            UIKit.StretchFull(title.gameObject);
            AddPulse(title.gameObject, title, 0.92f, 1f, 1.6f, 0.8f,
                     TitleCol, new Color(1f, 0.42f, 0.18f, 1f), 0f);

            var sub = UIKit.AddLabel(parent, "2v2 ARENA FIGHTER", 26, FontStyle.Bold,
                                     TextAnchor.MiddleCenter, UIKit.ColTxtDim);
            UIKit.SetLayoutHeight(sub.gameObject, 60f);
        }

        /// <summary>Gắn glow + click-flash + ButtonGlowFX cho 1 nút.</summary>
        private static void AddButtonFX(Button btn, Color glowCol)
        {
            // Glow phủ nút (con đầu → vẽ ngay trên nền nút, dưới label).
            var glowGo = UIKit.NewUI("Glow", btn.transform);
            UIKit.StretchFull(glowGo);
            var grt = glowGo.GetComponent<RectTransform>();
            grt.offsetMin = new Vector2(-7f, -7f); grt.offsetMax = new Vector2(7f, 7f); // viền loe ra ngoài.
            var glowImg = glowGo.AddComponent<Image>();
            glowImg.color = new Color(glowCol.r, glowCol.g, glowCol.b, 0.16f);
            glowImg.raycastTarget = false;
            glowGo.transform.SetAsFirstSibling();

            var flashGo = UIKit.NewUI("ClickFlash", btn.transform);
            UIKit.StretchFull(flashGo);
            var flashImg = flashGo.AddComponent<Image>();
            flashImg.color = new Color(1f, 1f, 1f, 0f);
            flashImg.raycastTarget = false;

            var fx = btn.gameObject.AddComponent<ButtonGlowFX>();
            var so = new SerializedObject(fx);
            UIKit.SetObj(so, "glow", glowImg);
            UIKit.SetObj(so, "clickFlash", flashImg);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fx);
        }

        /// <summary>Gắn UIPulse với đủ thông số qua SerializedObject.</summary>
        private static void AddPulse(GameObject go, Graphic target, float aMin, float aMax,
                                     float speed, float phase, Color colA, Color colB, float scaleAmp)
        {
            var p = go.AddComponent<UIPulse>();
            var so = new SerializedObject(p);
            UIKit.SetObj(so, "target", target);
            UIKit.SetFloat(so, "alphaMin", aMin);
            UIKit.SetFloat(so, "alphaMax", aMax);
            UIKit.SetFloat(so, "speed", speed);
            UIKit.SetFloat(so, "phase", phase);
            var ca = so.FindProperty("colorA"); if (ca != null) ca.colorValue = colA;
            var cb = so.FindProperty("colorB"); if (cb != null) cb.colorValue = colB;
            UIKit.SetFloat(so, "scaleAmplitude", scaleAmp);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(p);
        }

        // ───────────────────── GameFlow systems (wire flow vào trận) ─────────────────────

        /// <summary>
        /// Dựng __GameFlowSystems + wire — panel lấy theo TÊN con (CharSelectPanel/StageSelectPanel trong
        /// Menu_Screens; EndScreenPanel trong canvas Overlay riêng Menu_EndScreen — UIScreensBuilder đặt);
        /// HUD = canvas HUD_InMatch (tắt sẵn).
        /// </summary>
        public static void BuildSystems(UIChannels.Bundle ch, GameObject menuScreens, GameObject hud,
                                        GameObject endScreenRoot)
        {
            GameObject charSel = FindChild(menuScreens, "CharSelectPanel");
            GameObject stageSel = FindChild(menuScreens, "StageSelectPanel");
            GameObject endPanel = FindChild(endScreenRoot != null ? endScreenRoot : menuScreens,
                                            "EndScreenPanel");

            var root = new GameObject(SystemsName);
            Undo.RegisterCreatedObjectUndo(root, "Create GameFlow Systems");

            var timer = root.AddComponent<MatchTimer>();
            var win = root.AddComponent<WinCondition>();
            var spawner = root.AddComponent<PlayerSpawner>();
            var manager = root.AddComponent<MatchManager>();

            // MatchTimer — settings + kênh timer/timeup.
            var soT = new SerializedObject(timer);
            UIKit.SetObj(soT, "settings", ch.MatchSettings);
            UIKit.SetObj(soT, "onTimerChanged", ch.Timer);
            UIKit.SetObj(soT, "onTimeUp", ch.TimeUp);
            soT.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(timer); // BẮT BUỘC: flush ref vào scene khi SaveScene (bug đã gặp ở harness).

            // WinCondition — nghe OutOfLives/TimeUp, bắn MatchResult/MatchEnded.
            var soW = new SerializedObject(win);
            UIKit.SetObj(soW, "settings", ch.MatchSettings);
            UIKit.SetObj(soW, "onOutOfLives", ch.OutOfLives);
            UIKit.SetObj(soW, "onTimeUp", ch.TimeUp);
            UIKit.SetObj(soW, "onMatchResult", ch.MatchResult);
            UIKit.SetObj(soW, "onMatchEnded", ch.MatchEnded);
            soW.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(win);

            // PlayerSpawner — registry prefab variant + WinCondition để đăng ký 2 player spawn.
            var soS = new SerializedObject(spawner);
            UIKit.SetObj(soS, "characterRegistry", ch.CharacterRegistry);
            UIKit.SetObj(soS, "winCondition", win);
            soS.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spawner);

            // MatchManager — panels + systems + toàn bộ kênh nghe.
            var soM = new SerializedObject(manager);
            UIKit.SetObj(soM, "charSelectPanel", charSel);
            UIKit.SetObj(soM, "stageSelectPanel", stageSel);
            UIKit.SetObj(soM, "endScreenPanel", endPanel);
            UIKit.SetObj(soM, "matchHud", hud);
            UIKit.SetObj(soM, "playerSpawner", spawner);
            UIKit.SetObj(soM, "matchTimer", timer);
            UIKit.SetObj(soM, "stageRegistry", ch.StageRegistry);
            UIKit.SetObj(soM, "onCharSelectedP1", ch.CharSelP1);
            UIKit.SetObj(soM, "onCharSelectedP2", ch.CharSelP2);
            UIKit.SetObj(soM, "onStageSelected", ch.StageSel);
            UIKit.SetObj(soM, "onRematchPressed", ch.Rematch);
            UIKit.SetObj(soM, "onBackToCharSelect", ch.BackToCharSelect);
            UIKit.SetObj(soM, "onMatchEnded", ch.MatchEnded);
            UIKit.SetObj(soM, "onMatchStart", ch.MatchStart);   // khởi động ItemSpawner (hệ item) khi vào Match — đừng để rớt khi build lại Lobby.
            soM.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);

            // FighterIdentityView — tên + chân dung THẬT (registry) lên HUD. Đặt trên systems (LUÔN
            // active) vì HUD tắt lúc menu → component trong HUD sẽ miss event CharSelected.
            var identity = root.AddComponent<FA.UI.FighterIdentityView>();
            var soI = new SerializedObject(identity);
            UIKit.SetObj(soI, "charSelectedP1", ch.CharSelP1);
            UIKit.SetObj(soI, "charSelectedP2", ch.CharSelP2);
            UIKit.SetObj(soI, "registry", ch.CharacterRegistry);
            UIKit.SetObj(soI, "portraitP1", FindDeep<UnityEngine.UI.Image>(hud, "HUDTop_P1/Portrait_P1/Pic"));
            UIKit.SetObj(soI, "nameP1", FindDeep<UnityEngine.UI.Text>(hud, "HUDTop_P1/Info_P1/NameLabel"));
            UIKit.SetObj(soI, "portraitP2", FindDeep<UnityEngine.UI.Image>(hud, "HUDTop_P2/Portrait_P2/Pic"));
            UIKit.SetObj(soI, "nameP2", FindDeep<UnityEngine.UI.Text>(hud, "HUDTop_P2/Info_P2/NameLabel"));
            soI.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(identity);
        }

        /// <summary>Tìm component theo PATH con của root (transform.Find hỗ trợ path) — log rõ khi trượt
        /// thay vì wire null im lặng (bài học "scene chết im lặng").</summary>
        private static T FindDeep<T>(GameObject rootGo, string path) where T : Component
        {
            Transform t = rootGo != null ? rootGo.transform.Find(path) : null;
            T c = t != null ? t.GetComponent<T>() : null;
            if (c == null)
                Debug.LogWarning($"[UILobbyBuilder] Không tìm thấy {typeof(T).Name} tại '{path}' trong HUD — " +
                                 "FighterIdentityView sẽ thiếu ref này.");
            return c;
        }

        // ───────────────────── Kiểm tra Build Settings (map load được không) ─────────────────────

        /// <summary>
        /// LoadSceneAsync CHỈ load scene có trong Build Settings — kiểm scene của StageRegistry, trả danh
        /// sách tên THIẾU để menu log cảnh báo (không tự sửa Build Settings — tránh conflict với team).
        /// </summary>
        public static System.Collections.Generic.List<string> MissingBuildScenes(StageRegistrySO reg)
        {
            var missing = new System.Collections.Generic.List<string>();
            if (reg == null) return missing;

            for (int i = 0; i < reg.Count; i++)
            {
                string name = reg.GetSceneName(i);
                if (string.IsNullOrEmpty(name)) continue;
                bool found = false;
                foreach (var s in EditorBuildSettings.scenes)
                {
                    if (s.enabled && System.IO.Path.GetFileNameWithoutExtension(s.path) == name)
                    { found = true; break; }
                }
                if (!found) missing.Add(name);
            }
            return missing;
        }

        // ───────────────────── Art slots (tầng 2 — thả PNG đúng tên là tự nhận) ─────────────────────

        /// <summary>Áp sprite khung nút nếu có art slot; giữ label + FX code (hover/click vẫn chạy).</summary>
        internal static void ApplyButtonArt(Button btn, string spriteName) // internal: UIScreensBuilder dùng chung
        {
            var sp = FindSprite(spriteName);
            if (sp == null) return;
            var img = btn.GetComponent<Image>();
            img.sprite = sp; img.type = Image.Type.Simple; img.color = Color.white;
            EditorUtility.SetDirty(img);
        }

        /// <summary>
        /// Tìm Sprite theo TÊN file (pattern FindExact của UIChannels). PNG mới thả vào thường import kiểu
        /// Default (không phải Sprite) → tự chỉnh TextureImporter sang Sprite (2D and UI) rồi reimport —
        /// người dùng chỉ cần kéo-thả file, KHÔNG phải chỉnh import settings tay.
        /// </summary>
        internal static Sprite FindSprite(string assetName)
        {
            // Quét System.IO đường dẫn TUYỆT ĐỐI (v9 — xem UIKit.FindProjectFiles) các đuôi ảnh phổ biến.
            foreach (string ext in new[] { ".png", ".jpg", ".jpeg", ".psd" })
            {
                foreach (string file in UIKit.FindProjectFiles(assetName + ext))
                {
                    var sp = LoadOrFixSprite(file);
                    if (sp != null) return sp;
                }
            }
            foreach (var guid in AssetDatabase.FindAssets(assetName))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != assetName) continue;
                var sp = LoadOrFixSprite(path);
                if (sp != null) return sp;
            }
            return null;
        }

        /// <summary>
        /// Load Sprite + SỬA import settings nếu sai. ROOT-CAUSE vụ "mất chữ" title: PNG bị import
        /// spriteMode=MULTIPLE → Unity auto-slice thành nhiều sprite CON; LoadAssetAtPath&lt;Sprite&gt; trả
        /// sprite con đầu (1 mảnh lửa nhỏ) thay vì trọn ảnh → Image vẽ mảnh tí hin, tưởng ảnh hỏng.
        /// Ép textureType=Sprite + spriteImportMode=SINGLE (1 sprite trọn ảnh) + alphaIsTransparency.
        /// </summary>
        private static Sprite LoadOrFixSprite(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && (imp.textureType != TextureImporterType.Sprite
                                || imp.spriteImportMode != SpriteImportMode.Single
                                || !imp.alphaIsTransparency))
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static GameObject FindChild(GameObject parent, string name)
        {
            var t = parent != null ? parent.transform.Find(name) : null;
            if (t == null) Debug.LogWarning($"[UILobby] Không thấy panel '{name}' trong {parent?.name} — MatchManager sẽ thiếu panel này.");
            return t != null ? t.gameObject : null;
        }
    }
}
