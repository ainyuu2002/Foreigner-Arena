using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FA.EditorTools
{
    /// <summary>
    /// PRIMITIVES dùng chung cho bộ tool dựng UI PRODUCTION (P8 HUD + P12 màn hình) — xem
    /// <see cref="UIProductionMenu"/>. Gom mọi helper uGUI Unity-native (palette, font/sprite builtin,
    /// panel/label/button/bar, layout, EventSystem/Camera, SerializedObject setters) để 3 builder
    /// (<see cref="UIHudBuilder"/>, <see cref="UIScreensBuilder"/>) KHÔNG lặp code (DRY, §1.2).
    ///
    /// EDITOR-ONLY (asmdef FA.EditorTools.Editor, includePlatforms=Editor): được ref FA.UI/FA.Core vì
    /// KHÔNG module gameplay nào ref ngược → không phá cô lập §1.3. KHÔNG tự vẽ widget bằng code: chỉ
    /// add component uGUI (Image/Text/Button/LayoutGroup) + cấu hình (§1.1).
    ///
    /// Các gotcha ĐÃ chốt (đồng bộ GameFlowHarnessCanvas/ResourceTestHudCanvas):
    ///   • Font: <see cref="UnityEngine.Resources.GetBuiltinResource{T}"/>("LegacyRuntime.ttf") — serialize
    ///     được vào scene (AssetDatabase.GetBuiltinExtraResource trả null trong ngữ cảnh này → mất font).
    ///   • Image Type=Filled BẮT BUỘC có sprite (UISprite.psd) thì fillAmount mới có tác dụng.
    ///   • Sau set SerializedObject phải <see cref="EditorUtility.SetDirty"/> kẻo ref null lúc Play.
    ///   • Button.targetGraphic raycastTarget=TRUE; label raycastTarget=FALSE (click xuyên xuống nút).
    ///   • EventSystem dùng InputSystemUIInputModule phải gọi AssignDefaultActions() mới nhận click.
    /// </summary>
    internal static class UIKit
    {
        // ───────────────────── Palette (production, tông tối + nhấn) ─────────────────────
        public static readonly Color ColPanel      = new Color(0.07f, 0.08f, 0.11f, 0.86f);
        public static readonly Color ColPanelLight = new Color(0.13f, 0.15f, 0.20f, 0.95f);
        public static readonly Color ColBarBack    = new Color(0f, 0f, 0f, 0.55f);
        public static readonly Color ColPercent    = new Color(0.93f, 0.27f, 0.22f, 1f); // đỏ — % knockback
        public static readonly Color ColRage       = new Color(1f, 0.52f, 0.06f, 1f);    // cam — nộ
        public static readonly Color ColMana       = new Color(0.27f, 0.62f, 1f, 1f);    // xanh — mana ô đầy
        public static readonly Color ColManaTrack  = new Color(0.20f, 0.22f, 0.28f, 0.9f);
        public static readonly Color ColLife       = new Color(0.96f, 0.83f, 0.22f, 1f); // vàng — icon mạng
        public static readonly Color ColLifeEmpty  = new Color(0.22f, 0.22f, 0.25f, 0.85f);
        public static readonly Color ColBtn        = new Color(0.17f, 0.33f, 0.55f, 1f);
        public static readonly Color ColBtnAccent  = new Color(0.55f, 0.20f, 0.22f, 1f);
        public static readonly Color ColTxt        = Color.white;
        public static readonly Color ColTxtDim     = new Color(0.78f, 0.80f, 0.86f, 1f);
        public static readonly Color ColP1Accent   = new Color(0.30f, 0.55f, 0.95f, 1f); // xanh dương — P1
        public static readonly Color ColP2Accent   = new Color(0.92f, 0.40f, 0.35f, 1f); // đỏ cam — P2

        // ───────────────────── GameObject / RectTransform ─────────────────────

        public static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>Panel neo theo anchor (vd (0.5,1) = trên-giữa). offset = anchoredPosition.</summary>
        public static GameObject NewPanel(string name, Transform parent, Vector2 anchor, Vector2 offset,
                                          Vector2 size, Color back)
        {
            var p = NewUI(name, parent);
            var rt = p.GetComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
            rt.anchoredPosition = offset; rt.sizeDelta = size;
            AddImage(p, back);
            return p;
        }

        public static void StretchFull(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        // ───────────────────── Image / Label / Button ─────────────────────

        private static Sprite _uiSprite;
        public static Sprite UiSprite
        {
            get
            {
                if (_uiSprite == null)
                    _uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                return _uiSprite;
            }
        }

        public static Image AddImage(GameObject go, Color c, bool raycastTarget = false)
        {
            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.sprite = UiSprite;             // BẮT BUỘC cho Image Type=Filled hoạt động.
            img.type = Image.Type.Sliced;
            img.color = c;
            img.raycastTarget = raycastTarget; // nền KHÔNG nhận raycast → không nuốt click của nút.
            return img;
        }

        public static Text AddLabel(Transform parent, string text, int size, FontStyle style,
                                    TextAnchor align, Color? color = null, float width = 0f)
        {
            var go = NewUI($"Label_{text}", parent);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = Font();
            t.fontSize = size; t.fontStyle = style; t.color = color ?? ColTxt; t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            EditorUtility.SetDirty(t);
            if (width > 0f)
            {
                var le = go.AddComponent<LayoutElement>();
                le.preferredWidth = width; le.minWidth = width;
                le.preferredHeight = size + 12f; le.minHeight = size + 12f;
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, size + 12f);
            }
            return t;
        }

        /// <summary>Nút uGUI: Image (targetGraphic, raycast ON) + Button + label stretch full + LayoutElement fallback size.</summary>
        public static Button MakeButton(Transform parent, string text, Color color, float w, float h, int fontSize = 20)
        {
            var go = NewUI($"Btn_{text}", parent);
            var img = AddImage(go, color, raycastTarget: true);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.25f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.20f);
            btn.colors = colors;

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = w; le.preferredHeight = h; le.minWidth = w; le.minHeight = h;
            le.flexibleWidth = 0f; le.flexibleHeight = 0f;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);

            var lbl = AddLabel(go.transform, text, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter);
            lbl.raycastTarget = false;
            StretchFull(lbl.gameObject);
            lbl.gameObject.name = "Label"; // tên cố định để dễ tìm khi tinh chỉnh.
            return btn;
        }

        /// <summary>Thanh fill liên tục (Image Type=Filled Horizontal) trong 1 khung nền. Trả Image fill để gắn view.</summary>
        public static Image MakeFilledBar(Transform parent, Color fillColor, float height)
        {
            var holder = NewUI("Bar", parent);
            SetLayoutHeight(holder, height);
            var holderLe = holder.GetComponent<LayoutElement>();
            holderLe.flexibleWidth = 1f;
            AddImage(holder, ColBarBack);

            var fillGo = NewUI("Fill", holder.transform);
            StretchFull(fillGo);
            var fillImg = AddImage(fillGo, fillColor);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 0f;
            return fillImg;
        }

        // ───────────────────── Layout helpers ─────────────────────

        public static VerticalLayoutGroup AddVertical(GameObject go, float spacing, int pad = 16,
                                                      TextAnchor align = TextAnchor.UpperCenter,
                                                      bool expandH = false)
        {
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing; vlg.padding = new RectOffset(pad, pad, pad, pad);
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = expandH;
            vlg.childAlignment = align;
            return vlg;
        }

        public static HorizontalLayoutGroup AddHorizontal(GameObject go, float spacing,
                                                          bool controlW, bool expandW,
                                                          TextAnchor align = TextAnchor.MiddleCenter, int pad = 0)
        {
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing; hlg.padding = new RectOffset(pad, pad, pad, pad);
            hlg.childControlWidth = controlW; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = expandW; hlg.childForceExpandHeight = false;
            hlg.childAlignment = align;
            return hlg;
        }

        public static LayoutElement SetLayoutHeight(GameObject go, float h)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredHeight = h; le.minHeight = h;
            return le;
        }

        // ───────────────────── Scene infra (camera + event system) ─────────────────────

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            WireUiInputModule(es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>());
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        /// <summary>
        /// Gán UI input cho module. KHÔNG dùng <c>AssignDefaultActions()</c> — bản Input System này ném
        /// InvalidOperationException ("Action 'UI/Point' must be part of an InputActionAsset") vì default
        /// actions không thuộc asset nào. Thay vào đó: tìm 1 InputActionAsset CÓ map "UI" (vd template
        /// InputSystem_Actions) → gán asset + từng reference (action NẰM trong asset nên Create() không ném).
        /// </summary>
        private static void WireUiInputModule(UnityEngine.InputSystem.UI.InputSystemUIInputModule module)
        {
            var uiAsset = FindUiActionsAsset();
            if (uiAsset == null)
            {
                try { module.AssignDefaultActions(); }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[UIKit] Không thấy InputActionAsset có map 'UI' và AssignDefaultActions lỗi: "
                        + e.Message + " → click trong UI_Build có thể không ăn (KHÔNG ảnh hưởng prefab/scene đã dựng).");
                }
                return;
            }
            module.actionsAsset = uiAsset;
            module.point                    = UiRef(uiAsset, "Point");
            module.leftClick                = UiRef(uiAsset, "Click");
            module.middleClick              = UiRef(uiAsset, "MiddleClick");
            module.rightClick               = UiRef(uiAsset, "RightClick");
            module.scrollWheel              = UiRef(uiAsset, "ScrollWheel");
            module.move                     = UiRef(uiAsset, "Navigate");
            module.submit                   = UiRef(uiAsset, "Submit");
            module.cancel                   = UiRef(uiAsset, "Cancel");
            module.trackedDevicePosition    = UiRef(uiAsset, "TrackedDevicePosition");
            module.trackedDeviceOrientation = UiRef(uiAsset, "TrackedDeviceOrientation");
        }

        private static UnityEngine.InputSystem.InputActionReference UiRef(
            UnityEngine.InputSystem.InputActionAsset asset, string action)
        {
            var a = asset.FindAction($"UI/{action}");
            return a != null ? UnityEngine.InputSystem.InputActionReference.Create(a) : null;
        }

        private static UnityEngine.InputSystem.InputActionAsset FindUiActionsAsset()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:InputActionAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && asset.FindActionMap("UI") != null) return asset;
            }
            return null;
        }

        public static void EnsureCamera()
        {
            if (Camera.main != null) return;
            var camGo = new GameObject("UICamera", typeof(Camera)) { tag = "MainCamera" };
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 8f;
            cam.transform.position = new Vector3(0f, 1f, -10f);
            cam.backgroundColor = new Color(0.10f, 0.11f, 0.14f, 1f);
            Undo.RegisterCreatedObjectUndo(camGo, "Create UI Camera");
        }

        /// <summary>Canvas Screen-Space-Overlay chuẩn 1920×1080 ScaleWithScreenSize. sortingOrder tách HUD/menu.</summary>
        public static GameObject NewCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(go, "Create Canvas " + name);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            // CHỮ NÉT: legacy Text bị MỜ khi canvas scale ≠ 1 (atlas font bake ở cỡ logic rồi phóng to/thu nhỏ).
            // dynamicPixelsPerUnit cao → bake glyph ở mật độ pixel lớn hơn → chữ sắc nét ở mọi độ phân giải.
            scaler.dynamicPixelsPerUnit = 4f;
            scaler.referencePixelsPerUnit = 100f;
            canvas.pixelPerfect = true; // snap UI vào lưới pixel → cạnh chữ/icon sắc, bớt nhòe.
            return go;
        }

        // ───────────────────── Quét file project (KHÔNG phụ thuộc CWD) ─────────────────────

        /// <summary>
        /// Quét file dưới Assets/_Project bằng đường dẫn TUYỆT ĐỐI (từ <see cref="Application.dataPath"/>)
        /// rồi trả đường dẫn TƯƠNG ĐỐI "Assets/..." cho <c>AssetDatabase.LoadAssetAtPath</c>.
        /// ROOT-CAUSE (Lobby v9, vụ "bấm TIẾP TỤC không ăn"): bản cũ đưa thẳng "Assets/_Project" (tương đối)
        /// vào <c>Directory.GetFiles</c> — CWD của Unity trên Windows bị file dialog (Open/Save/Import) đổi
        /// mất → scan trượt TOÀN BỘ .asset (sprite vẫn thấy nhờ fallback FindAssets — index thấy texture
        /// nhưng trượt ScriptableObject) → mọi ref asset trong scene = {fileID: 0}, lúc được lúc không theo
        /// việc user có mở dialog trước khi build hay chưa. dataPath tuyệt đối nên miễn nhiễm CWD.
        /// </summary>
        public static string[] FindProjectFiles(string searchPattern)
        {
            string dataPath = Application.dataPath.Replace('/', '\\');      // ...\<Project>\Assets
            string projectRoot = System.IO.Path.GetDirectoryName(dataPath); // ...\<Project>
            string absRoot = System.IO.Path.Combine(dataPath, "_Project");
            if (projectRoot == null || !System.IO.Directory.Exists(absRoot)) return new string[0];

            string[] files = System.IO.Directory.GetFiles(absRoot, searchPattern,
                                                          System.IO.SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
                files[i] = files[i].Substring(projectRoot.Length + 1).Replace('\\', '/');
            return files;
        }

        // ───────────────────── SerializedObject setters ─────────────────────

        public static void SetObj(SerializedObject so, string prop, Object val)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = val;
        }

        public static void SetBool(SerializedObject so, string prop, bool val)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.boolValue = val;
        }

        public static void SetFloat(SerializedObject so, string prop, float val)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.floatValue = val;
        }

        public static void SetInt(SerializedObject so, string prop, int val)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.intValue = val;
        }

        /// <summary>Gán mảng object reference cho 1 SerializedProperty (vd cells[]/icons[]).</summary>
        public static void SetObjArray(SerializedObject so, string prop, Object[] values)
        {
            var p = so.FindProperty(prop);
            if (p == null) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        /// <summary>Gán mảng string cho 1 SerializedProperty (vd names[] bake từ registry).</summary>
        public static void SetStringArray(SerializedObject so, string prop, string[] values)
        {
            var p = so.FindProperty(prop);
            if (p == null) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).stringValue = values[i];
        }

        // ───────────────────── Font ─────────────────────

        private static Font _font;
        public static Font Font()
        {
            if (_font != null) return _font;
            // Fully-qualify: "Resources" trùng namespace FA.Resources trong project.
            _font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = AssetDatabase.GetBuiltinExtraResource<Font>("LegacyRuntime.ttf");
            if (_font == null) Debug.LogError("[UIKit] KHÔNG lấy được builtin font LegacyRuntime.ttf — Text sẽ trống chữ.");
            return _font;
        }
    }
}
