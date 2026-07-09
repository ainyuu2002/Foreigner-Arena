using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using FA.Core;
using FA.UI;
using FA.GameFlow;

namespace FA.EditorTools
{
    /// <summary>
    /// PHẦN MÀN HÌNH MENU (P12) của bộ tool dựng UI production (xem <see cref="UIProductionMenu"/>). Dựng 1
    /// Canvas Overlay <c>Menu_Screens</c> gồm 3 panel (MatchManager SetActive đúng 1 theo phase):
    ///   • CharSelect — bố cục mock tuần 3: lưới nhân vật CHUNG giữa + 2 card preview P1/P2 + XÁC NHẬN +
    ///     TIẾP TỤC (<see cref="CharSelectScreenView"/> quản lý lượt; kênh chỉ raise khi TIẾP TỤC).
    ///   • StageSelect — bố cục mock tuần 3: carousel map + card BẢN ĐỒ NGẪU NHIÊN + luật trận + BẮT ĐẦU
    ///     (<see cref="StageSelectScreenView"/>; kênh chỉ raise khi BẮT ĐẦU).
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
        public const string EndName = "Menu_EndScreen";

        private static readonly Color PanelBig = new Color(0.06f, 0.07f, 0.10f, 0.94f);

        public static GameObject Build(UIChannels.Bundle ch, out GameObject endRoot)
        {
            var menu = UIKit.NewCanvas(MenuName, 200);

            var charSel = BuildCharSelect(menu.transform, ch);
            var stageSel = BuildStageSelect(menu.transform, ch);

            // EndScreen: CANVAS RIÊNG Screen-Space-OVERLAY (không chung Menu_Screens camera-space).
            // GoToEnd KHÔNG unload map — scene map có CAMERA RIÊNG render đè camera Lobby → panel
            // camera-space vô hình trong lúc map còn load (đúng bệnh HUD "vào trận không thấy").
            // Overlay luôn vẽ trên mọi camera nên miễn nhiễm. Sorting 210: trên HUD 100, dưới Pause 400.
            endRoot = UIKit.NewCanvas(EndName, 210);
            var end = BuildEndScreen(endRoot.transform, ch);

            // Mặc định hiện CharSelect; ẩn 2 panel còn lại (chỉ 1 active mỗi phase — MatchManager điều khiển runtime).
            charSel.SetActive(true);
            stageSel.SetActive(false);
            end.SetActive(false);
            return menu;
        }

        // ───────────────────── CharSelect ─────────────────────

        /// <summary>
        /// Màn chọn nhân vật BỐ CỤC MOCK (tuần 3): panel FULL màn hình — chip trạng thái P1/P2 hai góc trên,
        /// lưới nhân vật chung ở giữa (6 cột, tự thêm hàng theo registry), card preview P1 trái / P2 phải
        /// (khung accent + chân dung + tên + XÁC NHẬN), nút TIẾP TỤC CHỌN MAP dưới giữa.
        /// <see cref="CharSelectScreenView"/> quản lý lượt chọn; kênh CHỈ raise khi TIẾP TỤC (đổi ý thoải mái).
        /// Art slot: "CharSelect_BG" (nền) + icon per-nhân-vật lấy từ CharacterRegistry.icon.
        /// </summary>
        private static GameObject BuildCharSelect(Transform parent, UIChannels.Bundle ch)
        {
            var panel = UIKit.NewUI("CharSelectPanel", parent);
            UIKit.StretchFull(panel);

            var bgSprite = UILobbyBuilder.FindSprite("CharSelect_BG");
            if (bgSprite != null)
            {
                var bgImg = panel.AddComponent<Image>();
                bgImg.sprite = bgSprite; bgImg.type = Image.Type.Simple; bgImg.color = Color.white;
                bgImg.raycastTarget = true;

                // Scrim tối nhẹ đè lên art (con ĐẦU TIÊN — nằm dưới mọi UI): art sét/dung nham rất rực,
                // không có scrim thì label lưới + chip trạng thái chìm vào nền.
                var scrim = UIKit.NewUI("Scrim", panel.transform);
                UIKit.StretchFull(scrim);
                UIKit.AddImage(scrim, new Color(0f, 0f, 0f, 0.30f));
            }
            else UIKit.AddImage(panel, PanelBig, raycastTarget: true);

            // Bake tên + icon từ registry (view giữ mảng riêng — không ref registry lúc runtime).
            int n = ch.CharacterRegistry != null ? Mathf.Max(1, ch.CharacterRegistry.Count) : 2;
            var names = new string[n];
            var icons = new Sprite[n];
            for (int i = 0; i < n; i++)
            {
                names[i] = NameOf(ch.CharacterRegistry, i, $"Fighter {i + 1}");
                icons[i] = IconOf(ch.CharacterRegistry, i);
            }

            // ── Chip trạng thái 2 góc trên ──
            Text statusP1 = MakeStatusChip(panel.transform, "P1", UIKit.ColP1Accent, left: true);
            Text statusP2 = MakeStatusChip(panel.transform, "P2", UIKit.ColP2Accent, left: false);

            // ── Lưới nhân vật giữa ── banner tiêu đề dùng chung (art "CharSelect_Banner").
            AddTitleBanner(panel.transform, "DANH SÁCH CÁC NHÂN VẬT", fallbackY: -94f);

            int cols = Mathf.Min(6, n);
            int rows = Mathf.CeilToInt(n / (float)cols);
            var grid = UIKit.NewUI("Grid", panel.transform);
            var grt = grid.GetComponent<RectTransform>();
            grt.anchorMin = grt.anchorMax = grt.pivot = new Vector2(0.5f, 0.5f);
            grt.anchoredPosition = new Vector2(0f, 20f);
            grt.sizeDelta = new Vector2(cols * 120f + (cols - 1) * 10f, rows * 144f + (rows - 1) * 10f);
            var glg = grid.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(120f, 144f);
            glg.spacing = new Vector2(10f, 10f);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = cols;
            glg.childAlignment = TextAnchor.MiddleCenter;

            var cellBtns = new Button[n];
            var tagsP1 = new GameObject[n];
            var tagsP2 = new GameObject[n];
            for (int i = 0; i < n; i++)
                cellBtns[i] = MakeCharCell(grid.transform, names[i], icons[i], i, out tagsP1[i], out tagsP2[i]);

            // ── 2 card preview ──
            CardRefs c1 = MakeCharCard(panel.transform, isP1: true);
            CardRefs c2 = MakeCharCard(panel.transform, isP1: false);

            // ── Nút tiếp tục (dưới giữa) ── ART SLOT "CharSelect_Btn": khung mạch điện xanh, label đè lên.
            var contBtn = UIKit.MakeButton(panel.transform, "TIẾP TỤC CHỌN MAP", UIKit.ColBtn, 520f, 70f, 26);
            UILobbyBuilder.ApplyButtonArt(contBtn, "CharSelect_Btn");
            var crt = contBtn.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0f);
            crt.anchoredPosition = new Vector2(0f, 36f);
            contBtn.interactable = false; // view bật khi cả 2 sẵn sàng.

            // ── View + wire ──
            var view = panel.AddComponent<CharSelectScreenView>();
            var so = new SerializedObject(view);
            UIKit.SetObj(so, "charSelectedP1", ch.CharSelP1);
            UIKit.SetObj(so, "charSelectedP2", ch.CharSelP2);
            UIKit.SetStringArray(so, "names", names);
            UIKit.SetObjArray(so, "portraits", icons);
            UIKit.SetObjArray(so, "tagsP1", tagsP1);
            UIKit.SetObjArray(so, "tagsP2", tagsP2);
            UIKit.SetObj(so, "previewImageP1", c1.Preview);
            UIKit.SetObj(so, "previewLetterP1", c1.Letter);
            UIKit.SetObj(so, "nameLabelP1", c1.Name);
            UIKit.SetObj(so, "statusLabelP1", statusP1);
            UIKit.SetObj(so, "confirmButtonP1", c1.Confirm);
            UIKit.SetObj(so, "confirmLabelP1", c1.ConfirmLabel);
            UIKit.SetObj(so, "previewImageP2", c2.Preview);
            UIKit.SetObj(so, "previewLetterP2", c2.Letter);
            UIKit.SetObj(so, "nameLabelP2", c2.Name);
            UIKit.SetObj(so, "statusLabelP2", statusP2);
            UIKit.SetObj(so, "confirmButtonP2", c2.Confirm);
            UIKit.SetObj(so, "confirmLabelP2", c2.ConfirmLabel);
            UIKit.SetObj(so, "continueButton", contBtn);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);

            for (int i = 0; i < n; i++)
            {
                UnityEventTools.AddIntPersistentListener(cellBtns[i].onClick,
                    new UnityAction<int>(view.SelectCell), i);
                EditorUtility.SetDirty(cellBtns[i]);
            }
            UnityEventTools.AddPersistentListener(c1.Confirm.onClick, new UnityAction(view.ConfirmP1));
            UnityEventTools.AddPersistentListener(c2.Confirm.onClick, new UnityAction(view.ConfirmP2));
            UnityEventTools.AddPersistentListener(contBtn.onClick, new UnityAction(view.Continue));
            EditorUtility.SetDirty(c1.Confirm);
            EditorUtility.SetDirty(c2.Confirm);
            EditorUtility.SetDirty(contBtn);
            return panel;
        }

        /// <summary>Chip trạng thái góc trên: "TRẠNG THÁI CHỌN Pn:" + text trạng thái (view cập nhật).</summary>
        private static Text MakeStatusChip(Transform parent, string tag, Color accent, bool left)
        {
            var chip = UIKit.NewPanel($"Status_{tag}", parent, new Vector2(left ? 0f : 1f, 1f),
                                      new Vector2(left ? 24f : -24f, -20f), new Vector2(470f, 64f), UIKit.ColPanel);
            UIKit.AddHorizontal(chip, 10f, controlW: true, expandW: false, TextAnchor.MiddleLeft, pad: 14);
            UIKit.AddLabel(chip.transform, $"TRẠNG THÁI CHỌN {tag}:", 22, FontStyle.Bold,
                           TextAnchor.MiddleLeft, accent, width: 265f);
            return UIKit.AddLabel(chip.transform, "Chưa chọn", 22, FontStyle.Bold,
                                  TextAnchor.MiddleLeft, UIKit.ColTxt, width: 165f);
        }

        /// <summary>Ô lưới nhân vật: icon (hoặc chữ cái đầu) + tên + 2 tag góc P1/P2 (ẩn — view bật khi chọn).</summary>
        private static Button MakeCharCell(Transform parent, string name, Sprite icon, int index,
                                           out GameObject tagP1, out GameObject tagP2)
        {
            var go = UIKit.NewUI($"Cell_{index}_{name}", parent);
            var img = UIKit.AddImage(go, UIKit.ColPanelLight, raycastTarget: true);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = Color.Lerp(UIKit.ColPanelLight, Color.white, 0.25f);
            colors.pressedColor = Color.Lerp(UIKit.ColPanelLight, Color.black, 0.20f);
            btn.colors = colors;

            // Vùng chân dung (trên): icon registry nếu có, không thì chữ cái đầu.
            if (icon != null)
            {
                var iconGo = UIKit.NewUI("Icon", go.transform);
                var irt = iconGo.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.5f, 1f); irt.anchorMax = new Vector2(0.5f, 1f);
                irt.pivot = new Vector2(0.5f, 1f); irt.anchoredPosition = new Vector2(0f, -8f);
                irt.sizeDelta = new Vector2(96f, 96f);
                var iimg = iconGo.AddComponent<Image>();
                iimg.sprite = icon; iimg.preserveAspect = true; iimg.raycastTarget = false;
            }
            else
            {
                var letter = UIKit.AddLabel(go.transform, name.Substring(0, 1).ToUpperInvariant(),
                                            56, FontStyle.Bold, TextAnchor.MiddleCenter,
                                            new Color(1f, 1f, 1f, 0.30f));
                var lrt = letter.GetComponent<RectTransform>();
                lrt.anchorMin = new Vector2(0f, 0.25f); lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            }

            var nameLbl = UIKit.AddLabel(go.transform, name, 15, FontStyle.Bold, TextAnchor.MiddleCenter);
            var nrt = nameLbl.GetComponent<RectTransform>();
            nrt.anchorMin = Vector2.zero; nrt.anchorMax = new Vector2(1f, 0f);
            nrt.offsetMin = new Vector2(2f, 4f); nrt.offsetMax = new Vector2(-2f, 32f);

            tagP1 = MakeCornerTag(go.transform, "P1", UIKit.ColP1Accent, left: true);
            tagP2 = MakeCornerTag(go.transform, "P2", UIKit.ColP2Accent, left: false);
            return btn;
        }

        private static GameObject MakeCornerTag(Transform parent, string txt, Color accent, bool left)
        {
            var tag = UIKit.NewUI($"Tag_{txt}", parent);
            var rt = tag.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(left ? 0f : 1f, 1f);
            rt.anchoredPosition = new Vector2(left ? 4f : -4f, -4f);
            rt.sizeDelta = new Vector2(36f, 26f);
            UIKit.AddImage(tag, accent);
            var lbl = UIKit.AddLabel(tag.transform, txt, 15, FontStyle.Bold, TextAnchor.MiddleCenter);
            UIKit.StretchFull(lbl.gameObject);
            tag.SetActive(false);
            return tag;
        }

        /// <summary>Refs các widget của 1 card preview (builder wire vào CharSelectScreenView).</summary>
        private sealed class CardRefs
        {
            public Image Preview;
            public Text Letter, Name, ConfirmLabel;
            public Button Confirm;
        }

        /// <summary>Card preview: khung accent (viền 5px) + tag Pn + chân dung lớn + tên + XÁC NHẬN.</summary>
        private static CardRefs MakeCharCard(Transform parent, bool isP1)
        {
            Color accent = isP1 ? UIKit.ColP1Accent : UIKit.ColP2Accent;
            var card = UIKit.NewPanel($"Card_{(isP1 ? "P1" : "P2")}", parent,
                                      new Vector2(isP1 ? 0f : 1f, 0.5f),
                                      new Vector2(isP1 ? 50f : -50f, 10f),
                                      new Vector2(340f, 600f), accent); // nền accent = VIỀN (inner đè giữa).

            var inner = UIKit.NewUI("Inner", card.transform);
            UIKit.StretchFull(inner);
            var irt = inner.GetComponent<RectTransform>();
            irt.offsetMin = new Vector2(5f, 5f); irt.offsetMax = new Vector2(-5f, -5f);
            UIKit.AddImage(inner, new Color(0.05f, 0.06f, 0.09f, 0.98f));
            UIKit.AddVertical(inner, 10f, 14, TextAnchor.UpperCenter);

            var tag = UIKit.AddLabel(inner.transform, isP1 ? "P1" : "P2", 34, FontStyle.Bold,
                                     TextAnchor.MiddleCenter, accent);
            UIKit.SetLayoutHeight(tag.gameObject, 48f);

            var previewHolder = UIKit.NewUI("Preview", inner.transform);
            var phLe = previewHolder.AddComponent<LayoutElement>();
            phLe.flexibleHeight = 1f; phLe.minHeight = 220f;
            UIKit.AddImage(previewHolder, UIKit.ColBarBack);

            var letter = UIKit.AddLabel(previewHolder.transform, "?", 150, FontStyle.Bold,
                                        TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.22f));
            UIKit.StretchFull(letter.gameObject);

            var portraitGo = UIKit.NewUI("Portrait", previewHolder.transform);
            UIKit.StretchFull(portraitGo);
            var prt = portraitGo.GetComponent<RectTransform>();
            prt.offsetMin = new Vector2(8f, 8f); prt.offsetMax = new Vector2(-8f, -8f);
            var portrait = portraitGo.AddComponent<Image>();
            portrait.preserveAspect = true; portrait.raycastTarget = false; portrait.enabled = false;

            var nameLbl = UIKit.AddLabel(inner.transform, "—", 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            UIKit.SetLayoutHeight(nameLbl.gameObject, 42f);

            var confirm = UIKit.MakeButton(inner.transform, "XÁC NHẬN",
                                           Color.Lerp(accent, Color.black, 0.35f), 300f, 64f, 26);
            // ART SLOT "CharSelect_Btn" (chung với nút TIẾP TỤC): sprite thay nền accent; viền card vẫn
            // giữ màu per-player nên không mất nhận diện P1/P2.
            UILobbyBuilder.ApplyButtonArt(confirm, "CharSelect_Btn");

            return new CardRefs
            {
                Preview = portrait,
                Letter = letter,
                Name = nameLbl,
                Confirm = confirm,
                ConfirmLabel = confirm.transform.Find("Label").GetComponent<Text>()
            };
        }

        // ───────────────────── StageSelect ─────────────────────

        /// <summary>
        /// Màn chọn map BỐ CỤC MOCK (tuần 3): banner "LỰA CHỌN CHIẾN TRƯỜNG" trên cùng, CAROUSEL giữa-trái
        /// (card map lớn + 2 card mờ 2 bên + mũi tên &lt; &gt; + chấm trang + chip tên map), card BẢN ĐỒ
        /// NGẪU NHIÊN bên phải, hàng luật trận + nút BẮT ĐẦU TRẬN ĐẤU dưới giữa.
        /// <see cref="StageSelectScreenView"/> quản lý; kênh StageSelected CHỈ raise khi BẮT ĐẦU.
        /// Art slot: "StageSelect_BG" (nền), "StageSelect_Random" (khối ? của card random); banner title +
        /// nút BẮT ĐẦU tái dùng "CharSelect_Banner"/"CharSelect_Btn" (đồng bộ 2 màn — v9.8);
        /// ảnh map lấy từ StageRegistry.icon (gán trong Inspector của registry).
        /// </summary>
        private static GameObject BuildStageSelect(Transform parent, UIChannels.Bundle ch)
        {
            var panel = UIKit.NewUI("StageSelectPanel", parent);
            UIKit.StretchFull(panel);

            var bgSprite = UILobbyBuilder.FindSprite("StageSelect_BG");
            if (bgSprite != null)
            {
                var bgImg = panel.AddComponent<Image>();
                bgImg.sprite = bgSprite; bgImg.type = Image.Type.Simple; bgImg.color = Color.white;
                bgImg.raycastTarget = true;

                // Scrim tối đè lên art (con ĐẦU TIÊN — dưới mọi UI): art sảnh obsidian có đấu trường +
                // vết nứt dung nham sáng NGAY VÙNG GIỮA, đúng chỗ carousel card map đè lên — scrim đậm
                // hơn CharSelect (0.40 vs 0.30) để ảnh chụp map trên card không chìm vào nền.
                var scrim = UIKit.NewUI("Scrim", panel.transform);
                UIKit.StretchFull(scrim);
                UIKit.AddImage(scrim, new Color(0f, 0f, 0f, 0.40f));
            }
            else UIKit.AddImage(panel, PanelBig, raycastTarget: true);

            int n = ch.StageRegistry != null ? Mathf.Max(1, ch.StageRegistry.Count) : 2;
            var names = new string[n];
            var previews = new Sprite[n];
            for (int i = 0; i < n; i++)
            {
                names[i] = NameOf(ch.StageRegistry, i, $"Stage {i + 1}");
                previews[i] = IconOf(ch.StageRegistry, i);
            }

            // ── Banner tiêu đề ── dùng chung khung "CharSelect_Banner" với màn chọn nhân vật (v9.8 —
            // đồng bộ art 2 màn theo yêu cầu user).
            AddTitleBanner(panel.transform, "LỰA CHỌN CHIẾN TRƯỜNG", fallbackY: -66f);

            // ── Carousel (lệch trái — chừa chỗ card random bên phải) ──
            var car = UIKit.NewUI("Carousel", panel.transform);
            var crt0 = car.GetComponent<RectTransform>();
            crt0.anchorMin = crt0.anchorMax = crt0.pivot = new Vector2(0.5f, 0.5f);
            crt0.anchoredPosition = new Vector2(-240f, 20f);
            crt0.sizeDelta = new Vector2(1080f, 580f);

            var tenMap = UIKit.AddLabel(car.transform, "tên map", 20, FontStyle.Normal,
                                        TextAnchor.MiddleCenter, UIKit.ColTxtDim);
            var tmrt = tenMap.GetComponent<RectTransform>();
            tmrt.anchorMin = tmrt.anchorMax = tmrt.pivot = new Vector2(0.5f, 0.5f);
            tmrt.anchoredPosition = new Vector2(0f, 264f);

            var chip = UIKit.NewPanel("NameChip", car.transform, new Vector2(0.5f, 0.5f),
                                      new Vector2(0f, 220f), new Vector2(430f, 54f), UIKit.ColPanel);
            var mapName = UIKit.AddLabel(chip.transform, "—", 26, FontStyle.Bold, TextAnchor.MiddleCenter);
            UIKit.StretchFull(mapName.gameObject);

            // Card mờ 2 bên vẽ TRƯỚC (dưới), card giữa vẽ SAU (đè lên khi chồng mép).
            MakeMapCard(car.transform, "Prev", new Vector2(-330f, -30f), new Vector2(210f, 290f), dim: true,
                        out Image prevImg, out Text prevLetter);
            MakeMapCard(car.transform, "Next", new Vector2(330f, -30f), new Vector2(210f, 290f), dim: true,
                        out Image nextImg, out Text nextLetter);
            MakeMapCard(car.transform, "Center", new Vector2(0f, -30f), new Vector2(310f, 410f), dim: false,
                        out Image centerImg, out Text centerLetter);

            var prevBtn = UIKit.MakeButton(car.transform, "<", UIKit.ColPanelLight, 70f, 70f, 32);
            var pbrt = prevBtn.GetComponent<RectTransform>();
            pbrt.anchorMin = pbrt.anchorMax = pbrt.pivot = new Vector2(0.5f, 0.5f);
            pbrt.anchoredPosition = new Vector2(-490f, -30f);
            var nextBtn = UIKit.MakeButton(car.transform, ">", UIKit.ColPanelLight, 70f, 70f, 32);
            var nbrt = nextBtn.GetComponent<RectTransform>();
            nbrt.anchorMin = nbrt.anchorMax = nbrt.pivot = new Vector2(0.5f, 0.5f);
            nbrt.anchoredPosition = new Vector2(490f, -30f);

            // Chấm trang (1 chấm / map).
            var dotsRow = UIKit.NewUI("Dots", car.transform);
            var drt = dotsRow.GetComponent<RectTransform>();
            drt.anchorMin = drt.anchorMax = drt.pivot = new Vector2(0.5f, 0.5f);
            drt.anchoredPosition = new Vector2(0f, -268f);
            drt.sizeDelta = new Vector2(n * 22f, 14f);
            UIKit.AddHorizontal(dotsRow, 8f, controlW: false, expandW: false, TextAnchor.MiddleCenter);
            var dots = new Image[n];
            for (int i = 0; i < n; i++)
            {
                var dot = UIKit.NewUI($"Dot_{i}", dotsRow.transform);
                dot.GetComponent<RectTransform>().sizeDelta = new Vector2(14f, 14f);
                dots[i] = UIKit.AddImage(dot, Color.white);
            }

            // ── Card BẢN ĐỒ NGẪU NHIÊN (phải) — cả card là 1 nút toggle ──
            var randGo = UIKit.NewUI("RandomCard", panel.transform);
            var rrt = randGo.GetComponent<RectTransform>();
            rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(1f, 0.5f);
            rrt.anchoredPosition = new Vector2(-70f, 20f);
            rrt.sizeDelta = new Vector2(400f, 460f);

            var highlight = UIKit.NewUI("Highlight", randGo.transform);
            UIKit.StretchFull(highlight);
            var hlrt = highlight.GetComponent<RectTransform>();
            hlrt.offsetMin = new Vector2(-8f, -8f); hlrt.offsetMax = new Vector2(8f, 8f);
            UIKit.AddImage(highlight, UIKit.ColRage); // viền cam sáng khi bật chế độ ngẫu nhiên.
            // randGo có VLG — viền phải đứng NGOÀI flow dọc (kẻo bị cuốn thành 1 hàng layout).
            highlight.AddComponent<LayoutElement>().ignoreLayout = true;
            highlight.SetActive(false);

            var randImg = UIKit.AddImage(randGo, UIKit.ColPanelLight, raycastTarget: true);

            // Khung card (v9.9, user yêu cầu): viền ĐỒNG CHÁY 2 lớp — Image ngoài loe ±6 làm viền, Image
            // trong lùi 3px phủ nền tối → thành ring dày ~9px, tông khớp khung kim loại trong art orb.
            // Cả 2 ignoreLayout (VLG của card đang xếp dọc); vẽ SAU highlight (viền cam random vẫn loe
            // ±8 ra NGOÀI khung), TRƯỚC art/label (không che nội dung — con sau đè con trước).
            var frame = UIKit.NewUI("Frame", randGo.transform);
            UIKit.StretchFull(frame);
            var frt = frame.GetComponent<RectTransform>();
            frt.offsetMin = new Vector2(-6f, -6f); frt.offsetMax = new Vector2(6f, 6f);
            frame.AddComponent<LayoutElement>().ignoreLayout = true;
            var frameImg = UIKit.AddImage(frame, new Color(0.58f, 0.38f, 0.16f, 1f));

            var frameInner = UIKit.NewUI("FrameInner", randGo.transform);
            UIKit.StretchFull(frameInner);
            var firt = frameInner.GetComponent<RectTransform>();
            firt.offsetMin = new Vector2(3f, 3f); firt.offsetMax = new Vector2(-3f, -3f);
            frameInner.AddComponent<LayoutElement>().ignoreLayout = true;
            UIKit.AddImage(frameInner, new Color(0.07f, 0.08f, 0.11f, 1f));

            var randBtn = randGo.AddComponent<Button>();
            randBtn.targetGraphic = frameImg; // hover/press tint VIỀN đồng (nền tối bên trong giữ nguyên).

            UIKit.AddVertical(randGo, 10f, 18, TextAnchor.UpperCenter);
            var artHolder = UIKit.NewUI("Art", randGo.transform);
            var ahLe = artHolder.AddComponent<LayoutElement>();
            ahLe.flexibleHeight = 1f; ahLe.minHeight = 240f;
            var randSprite = UILobbyBuilder.FindSprite("StageSelect_Random");
            if (randSprite != null)
            {
                var aimg = artHolder.AddComponent<Image>();
                aimg.sprite = randSprite; aimg.preserveAspect = true; aimg.raycastTarget = false;
            }
            else
            {
                var q = UIKit.AddLabel(artHolder.transform, "?", 150, FontStyle.Bold, TextAnchor.MiddleCenter,
                                       new Color(1f, 1f, 1f, 0.30f));
                UIKit.StretchFull(q.gameObject);
            }
            var randTitle = UIKit.AddLabel(randGo.transform, "BẢN ĐỒ NGẪU NHIÊN", 28, FontStyle.Bold,
                                           TextAnchor.MiddleCenter);
            UIKit.SetLayoutHeight(randTitle.gameObject, 40f);
            var randSub = UIKit.AddLabel(randGo.transform, "SẼ CHỌN MỘT TRẬN ĐẤU BẤT NGỜ", 17,
                                         FontStyle.Normal, TextAnchor.MiddleCenter, UIKit.ColTxtDim);
            UIKit.SetLayoutHeight(randSub.gameObject, 30f);

            // ── Hàng luật trận + nút bắt đầu (dưới giữa) ──
            var rulesHolder = UIKit.NewUI("RulesHolder", panel.transform);
            var rhrt = rulesHolder.GetComponent<RectTransform>();
            rhrt.anchorMin = rhrt.anchorMax = rhrt.pivot = new Vector2(0.5f, 0f);
            rhrt.anchoredPosition = new Vector2(0f, 118f);
            rhrt.sizeDelta = new Vector2(1000f, 58f);
            UIKit.AddVertical(rulesHolder, 0f, 0, TextAnchor.MiddleCenter);
            UISettingsBuilder.AddMatchRules(rulesHolder.transform, ch);

            var startBtn = UIKit.MakeButton(panel.transform, "BẮT ĐẦU TRẬN ĐẤU", UIKit.ColBtn, 520f, 70f, 28);
            // Cùng khung mạch điện với nút TIẾP TỤC CHỌN MAP / XÁC NHẬN bên CharSelect (cùng cỡ 520×70).
            UILobbyBuilder.ApplyButtonArt(startBtn, "CharSelect_Btn");
            var srt = startBtn.GetComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0f);
            srt.anchoredPosition = new Vector2(0f, 36f);

            // ── View + wire ──
            var view = panel.AddComponent<StageSelectScreenView>();
            var so = new SerializedObject(view);
            UIKit.SetObj(so, "stageSelectedChannel", ch.StageSel);
            UIKit.SetStringArray(so, "names", names);
            UIKit.SetObjArray(so, "previews", previews);
            UIKit.SetObj(so, "mapNameLabel", mapName);
            UIKit.SetObj(so, "centerImage", centerImg);
            UIKit.SetObj(so, "centerLetter", centerLetter);
            UIKit.SetObj(so, "prevImage", prevImg);
            UIKit.SetObj(so, "prevLetter", prevLetter);
            UIKit.SetObj(so, "nextImage", nextImg);
            UIKit.SetObj(so, "nextLetter", nextLetter);
            UIKit.SetObjArray(so, "dots", dots);
            UIKit.SetObj(so, "randomHighlight", highlight);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);

            UnityEventTools.AddPersistentListener(prevBtn.onClick, new UnityAction(view.Prev));
            UnityEventTools.AddPersistentListener(nextBtn.onClick, new UnityAction(view.Next));
            UnityEventTools.AddPersistentListener(randBtn.onClick, new UnityAction(view.ToggleRandom));
            UnityEventTools.AddPersistentListener(startBtn.onClick, new UnityAction(view.StartMatch));
            EditorUtility.SetDirty(prevBtn);
            EditorUtility.SetDirty(nextBtn);
            EditorUtility.SetDirty(randBtn);
            EditorUtility.SetDirty(startBtn);
            return panel;
        }

        /// <summary>
        /// Banner tiêu đề DÙNG CHUNG CharSelect/StageSelect: khung art "CharSelect_Banner" (720 rộng, neo
        /// đỉnh-giữa, cao theo tỉ lệ sprite — ảnh đã crop sát viền nên tâm ảnh = tâm khung) + chữ 30pt canh
        /// TÂM khung. Chữ pivot GIỮA (không phải đỉnh): rect NewUI mặc định cao 100 → pivot đỉnh làm tâm chữ
        /// tụt 50px, chữ rơi xuống mép dưới khung. Không có art → chữ trần tại <paramref name="fallbackY"/>.
        /// </summary>
        private static void AddTitleBanner(Transform parent, string text, float fallbackY)
        {
            float titleY = fallbackY;
            var bannerSp = UILobbyBuilder.FindSprite("CharSelect_Banner");
            if (bannerSp != null)
            {
                var banner = UIKit.NewUI("TitleBanner", parent);
                var brt = banner.GetComponent<RectTransform>();
                brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 1f);
                const float bw = 720f;
                float bh = bw * bannerSp.rect.height / bannerSp.rect.width;
                brt.sizeDelta = new Vector2(bw, bh);
                brt.anchoredPosition = new Vector2(0f, -6f);
                var bimg = banner.AddComponent<Image>();
                bimg.sprite = bannerSp; bimg.type = Image.Type.Simple; bimg.color = Color.white;
                bimg.raycastTarget = false;
                titleY = -6f - bh * 0.5f; // chữ nằm giữa tấm khung.
            }
            var title = UIKit.AddLabel(parent, text, 30, FontStyle.Bold, TextAnchor.MiddleCenter);
            var trt = title.GetComponent<RectTransform>();
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = new Vector2(0f, titleY);
        }

        /// <summary>Card ảnh map cho carousel: khung tối + Image preserveAspect + chữ cái fallback.</summary>
        private static void MakeMapCard(Transform parent, string name, Vector2 pos, Vector2 size, bool dim,
                                        out Image img, out Text letter)
        {
            var card = UIKit.NewPanel($"Card_{name}", parent, new Vector2(0.5f, 0.5f), pos, size,
                                      dim ? new Color(0.09f, 0.10f, 0.14f, 0.90f) : UIKit.ColPanelLight);

            letter = UIKit.AddLabel(card.transform, "?", dim ? 64 : 110, FontStyle.Bold,
                                    TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.22f));
            UIKit.StretchFull(letter.gameObject);

            var imgGo = UIKit.NewUI("Img", card.transform);
            UIKit.StretchFull(imgGo);
            var irt = imgGo.GetComponent<RectTransform>();
            irt.offsetMin = new Vector2(6f, 6f); irt.offsetMax = new Vector2(-6f, -6f);
            img = imgGo.AddComponent<Image>();
            img.preserveAspect = true; img.raycastTarget = false; img.enabled = false;
            if (dim) img.color = new Color(1f, 1f, 1f, 0.45f); // card bên mờ đi — tập trung vào card giữa.
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

        private static Sprite IconOf(StageRegistrySO reg, int i)
        {
            var e = reg != null ? reg.Get(i) : null;
            return e != null ? e.icon : null;
        }
    }
}
