using UnityEngine;

namespace FA.EditorTools
{
    /// <summary>
    /// Sinh art pixel map BĂNG/TUYẾT "Đỉnh Vĩnh Đông" (design 01): trời đêm + trăng + cực quang + núi tuyết
    /// parallax, tile băng phủ tuyết, platform bay có nhũ băng, tường băng vỡ được. PPU nền = 16, tile = 64.
    /// </summary>
    internal static class StageArtIce
    {
        public const string Dir = "Assets/_Project/Art/Maps/Ice";
        public const string Sky = Dir + "/Ice_Sky.png";
        public const string Mountains = Dir + "/Ice_Mountains.png";
        public const string Pines = Dir + "/Ice_Pines.png";
        public const string TileTop = Dir + "/Ice_TileTop.png";
        public const string TileTopB = Dir + "/Ice_TileTopB.png";
        public const string TileGloss = Dir + "/Ice_TileGloss.png";
        public const string TileInner = Dir + "/Ice_TileInner.png";
        public const string TileInnerB = Dir + "/Ice_TileInnerB.png";
        public const string Platform = Dir + "/Ice_Platform.png";
        public const string Wall = Dir + "/Ice_Wall.png";
        public const string Icicle = Dir + "/Ice_Icicle.png";
        public const string Iceberg = Dir + "/Ice_Iceberg.png";
        public const string Aurora = Dir + "/Ice_Aurora.png";
        public const string FloorFace = Dir + "/Ice_FloorFace.png";
        public const string Spikes = Dir + "/Ice_Spikes.png";
        public const string ClingWall = Dir + "/Ice_ClingWall.png";

        public static void Generate()
        {
            StagePxCanvas.Reseed(101);
            GenSky();
            GenMountains();
            GenPines();
            GenTiles();
            GenPlatform();
            GenWall();
            GenIcicle();
            GenIceberg();
            GenFloorFace();
            GenSpikes();
            GenAurora();
            GenClingWall();
        }

        /// <summary>CỘT BĂNG BÁM lơ lửng (feedback: tường trên không bám được): trụ băng dọc 1×4 unit,
        /// chỏm tuyết trên đỉnh, highlight chéo + vết nứt, nhũ băng rủ dưới đáy.</summary>
        private static void GenClingWall()
        {
            StagePxCanvas.Reseed(515);
            var c = new StagePxCanvas(32, 128, new Color32(0, 0, 0, 0));
            c.FillRect(1, 6, 30, 118, StagePxCanvas.Hex("3a7cb0"));         // thân trụ
            for (int y = 6; y < 124; y++)                                    // gradient dọc thủ công
                if (y % 3 != 0) c.BlendRect(1, y, 30, 1, new Color32(43, 99, 148, 60));
            c.FillRect(1, 6, 3, 118, StagePxCanvas.Hex("245a86"));           // rìa tối trái
            c.FillRect(28, 6, 3, 118, StagePxCanvas.Hex("245a86"));          // rìa tối phải
            c.FillRect(5, 6, 2, 118, StagePxCanvas.Hex("7ec5e8"));           // vệt sáng trong
            var hi = StagePxCanvas.Hex("bfe6fa"); hi.a = 170;
            for (int i = 0; i < 7; i++)                                      // highlight chéo lấp lánh
            {
                int x = StagePxCanvas.RandInt(4, 24), y = StagePxCanvas.RandInt(12, 112);
                for (int k = 0; k < StagePxCanvas.RandInt(4, 8); k++) c.Blend(x + k, y + k, hi);
            }
            var crack = new Color32(30, 70, 104, 160);
            for (int i = 0; i < 4; i++)                                      // vết nứt ngang mảnh
            {
                int y = StagePxCanvas.RandInt(16, 110), x0 = StagePxCanvas.RandInt(3, 12);
                for (int k = 0; k < StagePxCanvas.RandInt(10, 18); k++)
                    c.Blend(x0 + k, y + (k % 4 == 0 ? 1 : 0), crack);
            }
            c.FillRect(0, 118, 32, 8, StagePxCanvas.Hex("eef6ff"));          // chỏm tuyết đỉnh
            c.Disc(8, 125, 3, Color.white);
            c.Disc(22, 126, 4, Color.white);
            c.TriangleDown(8, 8, 8, 14, StagePxCanvas.Hex("3d7fb0"));        // nhũ băng rủ đáy
            c.TriangleDown(20, 8, 10, 20, StagePxCanvas.Hex("2b6394"));
            c.Save(ClingWall, 32f);
        }

        /// <summary>Cực quang TÁCH LỚP RIÊNG (để gắn AmbientDrift trôi lững lờ) — 5 dải đa sắc + màn dọc.</summary>
        private static void GenAurora()
        {
            StagePxCanvas.Reseed(111);
            var c = new StagePxCanvas(512, 160, new Color32(0, 0, 0, 0));
            var pink = StagePxCanvas.Hex("ff6ad5"); pink.a = 62;
            var vio = StagePxCanvas.Hex("b070ff"); vio.a = 52;
            var grn = StagePxCanvas.Hex("57e878"); grn.a = 66;
            var teal = StagePxCanvas.Hex("3fd8b0"); teal.a = 70;
            var cyan = StagePxCanvas.Hex("58c8e8"); cyan.a = 50;
            c.SineBand(122f, 10f, 240f, 0.05f, 14, pink);
            c.SineBand(108f, 12f, 200f, 0.35f, 18, teal);
            c.SineBand(90f, 11f, 170f, 0.65f, 15, grn);
            c.SineBand(74f, 9f, 230f, 0.20f, 12, vio);
            c.SineBand(58f, 8f, 260f, 0.80f, 10, cyan);
            for (int i = 0; i < 30; i++)
            {
                int x = StagePxCanvas.RandInt(0, 512);
                int top = 118 - StagePxCanvas.RandInt(0, 24);
                int len = StagePxCanvas.RandInt(26, 64);
                var col = (i % 3 == 0) ? pink : (i % 3 == 1) ? grn : teal;
                for (int k = 0; k < len; k++)
                {
                    var cc = col; cc.a = (byte)(col.a * (1f - (float)k / len) * 0.8f);
                    c.Blend(x, top - k, cc);
                }
            }
            c.Save(Aurora, 16f);
        }

        private static void GenSky()
        {
            var c = new StagePxCanvas(512, 288, StagePxCanvas.Hex("0a0e2a"));
            c.VerticalGradient(StagePxCanvas.Hex("1a2448"), StagePxCanvas.Hex("070a1e"), 0.05f);
            // Sao 3 tầng sáng.
            c.Speckle(90, StagePxCanvas.Hex("9fb4de"), 60, 288);
            c.Speckle(50, StagePxCanvas.Hex("dbe6ff"), 90, 288);
            c.Speckle(18, Color.white, 140, 288, 2);
            // CỰC QUANG RỰC RỠ đa sắc (tham chiếu Summit — Smash Bros): hồng + tím + xanh lá + lam,
            // dải rộng chồng lớp + màn sáng dọc cho có chiều sâu.
            var pink = StagePxCanvas.Hex("ff6ad5"); pink.a = 62;
            var vio = StagePxCanvas.Hex("b070ff"); vio.a = 52;
            var grn = StagePxCanvas.Hex("57e878"); grn.a = 66;
            var teal = StagePxCanvas.Hex("3fd8b0"); teal.a = 70;
            var cyan = StagePxCanvas.Hex("58c8e8"); cyan.a = 50;
            c.SineBand(250f, 10f, 240f, 0.05f, 14, pink);
            c.SineBand(236f, 12f, 200f, 0.35f, 18, teal);
            c.SineBand(218f, 11f, 170f, 0.65f, 15, grn);
            c.SineBand(202f, 9f, 230f, 0.20f, 12, vio);
            c.SineBand(186f, 8f, 260f, 0.80f, 10, cyan);
            // Màn cực quang dọc (curtain) rơi từ dải xuống — alpha nhạt dần.
            for (int i = 0; i < 26; i++)
            {
                int x = StagePxCanvas.RandInt(0, 512);
                int top = 246 - StagePxCanvas.RandInt(0, 24);
                int len = StagePxCanvas.RandInt(24, 62);
                var col = (i % 3 == 0) ? pink : (i % 3 == 1) ? grn : teal;
                for (int k = 0; k < len; k++)
                {
                    var cc = col; cc.a = (byte)(col.a * (1f - (float)k / len) * 0.8f);
                    c.Blend(x, top - k, cc);
                }
            }
            // Trăng + quầng sáng.
            // Trăng HẠ THẤP vào tầm camera mặc định (đứng dưới sàn vẫn thấy).
            var halo = StagePxCanvas.Hex("e8e8c8"); halo.a = 26;
            c.Disc(430, 172, 34, halo);
            c.Disc(430, 172, 27, new Color32(232, 232, 200, 60));
            c.Disc(430, 172, 21, StagePxCanvas.Hex("efedd2"));
            c.Disc(422, 178, 4, StagePxCanvas.Hex("d8d6b4")); // crater
            c.Disc(437, 166, 3, StagePxCanvas.Hex("d8d6b4"));
            c.Save(Sky, 16f);
        }

        private static void GenMountains()
        {
            var c = new StagePxCanvas(512, 160, new Color32(0, 0, 0, 0));
            // Dãy xa (sáng hơn, thấp) rồi dãy gần (đậm, cao) — tương phản mạnh với trời.
            c.MountainRidge(0, 46, 92, 7, StagePxCanvas.Hex("3b4584"));
            // Chóp tuyết dãy xa.
            c.Speckle(300, new Color32(210, 224, 244, 130), 70, 130);
            c.MountainRidge(0, 24, 66, 9, StagePxCanvas.Hex("272f63"));
            c.Speckle(220, new Color32(196, 212, 238, 100), 34, 80);
            c.Save(Mountains, 16f);
        }

        private static void GenPines()
        {
            var c = new StagePxCanvas(512, 96, new Color32(0, 0, 0, 0));
            var dark = StagePxCanvas.Hex("0c1428");
            var dark2 = StagePxCanvas.Hex("111a33");
            // Nền đồi thấp.
            c.MountainRidge(0, 6, 18, 12, dark2);
            // Hàng thông silhouette cao thấp xen kẽ.
            for (int x = 6; x < 512; x += StagePxCanvas.RandInt(18, 34))
            {
                int h = StagePxCanvas.RandInt(26, 58);
                int baseY = StagePxCanvas.RandInt(0, 10);
                int tiers = 3;
                for (int t = 0; t < tiers; t++)
                {
                    int ty = baseY + (h / tiers) * t;
                    int bw = Mathf.RoundToInt((h * 0.55f) * (1f - t * 0.28f));
                    c.Triangle(x, ty, bw, h - (h / tiers) * t, dark);
                }
                c.FillRect(x - 1, baseY, 3, 4, dark); // gốc
            }
            c.Save(Pines, 16f);
        }

        private static void GenTiles()
        {
            // FEEDBACK TEST (mới): TOÀN BỘ SÀN LÀ BĂNG — bỏ hẳn đất nâu. 2 variant mặt = băng xanh
            // tuyết phủ; TileGloss = mảng băng TRẦN bóng loáng làm điểm nhấn. Tile KHÍT, không viền khối.
            GenIceTop(TileTop, 11);
            GenIceTop(TileTopB, 77);

            // Ô THUẦN BĂNG bóng loáng (đặt thành mảng trượt riêng, gán IceSurface).
            var g = new StagePxCanvas(64, 64, StagePxCanvas.Hex("9fd4ef"));
            g.VerticalGradient(StagePxCanvas.Hex("6aaad2"), StagePxCanvas.Hex("bfe6fa"), 0.04f);
            var sheen = new Color32(255, 255, 255, 170);
            for (int i = 0; i < 6; i++)
            {
                int x = StagePxCanvas.RandInt(2, 52), y = StagePxCanvas.RandInt(6, 54);
                for (int k = 0; k < StagePxCanvas.RandInt(8, 16); k++) g.Blend(x + k, y + k / 2, sheen);
            }
            var crack = new Color32(70, 120, 160, 140);
            for (int i = 0; i < 3; i++)
            {
                int x = StagePxCanvas.RandInt(6, 50), y = StagePxCanvas.RandInt(8, 52);
                for (int k = 0; k < StagePxCanvas.RandInt(8, 14); k++) g.Blend(x + k, y - (k % 3 == 0 ? 1 : 0), crack);
            }
            g.FillRect(0, 60, 64, 4, new Color32(224, 244, 255, 255)); // mặt băng sáng
            g.Save(TileGloss, 64f);

            // 2 variant lòng sàn: BĂNG SÂU xanh đậm (bọt khí + vết nứt) — không còn đất nâu.
            GenIceInner(TileInner, 23);
            GenIceInner(TileInnerB, 91);
        }

        /// <summary>Ô mặt sàn: THÂN BĂNG xanh (cùng tông platform) + tuyết phủ trên — hết đất nâu.</summary>
        private static void GenIceTop(string path, int seed)
        {
            StagePxCanvas.Reseed(seed);
            var t = new StagePxCanvas(64, 64, StagePxCanvas.Hex("3a7cb0"));
            t.VerticalGradient(StagePxCanvas.Hex("2b6394"), StagePxCanvas.Hex("58a0cc"), 0.05f);
            IceBody(t, 0, 46);
            var crack = new Color32(30, 70, 104, 150); // vết nứt băng chéo mảnh
            for (int i = 0; i < 3; i++)
            {
                int x = StagePxCanvas.RandInt(4, 52), y = StagePxCanvas.RandInt(6, 40);
                for (int k = 0; k < StagePxCanvas.RandInt(8, 14); k++)
                    t.Blend(x + k, y - (k % 3 == 0 ? 1 : 0), crack);
            }
            t.FillRect(0, 50, 64, 14, StagePxCanvas.Hex("eef6ff"));               // tuyết phủ
            t.FillRect(0, 47, 64, 3, StagePxCanvas.Hex("c9e2f4"));                // mép tuyết
            for (int i = 0; i < 7; i++)                                            // mô tuyết nhấp nhô
                t.Disc(StagePxCanvas.RandInt(2, 62), 62, StagePxCanvas.RandInt(2, 5), Color.white);
            for (int i = 0; i < 4; i++)                                            // tuyết rủ xuống thân băng
                t.VLine(StagePxCanvas.RandInt(3, 61), 47, 47 - StagePxCanvas.RandInt(2, 6), StagePxCanvas.Hex("dceffb"));
            t.Save(path, 64f);
        }

        /// <summary>Ô lòng sàn: BĂNG SÂU xanh đậm — bọt khí li ti + highlight chéo, ghép khít không viền.</summary>
        private static void GenIceInner(string path, int seed)
        {
            StagePxCanvas.Reseed(seed);
            var c = new StagePxCanvas(64, 64, StagePxCanvas.Hex("2b5f8e"));
            c.VerticalGradient(StagePxCanvas.Hex("1e4668"), StagePxCanvas.Hex("34719f"), 0.05f);
            IceBody(c, 0, 64);
            var bubble = new Color32(214, 238, 252, 110); // bọt khí kẹt trong băng
            for (int i = 0; i < 9; i++)
                c.Disc(StagePxCanvas.RandInt(3, 61), StagePxCanvas.RandInt(3, 61),
                       StagePxCanvas.RandInt(1, 3), bubble);
            c.Save(path, 64f);
        }

        private static void IceBody(StagePxCanvas c, int y0, int h)
        {
            // Vệt highlight chéo + đốm tối cho chất băng.
            var hi = StagePxCanvas.Hex("7ec5e8"); hi.a = 150;
            var lo = StagePxCanvas.Hex("2b5f8e"); lo.a = 170;
            for (int i = 0; i < 5; i++)
            {
                int x = StagePxCanvas.RandInt(2, 60);
                int y = y0 + StagePxCanvas.RandInt(2, Mathf.Max(3, h - 12));
                for (int k = 0; k < StagePxCanvas.RandInt(4, 9); k++) c.Blend(x + k, y + k, hi);
            }
            for (int i = 0; i < 7; i++)
                c.Blend(StagePxCanvas.RandInt(0, 64), y0 + StagePxCanvas.RandInt(0, h), lo);
        }

        private static void GenPlatform()
        {
            var c = new StagePxCanvas(256, 96, new Color32(0, 0, 0, 0));
            // Thân slab băng dày (y 34..76), tuyết phủ (76..92) — viền tối cho khối rõ.
            c.FillRect(4, 34, 248, 42, StagePxCanvas.Hex("3a7cb0"));
            c.FillRect(0, 70, 256, 6, StagePxCanvas.Hex("bcdcf2"));
            c.FillRect(0, 76, 256, 14, StagePxCanvas.Hex("eef6ff"));
            for (int i = 0; i < 10; i++)
                c.Disc(StagePxCanvas.RandInt(8, 248), 89, StagePxCanvas.RandInt(2, 5), Color.white);
            var hi = StagePxCanvas.Hex("7ec5e8"); hi.a = 180;
            for (int i = 0; i < 12; i++)
            {
                int x = StagePxCanvas.RandInt(8, 240), y = StagePxCanvas.RandInt(38, 64);
                for (int k = 0; k < StagePxCanvas.RandInt(5, 10); k++) c.Blend(x + k, y + k, hi);
            }
            c.HLine(34, 4, 251, StagePxCanvas.Hex("1e4668"));
            c.FillRect(4, 34, 3, 42, StagePxCanvas.Hex("245a86"));
            c.FillRect(249, 34, 3, 42, StagePxCanvas.Hex("245a86"));
            // Nhũ băng dưới đáy.
            int[] xs = { 30, 78, 128, 176, 224 };
            foreach (int x in xs)
                c.TriangleDown(x, 40, StagePxCanvas.RandInt(10, 16), StagePxCanvas.RandInt(18, 34), StagePxCanvas.Hex("3d7fb0"));
            c.Save(Platform, 64f);
        }

        private static void GenWall()
        {
            var c = new StagePxCanvas(64, 320, StagePxCanvas.Hex("3a7cb0"));
            WallDetail(c);
            c.Save(Wall, 64f);
        }

        private static void WallDetail(StagePxCanvas c)
        {
            // Cột băng: viền tối, highlight dọc, vết nứt ngang.
            c.FillRect(0, 0, 4, 320, StagePxCanvas.Hex("2b5f8e"));
            c.FillRect(60, 0, 4, 320, StagePxCanvas.Hex("2b5f8e"));
            c.FillRect(8, 0, 6, 320, new Color32(126, 197, 232, 150));
            // Chỏm tuyết trên đầu.
            c.FillRect(0, 308, 64, 12, StagePxCanvas.Hex("eef6ff"));
            for (int i = 0; i < 7; i++)
            {
                int y = StagePxCanvas.RandInt(20, 290);
                int x0 = StagePxCanvas.RandInt(6, 20);
                var crack = StagePxCanvas.Hex("1e4668"); crack.a = 190;
                for (int k = 0; k < StagePxCanvas.RandInt(10, 26); k++)
                    c.Blend(x0 + k, y + (k % 5 == 0 ? 1 : 0), crack);
            }
        }

        private static void GenIcicle()
        {
            var c = new StagePxCanvas(32, 64, new Color32(0, 0, 0, 0));
            c.TriangleDown(16, 63, 22, 60, StagePxCanvas.Hex("3d7fb0"));
            c.TriangleDown(13, 63, 8, 34, new Color32(126, 197, 232, 200));
            c.Save(Icicle, 64f);
        }

        /// <summary>Núi băng khổng lồ nền sau (kiểu Summit): khối tròn hữu cơ, đỉnh nhô, băng phân tầng.</summary>
        private static void GenIceberg()
        {
            StagePxCanvas.Reseed(707);
            var c = new StagePxCanvas(512, 300, new Color32(0, 0, 0, 0));
            // Tông LẠNH + TỐI hơn mặt sàn để tách lớp xa-gần (núi = hậu cảnh).
            var body = StagePxCanvas.Hex("a9c2dc");
            var shade = StagePxCanvas.Hex("7f9fc4");
            var deep = StagePxCanvas.Hex("54749e");
            // Silhouette: 2 parabol chồng nhau tạo đỉnh chính lệch trái + vai phải thấp.
            for (int x = 0; x < 512; x++)
            {
                float t1 = (x - 200f) / 240f;
                float t2 = (x - 380f) / 200f;
                float h1 = 290f * (1f - t1 * t1);
                float h2 = 180f * (1f - t2 * t2);
                int h = Mathf.RoundToInt(Mathf.Max(0f, Mathf.Max(h1, h2)) + StagePxCanvas.RandInt(-2, 3));
                for (int y = 0; y < h && y < 300; y++)
                {
                    // Phân tầng màu theo độ cao + vệt dọc cho chất băng tảng.
                    Color32 col = y > h - 26 ? body : (y > h - 90 ? shade : deep);
                    c.Set(x, y, col);
                }
            }
            // Vệt nứt/khe băng dọc sườn.
            var crack = new Color32(90, 130, 170, 150);
            for (int i = 0; i < 22; i++)
            {
                int x = StagePxCanvas.RandInt(30, 480);
                int y0 = StagePxCanvas.RandInt(20, 200);
                int len = StagePxCanvas.RandInt(20, 70);
                for (int k = 0; k < len; k++)
                    c.Blend(x + (k / 14), y0 + k, crack);
            }
            // Mảng tuyết sáng loang trên đỉnh.
            c.Speckle(500, new Color32(255, 255, 255, 60), 140, 300);
            c.Speckle(240, new Color32(255, 255, 255, 90), 210, 300);
            c.Save(Iceberg, 16f);
        }

        /// <summary>Mặt trước sàn: khối băng hữu cơ (thay mặt tile phẳng) — mép lượn tròn, nứt, nhũ đá.</summary>
        private static void GenFloorFace()
        {
            StagePxCanvas.Reseed(808);
            var c = new StagePxCanvas(960, 176, new Color32(0, 0, 0, 0)); // 30×5.5 unit @32ppu
            var body = StagePxCanvas.Hex("6ea6cf");
            var lite = StagePxCanvas.Hex("8fc0e0");
            var dark = StagePxCanvas.Hex("40719e");
            var deep = StagePxCanvas.Hex("2b527c");
            for (int x = 0; x < 960; x++)
            {
                // Vai tròn 2 đầu: chiều cao giảm dần theo parabol ở 90px mép.
                float edge = 1f;
                if (x < 90) edge = Mathf.Sqrt(x / 90f);
                else if (x > 869) edge = Mathf.Sqrt((959 - x) / 90f);
                int top = 160 + StagePxCanvas.RandInt(-2, 3);           // mép trên lượn nhẹ (nằm dưới lớp tuyết tile)
                int h = Mathf.RoundToInt(top * edge);
                int yBot = 160 - h;
                for (int y = yBot; y < top && y < 176; y++)
                {
                    float d = (float)(y - yBot) / Mathf.Max(1, h);      // 0 đáy → 1 mặt
                    Color32 col = d > 0.75f ? lite : d > 0.45f ? body : d > 0.2f ? dark : deep;
                    c.Set(x, y, col);
                }
            }
            // Vân băng ngang + nứt chéo.
            var vein = new Color32(190, 224, 244, 70);
            for (int i = 0; i < 16; i++)
                c.HLine(StagePxCanvas.RandInt(30, 150), StagePxCanvas.RandInt(0, 700),
                        StagePxCanvas.RandInt(120, 260) + 700, vein);
            var crack = new Color32(30, 60, 95, 170);
            for (int i = 0; i < 14; i++)
            {
                int x = StagePxCanvas.RandInt(40, 900), y = StagePxCanvas.RandInt(20, 150);
                for (int k = 0; k < StagePxCanvas.RandInt(14, 34); k++)
                    c.Blend(x + (k % 3 == 0 ? 1 : 0), y - k, crack);
            }
            // Nhũ băng rủ dưới đáy dọc theo vai tròn.
            for (int i = 0; i < 26; i++)
            {
                int x = StagePxCanvas.RandInt(60, 900);
                c.TriangleDown(x, 14 + StagePxCanvas.RandInt(0, 8), StagePxCanvas.RandInt(6, 14),
                               StagePxCanvas.RandInt(10, 26), deep);
            }
            c.Save(FloorFace, 32f);
        }

        /// <summary>Cụm gai băng trang trí (như Summit) — 1 cụm nhiều chóp cao thấp.</summary>
        private static void GenSpikes()
        {
            StagePxCanvas.Reseed(909);
            var c = new StagePxCanvas(96, 64, new Color32(0, 0, 0, 0));
            int[] xs = { 12, 26, 40, 55, 70, 84 };
            foreach (int x in xs)
            {
                int h = StagePxCanvas.RandInt(22, 58);
                int w = StagePxCanvas.RandInt(10, 16);
                c.Triangle(x, 0, w, h, StagePxCanvas.Hex("bfe0f2"));
                c.Triangle(x - 2, 0, w / 2, (int)(h * 0.8f), new Color32(255, 255, 255, 170)); // mặt sáng
            }
            c.Save(Spikes, 32f);
        }
    }
}
