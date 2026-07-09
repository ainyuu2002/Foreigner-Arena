using UnityEngine;

namespace FA.EditorTools
{
    /// <summary>
    /// Sinh art pixel map ĐẦM LẦY "Rừng Sương Độc" (design 02): trời đêm xanh độc + trăng mờ + sương,
    /// rừng cây silhouette + dây leo rủ, nước đầm phản chiếu, bờ đất cỏ + nấm phát sáng. PPU nền = 16, tile = 64.
    /// </summary>
    internal static class StageArtSwamp
    {
        public const string Dir = "Assets/_Project/Art/Maps/Swamp";
        public const string Sky = Dir + "/Swamp_Sky.png";
        public const string Trees = Dir + "/Swamp_Trees.png";
        public const string Vines = Dir + "/Swamp_Vines.png";
        public const string Water = Dir + "/Swamp_Water.png";
        public const string TileTop = Dir + "/Swamp_TileTop.png";
        public const string TileTopB = Dir + "/Swamp_TileTopB.png";
        public const string TileTopMud = Dir + "/Swamp_TileTopMud.png";
        public const string TileInner = Dir + "/Swamp_TileInner.png";
        public const string TileInnerB = Dir + "/Swamp_TileInnerB.png";
        public const string Platform = Dir + "/Swamp_Platform.png";
        public const string Mushroom = Dir + "/Swamp_Mushroom.png";
        public const string Cattail = Dir + "/Swamp_Cattail.png";
        public const string FloorFace = Dir + "/Swamp_FloorFace.png";
        public const string BigTree = Dir + "/Swamp_BigTree.png";
        public const string Roots = Dir + "/Swamp_Roots.png";

        public static void Generate()
        {
            StagePxCanvas.Reseed(202);
            GenSky();
            GenTrees();
            GenVines();
            GenWater();
            GenTiles();
            GenPlatform();
            GenDecor();
            GenFloorFace();
            GenBigTree();
            GenRoots();
        }

        /// <summary>Mặt bờ đất hữu cơ (kiểu Distant Planet): vai tròn, tầng đất-rêu, rễ cây rủ xuống nước.</summary>
        private static void GenFloorFace()
        {
            StagePxCanvas.Reseed(404);
            var c = new StagePxCanvas(448, 160, new Color32(0, 0, 0, 0)); // 14×5 unit @32ppu
            var moss = StagePxCanvas.Hex("3a6a34");
            var soil = StagePxCanvas.Hex("41301f");
            var soilD = StagePxCanvas.Hex("2c1f13");
            var deep = StagePxCanvas.Hex("1c130c");
            for (int x = 0; x < 448; x++)
            {
                float edge = 1f;
                if (x > 383) edge = Mathf.Sqrt((447 - x) / 64f);   // vai tròn 1 đầu (đầu kia chạm biên map)
                int top = 146 + StagePxCanvas.RandInt(-2, 2);
                int h = Mathf.RoundToInt(top * edge);
                int yBot = top - h + StagePxCanvas.RandInt(0, 6);
                for (int y = yBot; y < top && y < 160; y++)
                {
                    float d = (float)(y - yBot) / Mathf.Max(1, top - yBot);
                    c.Set(x, y, d > 0.86f ? moss : d > 0.5f ? soil : d > 0.22f ? soilD : deep);
                }
            }
            // Đốm rêu loang + đá cuội trong đất.
            var mossHi = new Color32(87, 168, 68, 120);
            for (int i = 0; i < 24; i++)
                c.Disc(StagePxCanvas.RandInt(10, 430), StagePxCanvas.RandInt(100, 145),
                       StagePxCanvas.RandInt(2, 6), mossHi);
            for (int i = 0; i < 10; i++)
                c.Disc(StagePxCanvas.RandInt(16, 420), StagePxCanvas.RandInt(40, 110),
                       StagePxCanvas.RandInt(3, 6), StagePxCanvas.Hex("4a4038"));
            // Rễ cây ngoằn ngoèo rủ khỏi đáy bờ.
            var root = StagePxCanvas.Hex("2a1d10");
            for (int i = 0; i < 14; i++)
            {
                int x = StagePxCanvas.RandInt(20, 420);
                int len = StagePxCanvas.RandInt(16, 44);
                int sway = StagePxCanvas.RandInt(-1, 2);
                for (int k = 0; k < len; k++)
                {
                    c.Blend(x + (k / 7) * sway, 26 - k > 0 ? 26 - k : 0 + (26 - k), root);
                    c.Blend(x + (k / 7) * sway, Mathf.Max(0, 26 - k), root);
                }
            }
            c.Save(FloorFace, 32f);
        }

        /// <summary>Cây cổ thụ KHỔNG LỒ nền sau (điểm nhấn kiểu Pikmin/Distant Planet).</summary>
        private static void GenBigTree()
        {
            StagePxCanvas.Reseed(303);
            var c = new StagePxCanvas(320, 320, new Color32(0, 0, 0, 0)); // 20×20 unit @16ppu
            var trunk = StagePxCanvas.Hex("14241a");
            var trunkHi = StagePxCanvas.Hex("1e3626");
            var canopy = StagePxCanvas.Hex("1e4028");
            var canopyHi = StagePxCanvas.Hex("2a5434");
            var canopyLite = StagePxCanvas.Hex("38683c");
            // Thân cây to bè gốc, thuôn dần lên.
            for (int y = 0; y < 210; y++)
            {
                float t = y / 210f;
                int half = Mathf.RoundToInt(Mathf.Lerp(46f, 14f, Mathf.Pow(t, 0.6f)));
                int cx = 160 + Mathf.RoundToInt(Mathf.Sin(t * 3.1f) * 8f); // thân hơi cong
                for (int x = cx - half; x <= cx + half; x++)
                    c.Set(x, y, x < cx - half / 3 ? trunkHi : trunk);
            }
            // Rễ bạnh gốc.
            for (int i = -3; i <= 3; i++)
            {
                if (i == 0) continue;
                int bx = 160 + i * 34;
                c.Triangle(bx, 0, 40, 34, trunk);
            }
            // Tán khổng lồ nhiều tầng cầu.
            for (int k = 0; k < 16; k++)
                c.Disc(160 + StagePxCanvas.RandInt(-90, 91), 240 + StagePxCanvas.RandInt(-28, 42),
                       StagePxCanvas.RandInt(26, 46), canopy);
            for (int k = 0; k < 10; k++)
                c.Disc(150 + StagePxCanvas.RandInt(-70, 71), 258 + StagePxCanvas.RandInt(-18, 34),
                       StagePxCanvas.RandInt(16, 28), canopyHi);
            for (int k = 0; k < 6; k++)
                c.Disc(140 + StagePxCanvas.RandInt(-50, 51), 274 + StagePxCanvas.RandInt(-10, 22),
                       StagePxCanvas.RandInt(8, 16), canopyLite);
            // Đom đóm đậu quanh tán.
            c.Speckle(30, new Color32(184, 232, 120, 190), 210, 320);
            c.Save(BigTree, 16f);
        }

        /// <summary>Chùm rễ rủ dưới bệ chìm giữa (treo lơ lửng trên mặt nước).</summary>
        private static void GenRoots()
        {
            StagePxCanvas.Reseed(202);
            var c = new StagePxCanvas(128, 64, new Color32(0, 0, 0, 0)); // 4×2 unit @32ppu
            var root = StagePxCanvas.Hex("2a1d10");
            var rootHi = StagePxCanvas.Hex("3d2c18");
            for (int i = 0; i < 12; i++)
            {
                int x = StagePxCanvas.RandInt(6, 122);
                int len = StagePxCanvas.RandInt(20, 60);
                int sway = StagePxCanvas.RandInt(-1, 2);
                for (int k = 0; k < len; k++)
                    c.Blend(x + (k / 8) * sway, 63 - k, k % 5 == 0 ? rootHi : root);
            }
            c.Save(Roots, 32f);
        }

        private static void GenSky()
        {
            var c = new StagePxCanvas(512, 288, StagePxCanvas.Hex("0a140c"));
            c.VerticalGradient(StagePxCanvas.Hex("16281a"), StagePxCanvas.Hex("070f09"), 0.05f);
            // Vài ngôi sao mờ xuyên tán rừng.
            c.Speckle(40, new Color32(159, 190, 160, 120), 150, 288);
            // Dải sương độc ngang trời.
            var fog1 = StagePxCanvas.Hex("2a4a34"); fog1.a = 60;
            var fog2 = StagePxCanvas.Hex("3a5e42"); fog2.a = 40;
            c.SineBand(120f, 6f, 260f, 0.2f, 18, fog1);
            c.SineBand(170f, 8f, 300f, 0.7f, 14, fog2);
            c.SineBand(80f, 5f, 220f, 0.45f, 12, fog1);
            // Trăng xanh nhạt + quầng.
            c.Disc(120, 226, 30, new Color32(216, 232, 192, 26));
            c.Disc(120, 226, 22, new Color32(216, 232, 192, 60));
            c.Disc(120, 226, 17, StagePxCanvas.Hex("d8e8c0"));
            c.Save(Sky, 16f);
        }

        private static void GenTrees()
        {
            var c = new StagePxCanvas(512, 176, new Color32(0, 0, 0, 0));
            var trunkFar = StagePxCanvas.Hex("101f14");
            var trunkNear = StagePxCanvas.Hex("0c1810");
            var canopy = StagePxCanvas.Hex("1e4028");
            var canopyHi = StagePxCanvas.Hex("2a5434");
            // Hàng thân cây chết mảnh (xa).
            for (int i = 0; i < 22; i++)
            {
                int x = StagePxCanvas.RandInt(0, 512);
                int h = StagePxCanvas.RandInt(50, 130);
                int w = StagePxCanvas.RandInt(2, 5);
                c.FillRect(x, 0, w, h, trunkFar);
                if (StagePxCanvas.Rand01() < 0.5f) // cành khẳng khiu
                    c.HLine(h - StagePxCanvas.RandInt(6, 20), x - StagePxCanvas.RandInt(4, 10), x, trunkFar);
            }
            // Cây cổ thụ tán tròn bên phải (theo design 02).
            c.FillRect(420, 0, 14, 120, trunkNear);
            c.HLine(88, 408, 424, trunkNear);
            c.HLine(70, 434, 452, trunkNear);
            for (int k = 0; k < 10; k++)
                c.Disc(430 + StagePxCanvas.RandInt(-34, 35), 128 + StagePxCanvas.RandInt(-16, 22),
                       StagePxCanvas.RandInt(14, 26), canopy);
            for (int k = 0; k < 6; k++)
                c.Disc(428 + StagePxCanvas.RandInt(-28, 29), 138 + StagePxCanvas.RandInt(-10, 16),
                       StagePxCanvas.RandInt(8, 14), canopyHi);
            // Cụm cây vừa bên trái.
            c.FillRect(52, 0, 9, 92, trunkNear);
            for (int k = 0; k < 6; k++)
                c.Disc(56 + StagePxCanvas.RandInt(-20, 21), 98 + StagePxCanvas.RandInt(-10, 14),
                       StagePxCanvas.RandInt(10, 18), canopy);
            // Sương mù thấp phủ gốc cây.
            var fog = StagePxCanvas.Hex("2a4a34"); fog.a = 80;
            c.BlendRect(0, 0, 512, 26, fog);
            c.Save(Trees, 16f);
        }

        private static void GenVines()
        {
            var c = new StagePxCanvas(512, 120, new Color32(0, 0, 0, 0));
            var vine = StagePxCanvas.Hex("2a5434");
            var leaf = StagePxCanvas.Hex("3a7444");
            for (int i = 0; i < 16; i++)
            {
                int x = StagePxCanvas.RandInt(8, 504);
                int len = StagePxCanvas.RandInt(30, 110);
                int sway = StagePxCanvas.RandInt(0, 3) - 1;
                for (int k = 0; k < len; k++)
                {
                    int xx = x + (k / 22) * sway;
                    c.Blend(xx, 119 - k, vine);
                    if (k % 9 == 4) { c.Blend(xx - 1, 119 - k, leaf); c.Blend(xx + 1, 119 - k - 1, leaf); }
                }
                if (StagePxCanvas.Rand01() < 0.4f) // đốm đom đóm đậu trên dây
                    c.Disc(x + sway * 2, 119 - len + 2, 1, StagePxCanvas.Hex("b8e878"));
            }
            c.Save(Vines, 16f);
        }

        private static void GenWater()
        {
            var c = new StagePxCanvas(512, 96, StagePxCanvas.Hex("0f2018"));
            c.VerticalGradient(StagePxCanvas.Hex("0a1710"), StagePxCanvas.Hex("15291d"), 0.05f);
            // Sóng phản chiếu ngang.
            for (int i = 0; i < 22; i++)
            {
                int y = StagePxCanvas.RandInt(6, 90);
                int x0 = StagePxCanvas.RandInt(0, 440);
                var stripe = StagePxCanvas.Hex("1e3828"); stripe.a = 200;
                c.HLine(y, x0, x0 + StagePxCanvas.RandInt(20, 70), stripe);
            }
            // Vệt phản chiếu trăng loang nhẹ.
            var moonRef = new Color32(216, 232, 192, 60);
            for (int i = 0; i < 8; i++)
                c.HLine(84 - i * 9, 104 + StagePxCanvas.RandInt(-8, 9), 136 + StagePxCanvas.RandInt(-8, 9), moonRef);
            // Mặt nước viền sáng.
            c.FillRect(0, 92, 512, 4, StagePxCanvas.Hex("2a4a34"));
            c.Save(Water, 16f);
        }

        private static void GenTiles()
        {
            // FEEDBACK TEST: đất đầm sáng tạo hơn — 3 variant mặt (cỏ / cỏ+nấm con / BÙN ƯỚT lầy rõ rệt),
            // 2 variant lòng đất, KHÔNG viền khối (tile ghép khít).
            GenGrassTop(TileTop, 41, false);
            GenGrassTop(TileTopB, 87, true);

            // Ô BÙN ƯỚT: mặt bùn nhão bóng nước, sủi tăm — điểm nhấn "đầm lầy thật".
            StagePxCanvas.Reseed(63);
            var m = new StagePxCanvas(64, 64, StagePxCanvas.Hex("3a2a1e"));
            MudDetail(m);
            m.FillRect(0, 56, 64, 8, StagePxCanvas.Hex("2b2016"));               // lớp bùn nhão sẫm
            var wet = new Color32(90, 116, 84, 200);                              // váng nước xanh rêu
            for (int i = 0; i < 5; i++)
                m.Disc(StagePxCanvas.RandInt(6, 58), 60, StagePxCanvas.RandInt(3, 7), wet);
            var glint = new Color32(190, 220, 190, 160);                          // ánh nước bóng
            for (int i = 0; i < 8; i++)
                m.HLine(StagePxCanvas.RandInt(57, 63), StagePxCanvas.RandInt(2, 40),
                        StagePxCanvas.RandInt(6, 20) + 40, glint);
            for (int i = 0; i < 4; i++)                                            // bong bóng sủi tăm
                m.Ring(StagePxCanvas.RandInt(8, 56), 60, 2, 1, new Color32(210, 230, 200, 150));
            m.Save(TileTopMud, 64f);

            GenMudInner(TileInner, 19);
            GenMudInner(TileInnerB, 111);
        }

        private static void GenGrassTop(string path, int seed, bool mushrooms)
        {
            StagePxCanvas.Reseed(seed);
            var t = new StagePxCanvas(64, 64, StagePxCanvas.Hex("3a2a1e"));
            MudDetail(t);
            t.FillRect(0, 58, 64, 6, StagePxCanvas.Hex("4a8a3a"));
            t.FillRect(0, 61, 64, 3, StagePxCanvas.Hex("57a844"));
            for (int x = 2; x < 64; x += StagePxCanvas.RandInt(4, 9))
                t.VLine(x, 58, 58 - StagePxCanvas.RandInt(2, 6), StagePxCanvas.Hex("4a8a3a"));
            if (mushrooms) // nấm con lấm tấm trên cỏ
                for (int i = 0; i < 3; i++)
                {
                    int x = StagePxCanvas.RandInt(6, 58);
                    t.FillRect(x, 62, 2, 2, StagePxCanvas.Hex("d8e8c0"));
                    t.Disc(x + 1, 63, 2, StagePxCanvas.Hex("7ad8a0"));
                }
            t.Save(path, 64f);
        }

        private static void GenMudInner(string path, int seed)
        {
            StagePxCanvas.Reseed(seed);
            var c = new StagePxCanvas(64, 64, StagePxCanvas.Hex("2c1f15"));
            MudDetail(c);
            // Đá cuội ướt lấp lánh cho đỡ trơ.
            for (int i = 0; i < 3; i++)
            {
                int x = StagePxCanvas.RandInt(6, 54), y = StagePxCanvas.RandInt(8, 52);
                c.Disc(x, y, StagePxCanvas.RandInt(3, 5), StagePxCanvas.Hex("4a4038"));
                c.Blend(x - 1, y + 1, new Color32(150, 170, 150, 150));
            }
            c.Save(path, 64f);
        }

        private static void MudDetail(StagePxCanvas c)
        {
            var dark = StagePxCanvas.Hex("24180f");
            var root = StagePxCanvas.Hex("1c130c");
            for (int i = 0; i < 12; i++)
                c.BlendRect(StagePxCanvas.RandInt(0, 58), StagePxCanvas.RandInt(0, 54),
                            StagePxCanvas.RandInt(3, 8), StagePxCanvas.RandInt(2, 4), dark);
            for (int i = 0; i < 3; i++) // rễ cây len lỏi
            {
                int x = StagePxCanvas.RandInt(6, 56), y = StagePxCanvas.RandInt(8, 50);
                for (int k = 0; k < StagePxCanvas.RandInt(8, 16); k++)
                    c.Blend(x + k, y - (k % 3 == 0 ? 1 : 0), root);
            }
        }

        private static void GenPlatform()
        {
            var c = new StagePxCanvas(256, 80, new Color32(0, 0, 0, 0));
            // Bệ đất treo rêu: cỏ trên, đất giữa, rễ rủ dưới.
            c.FillRect(4, 30, 248, 28, StagePxCanvas.Hex("3a2a1e"));
            c.FillRect(0, 52, 256, 6, StagePxCanvas.Hex("4a8a3a"));
            c.FillRect(0, 56, 256, 4, StagePxCanvas.Hex("57a844"));
            c.HLine(51, 0, 255, StagePxCanvas.Hex("24180f"));
            var moss = StagePxCanvas.Hex("3a7444"); moss.a = 200;
            for (int i = 0; i < 14; i++)
                c.Disc(StagePxCanvas.RandInt(8, 248), StagePxCanvas.RandInt(32, 50), StagePxCanvas.RandInt(2, 4), moss);
            // Rễ + giọt rêu rủ xuống.
            for (int i = 0; i < 12; i++)
            {
                int x = StagePxCanvas.RandInt(10, 246);
                c.VLine(x, 30, 30 - StagePxCanvas.RandInt(6, 20), StagePxCanvas.Hex("2a5434"));
            }
            c.Save(Platform, 64f);
        }

        private static void GenDecor()
        {
            // Nấm phát sáng.
            var m = new StagePxCanvas(32, 32, new Color32(0, 0, 0, 0));
            m.Disc(16, 16, 10, new Color32(122, 216, 160, 40));       // quầng sáng
            m.FillRect(14, 6, 4, 10, StagePxCanvas.Hex("d8e8c0"));    // thân
            m.Disc(16, 17, 7, StagePxCanvas.Hex("7ad8a0"));           // mũ
            m.Disc(14, 19, 2, StagePxCanvas.Hex("aef0c8"));           // đốm mũ
            m.Save(Mushroom, 64f);

            // Cỏ nến (cattail).
            var ct = new StagePxCanvas(16, 48, new Color32(0, 0, 0, 0));
            ct.VLine(8, 0, 40, StagePxCanvas.Hex("2a5434"));
            ct.FillRect(6, 32, 5, 12, StagePxCanvas.Hex("5a4028"));
            ct.VLine(11, 20, 34, StagePxCanvas.Hex("2a5434"));
            ct.Save(Cattail, 64f);
        }
    }
}
