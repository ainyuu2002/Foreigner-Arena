using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using FA.Core;
using FA.UI;
using FA.GameFlow;

namespace FA.EditorTools
{
    /// <summary>
    /// PHẦN CANVAS của tool "Build Test_Phase12 (GameFlow harness)" (xem <see cref="GameFlowHarnessSetup"/>).
    /// 1 chức năng: dựng Canvas Screen-Space-Overlay <c>__GameFlowHarnessUI</c> gồm:
    ///   • HUD (TimerView + 2 LivesView per-player) — bật khi Match.
    ///   • CharSelect panel (2 hàng nút char/người → IntButtonRaiser CharSelectedChannel_P1/P2).
    ///   • StageSelect panel (1 nút stage → IntButtonRaiser StageSelectedChannel).
    ///   • EndScreen panel (EndScreenView + nút Rematch/Back → VoidButtonRaiser).
    ///   • Debug panel (nút Kill/Damage/Combo/TimeUp → gọi method <see cref="GameFlowDebugPanel"/> qua onClick).
    ///
    /// KHÔNG tự vẽ widget bằng code: dùng uGUI Button/Text/Image + LayoutGroup (Unity-native §1.1), gắn các
    /// View/Raiser của FA.UI (P12). EDITOR-ONLY. Trả <see cref="Refs"/> cho bước wiring (MatchManager cần panel).
    /// </summary>
    internal static class GameFlowHarnessCanvas
    {
        public const string UiName = "__GameFlowHarnessUI";

        public sealed class Refs
        {
            public GameObject Canvas;
            public GameObject CharSelectPanel;
            public GameObject StageSelectPanel;
            public GameObject EndScreenPanel;
            public GameObject HudPanel;
            public GameFlowDebugPanel DebugPanel;   // MonoBehaviour runtime (FA.GameFlow) các nút gọi.
            public EndScreenView EndScreen;
            public TimerView Timer;
            public LivesView LivesP1, LivesP2;
        }

        private static readonly Color PanelBack = new Color(0f, 0f, 0f, 0.78f);
        private static readonly Color HudBack = new Color(0f, 0f, 0f, 0.35f);
        private static readonly Color BtnColor = new Color(0.18f, 0.34f, 0.55f, 1f);
        private static readonly Color DbgBtnColor = new Color(0.45f, 0.2f, 0.2f, 1f);
        private static readonly Color LifeColor = new Color(0.95f, 0.85f, 0.2f, 1f);
        private static readonly Color Txt = Color.white;

        public static Refs Build(GameFlowHarnessAssets.Bundle b)
        {
            var r = new Refs();
            var ui = new GameObject(UiName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(ui, "Create GameFlow Harness UI");
            r.Canvas = ui;

            var canvas = ui.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = ui.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            EnsureEventSystem();
            EnsureMainCamera();

            BuildHud(ui.transform, b, r);
            BuildCharSelect(ui.transform, b, r);
            BuildStageSelect(ui.transform, b, r);
            BuildEndScreen(ui.transform, b, r);
            BuildDebugPanel(ui.transform, b, r);
            return r;
        }

        // ───────────────────── HUD ─────────────────────

        private static void BuildHud(Transform parent, GameFlowHarnessAssets.Bundle b, Refs r)
        {
            var hud = NewPanel("HUD", parent, new Vector2(0.5f, 1f), new Vector2(0f, -16f),
                               new Vector2(900f, 90f), HudBack);
            r.HudPanel = hud;
            var hlg = hud.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 40f; hlg.padding = new RectOffset(20, 20, 10, 10);
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleCenter;

            // P1 lives (trái) | Timer (giữa) | P2 lives (phải).
            r.LivesP1 = BuildLivesView(hud.transform, "P1 Lives", b.LivesP1, b.MatchSettings);
            r.Timer = BuildTimerView(hud.transform, b.TimerChanged);
            r.LivesP2 = BuildLivesView(hud.transform, "P2 Lives", b.LivesP2, b.MatchSettings);
        }

        private static TimerView BuildTimerView(Transform parent, FloatEventChannelSO ch)
        {
            var go = NewUI("Timer", parent);
            var t = AddLabel(go.transform, "99", 48, FontStyle.Bold, TextAnchor.MiddleCenter);
            var view = go.AddComponent<TimerView>();
            var so = new SerializedObject(view);
            SetObj(so, "channel", ch);
            SetObj(so, "label", t);
            SetBool(so, "showAsMinutes", false);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view); // flush channel/label vào scene khi SaveScene (nếu không → null lúc Play).
            return view;
        }

        private static LivesView BuildLivesView(Transform parent, string title, IntEventChannelSO ch,
                                                MatchSettingsSO settings)
        {
            var col = NewUI($"LivesCol_{title}", parent);
            var vlg = col.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f; vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;

            AddLabel(col.transform, title, 18, FontStyle.Normal, TextAnchor.MiddleCenter);

            var row = NewUI("Icons", col.transform);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            int maxLives = settings != null ? Mathf.Clamp(settings.startingLives, 1, 5) : 3;
            var icons = new List<Image>(maxLives);
            for (int i = 0; i < maxLives; i++)
            {
                var ico = NewUI($"Life_{i}", row.transform);
                var img = AddImage(ico, LifeColor);
                var le = ico.AddComponent<LayoutElement>();
                le.preferredWidth = 22f; le.preferredHeight = 22f;
                le.minWidth = 22f; le.minHeight = 22f;
                icons.Add(img);
            }

            var view = col.AddComponent<LivesView>();
            var so = new SerializedObject(view);
            SetObj(so, "channel", ch);
            var arr = so.FindProperty("icons");
            arr.arraySize = icons.Count;
            for (int i = 0; i < icons.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = icons[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view); // flush channel + icons array vào scene khi SaveScene.
            return view;
        }

        // ───────────────────── CharSelect ─────────────────────

        private static void BuildCharSelect(Transform parent, GameFlowHarnessAssets.Bundle b, Refs r)
        {
            var panel = NewPanel("CharSelectPanel", parent, new Vector2(0.5f, 0.5f), Vector2.zero,
                                 new Vector2(900f, 520f), PanelBack);
            r.CharSelectPanel = panel;
            AddVerticalLayout(panel, 18f);
            AddLabel(panel.transform, "CHARACTER SELECT", 36, FontStyle.Bold, TextAnchor.MiddleCenter);
            AddLabel(panel.transform, "P1 chọn (hàng trên) + P2 chọn (hàng dưới) → vào Stage Select",
                     18, FontStyle.Normal, TextAnchor.MiddleCenter);

            int n = b.CharacterRegistry != null ? Mathf.Max(1, b.CharacterRegistry.Count) : 2;
            BuildSelectRow(panel.transform, "P1:", n, b.CharSelP1);
            BuildSelectRow(panel.transform, "P2:", n, b.CharSelP2);
        }

        private static void BuildStageSelect(Transform parent, GameFlowHarnessAssets.Bundle b, Refs r)
        {
            var panel = NewPanel("StageSelectPanel", parent, new Vector2(0.5f, 0.5f), Vector2.zero,
                                 new Vector2(900f, 360f), PanelBack);
            r.StageSelectPanel = panel;
            AddVerticalLayout(panel, 18f);
            AddLabel(panel.transform, "STAGE SELECT", 36, FontStyle.Bold, TextAnchor.MiddleCenter);
            AddLabel(panel.transform, "Chọn map → vào Match (load scene map additive)",
                     18, FontStyle.Normal, TextAnchor.MiddleCenter);

            int n = b.StageRegistry != null ? Mathf.Max(1, b.StageRegistry.Count) : 1;
            BuildSelectRow(panel.transform, "Stage:", n, b.StageSel);
        }

        /// <summary>Hàng N nút index 0..N-1, mỗi nút gắn IntButtonRaiser raise <paramref name="ch"/> với value=index.</summary>
        private static void BuildSelectRow(Transform parent, string label, int count, IntEventChannelSO ch)
        {
            var row = NewUI($"Row_{label}", parent);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f; hlg.childAlignment = TextAnchor.MiddleCenter;
            // childControlWidth=false: HLG KHÔNG ép lại width của nút → nút giữ nguyên sizeDelta/preferredWidth
            // đã set ở MakeButton (không bị sụp về 0 nếu tính toán preferred lỗi). Chỉ căn vị trí.
            hlg.childControlWidth = false; hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 70f; le.minHeight = 70f;
            le.flexibleWidth = 1f; // row chiếm hết bề ngang panel (VLG childForceExpandWidth=true cũng đảm bảo).

            AddLabel(row.transform, label, 22, FontStyle.Bold, TextAnchor.MiddleRight, 80f);

            for (int i = 0; i < count; i++)
            {
                var btn = MakeButton(row.transform, $"#{i}", BtnColor, 160f, 56f);
                var raiser = btn.gameObject.AddComponent<IntButtonRaiser>();
                var so = new SerializedObject(raiser);
                // Tên field khớp [SerializeField] trong IntButtonRaiser.cs: "channel" (IntEventChannelSO) + "value" (int).
                SetObj(so, "channel", ch);
                var vp = so.FindProperty("value");
                if (vp != null) vp.intValue = i;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(raiser); // không SetDirty → SerializedObject.intValue không flush vào scene khi save → Inspector trống.
            }
        }

        // ───────────────────── EndScreen ─────────────────────

        private static void BuildEndScreen(Transform parent, GameFlowHarnessAssets.Bundle b, Refs r)
        {
            var panel = NewPanel("EndScreenPanel", parent, new Vector2(0.5f, 0.5f), Vector2.zero,
                                 new Vector2(1000f, 640f), PanelBack);
            r.EndScreenPanel = panel;
            AddVerticalLayout(panel, 14f);

            var winner = AddLabel(panel.transform, "WINNER", 40, FontStyle.Bold, TextAnchor.MiddleCenter);

            var statsRow = NewUI("StatsRow", panel.transform);
            var hlg = statsRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 40f; hlg.childAlignment = TextAnchor.UpperCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = false;
            var sle = statsRow.AddComponent<LayoutElement>();
            sle.preferredHeight = 320f; sle.minHeight = 320f; sle.flexibleHeight = 1f;
            var s1 = AddLabel(statsRow.transform, "P1\n...", 22, FontStyle.Normal, TextAnchor.UpperLeft);
            var s2 = AddLabel(statsRow.transform, "P2\n...", 22, FontStyle.Normal, TextAnchor.UpperLeft);

            var view = panel.AddComponent<EndScreenView>();
            var so = new SerializedObject(view);
            SetObj(so, "channel", b.MatchResult);
            SetObj(so, "winnerLabel", winner);
            SetObj(so, "statsP1Label", s1);
            SetObj(so, "statsP2Label", s2);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view); // flush channel + label refs vào scene khi SaveScene.
            r.EndScreen = view;

            var btnRow = NewUI("Buttons", panel.transform);
            var bhlg = btnRow.AddComponent<HorizontalLayoutGroup>();
            bhlg.spacing = 20f; bhlg.childAlignment = TextAnchor.MiddleCenter;
            bhlg.childControlWidth = true; bhlg.childControlHeight = true;
            bhlg.childForceExpandWidth = false; bhlg.childForceExpandHeight = false;
            var brle = btnRow.AddComponent<LayoutElement>();
            brle.preferredHeight = 64f; brle.minHeight = 64f;

            MakeVoidRaiserButton(btnRow.transform, "REMATCH", b.Rematch);
            MakeVoidRaiserButton(btnRow.transform, "BACK", b.BackToCharSelect);
        }

        private static void MakeVoidRaiserButton(Transform parent, string text, VoidEventChannelSO ch)
        {
            var btn = MakeButton(parent, text, BtnColor, 220f, 56f);
            var raiser = btn.gameObject.AddComponent<VoidButtonRaiser>();
            var so = new SerializedObject(raiser);
            // Tên field khớp [SerializeField] trong VoidButtonRaiser.cs: "channel" (VoidEventChannelSO).
            SetObj(so, "channel", ch);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(raiser);
        }

        // ───────────────────── Debug panel ─────────────────────

        private static void BuildDebugPanel(Transform parent, GameFlowHarnessAssets.Bundle b, Refs r)
        {
            var panel = NewPanel("DebugPanel", parent, new Vector2(1f, 0f), new Vector2(-16f, 16f),
                                 new Vector2(360f, 560f), new Color(0f, 0f, 0f, 0.6f));
            // Neo góc dưới-phải.
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(1f, 0f); prt.anchorMax = new Vector2(1f, 0f); prt.pivot = new Vector2(1f, 0f);
            AddVerticalLayout(panel, 8f);
            AddLabel(panel.transform, "DEBUG (giả lập)", 22, FontStyle.Bold, TextAnchor.MiddleCenter);

            // Component runtime mang method (FA.GameFlow) — KHÔNG đặt logic ở editor script.
            r.DebugPanel = panel.AddComponent<GameFlowDebugPanel>();

            MakeMethodButton(panel.transform, "Kill P1", DbgBtnColor, r.DebugPanel, nameof(GameFlowDebugPanel.KillP1));
            MakeMethodButton(panel.transform, "Kill P2", DbgBtnColor, r.DebugPanel, nameof(GameFlowDebugPanel.KillP2));
            MakeMethodButton(panel.transform, "Damage P1->P2", BtnColor, r.DebugPanel, nameof(GameFlowDebugPanel.DamageP1ToP2));
            MakeMethodButton(panel.transform, "Damage P2->P1", BtnColor, r.DebugPanel, nameof(GameFlowDebugPanel.DamageP2ToP1));
            MakeMethodButton(panel.transform, "Combo+ P1", BtnColor, r.DebugPanel, nameof(GameFlowDebugPanel.ComboPlusP1));
            MakeMethodButton(panel.transform, "Combo+ P2", BtnColor, r.DebugPanel, nameof(GameFlowDebugPanel.ComboPlusP2));
            MakeMethodButton(panel.transform, "Combo Reset P1", BtnColor, r.DebugPanel, nameof(GameFlowDebugPanel.ComboResetP1));
            MakeMethodButton(panel.transform, "Combo Reset P2", BtnColor, r.DebugPanel, nameof(GameFlowDebugPanel.ComboResetP2));
            MakeMethodButton(panel.transform, "Force Time-Up", DbgBtnColor, r.DebugPanel, nameof(GameFlowDebugPanel.ForceTimeUp));
        }

        /// <summary>Nút gắn persistent onClick gọi method PUBLIC no-arg của <paramref name="target"/> (Unity-native UnityEvent).</summary>
        private static void MakeMethodButton(Transform parent, string text, Color color,
                                             GameFlowDebugPanel target, string methodName)
        {
            var btn = MakeButton(parent, text, color, 320f, 44f);
            // Persistent listener (lưu trong scene) gọi method PUBLIC no-arg lúc Play — Unity-native UnityEvent,
            // KHÔNG reflection runtime, hiện rõ trong Inspector onClick. UnityEventTools là editor API.
            var action = System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), target, methodName)
                         as UnityEngine.Events.UnityAction;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, action);
        }

        // ───────────────────── UI helpers ─────────────────────

        private static GameObject NewPanel(string name, Transform parent, Vector2 anchor, Vector2 offset,
                                           Vector2 size, Color back)
        {
            var p = NewUI(name, parent);
            var rt = p.GetComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
            rt.anchoredPosition = offset; rt.sizeDelta = size;
            AddImage(p, back);
            return p;
        }

        private static void AddVerticalLayout(GameObject go, float spacing)
        {
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing; vlg.padding = new RectOffset(20, 20, 20, 20);
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;
        }

        private static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Sprite _uiSprite;
        private static Sprite UiSprite
        {
            get
            {
                if (_uiSprite == null)
                    _uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                return _uiSprite;
            }
        }

        private static Image AddImage(GameObject go, Color c, bool raycastTarget = false)
        {
            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.sprite = UiSprite;
            img.type = Image.Type.Sliced;
            img.color = c;
            // Mặc định nền/panel KHÔNG nhận raycast → không "ăn" click đáng lẽ tới nút bên trên.
            // Chỉ Image của Button mới cần raycastTarget=true (targetGraphic) — truyền true ở MakeButton.
            img.raycastTarget = raycastTarget;
            return img;
        }

        private static Button MakeButton(Transform parent, string text, Color color, float w, float h)
        {
            var go = NewUI($"Btn_{text}", parent);
            // Image của Button PHẢI raycastTarget=true (đây là targetGraphic nhận click).
            var img = AddImage(go, color, raycastTarget: true);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            // Kích thước qua LayoutElement (cho LayoutGroup) + set thẳng sizeDelta làm fallback: nếu vì lý do
            // nào đó nút KHÔNG nằm trong layout group hợp lệ → vẫn có kích thước thực, không sụp về 0 (vô hình).
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = w; le.preferredHeight = h; le.minWidth = w; le.minHeight = h;
            le.flexibleWidth = 0f; le.flexibleHeight = 0f;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);

            var lbl = AddLabel(go.transform, text, 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            lbl.raycastTarget = false; // click "xuyên" label xuống Button bên dưới.
            // label stretch full bên trong nút.
            var lblRt = lbl.rectTransform;
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero; lblRt.offsetMax = Vector2.zero;
            return btn;
        }

        private static Text AddLabel(Transform parent, string text, int size, FontStyle style,
                                     TextAnchor align, float width = 0f)
        {
            var go = NewUI($"Label_{text}", parent);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = BuiltinFont();
            t.fontSize = size; t.fontStyle = style; t.color = Txt; t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            EditorUtility.SetDirty(t); // bảo đảm font + props serialize vào scene khi SaveScene.
            if (width > 0f)
            {
                var le = go.AddComponent<LayoutElement>();
                le.preferredWidth = width; le.minWidth = width;
                // Height fallback: trong HLG childControlHeight=false, label cần height riêng để không sụp 0.
                le.preferredHeight = size + 12f; le.minHeight = size + 12f;
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, size + 12f);
            }
            return t;
        }

        private static void EnsureMainCamera()
        {
            // Scene menu EMPTY (NewSceneSetup.EmptyScene) → KHÔNG có Camera → Unity báo "Display 1 No cameras
            // rendering". Canvas Screen-Space-Overlay vẫn vẽ nhưng cảnh báo hiện + world không thấy. Thêm 1
            // camera 2D nhẹ (MainCamera) để hết cảnh báo và nhìn được khi map load additive vào scene menu.
            if (Camera.main != null) return;
            var camGo = new GameObject("MenuCamera", typeof(Camera));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.transform.position = new Vector3(0f, 1f, -10f);
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f, 1f);
            Undo.RegisterCreatedObjectUndo(camGo, "Create Menu Camera");
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            // InputSystemUIInputModule thêm bằng code KHÔNG tự gán actions (khác khi thêm qua menu Component) →
            // pointer/click KHÔNG được xử lý → nút "bấm không có gì xảy ra". Gán bộ UI actions mặc định
            // (Point/Click/Navigate...) bằng API chính chủ để click hoạt động mà không cần actions asset riêng.
            var module = es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            module.AssignDefaultActions();
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        private static void SetObj(SerializedObject so, string prop, Object val)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = val;
        }

        private static void SetBool(SerializedObject so, string prop, bool val)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.boolValue = val;
        }

        private static Font _builtinFont;

        /// <summary>
        /// Font builtin của uGUI Text legacy, dạng asset SERIALIZE được vào scene (mở lại không mất).
        /// <see cref="Resources.GetBuiltinResource{T}(string)"/> là API chuẩn Unity 6 cho LegacyRuntime.ttf;
        /// <see cref="AssetDatabase.GetBuiltinExtraResource{T}(string)"/> (cách cũ trong tool) trả null trong
        /// ngữ cảnh này → Text mất font. Fallback giữ lại phòng môi trường khác; KHÔNG dùng
        /// Font.CreateDynamicFontFromOSFont (object runtime, không serialize vào scene đã lưu).
        /// </summary>
        private static Font BuiltinFont()
        {
            if (_builtinFont != null) return _builtinFont;
            // Fully-qualify: "Resources" trùng tên namespace FA.Resources trong project → phải dùng UnityEngine.Resources.
            _builtinFont = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_builtinFont == null)
                _builtinFont = AssetDatabase.GetBuiltinExtraResource<Font>("LegacyRuntime.ttf");
            if (_builtinFont == null)
                Debug.LogError("[GameFlowHarness] KHÔNG lấy được builtin font LegacyRuntime.ttf — Text sẽ trống chữ.");
            return _builtinFont;
        }
    }
}
