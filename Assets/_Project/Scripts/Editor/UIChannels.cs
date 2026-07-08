using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using FA.Core;
using FA.Combat;
using FA.GameFlow;

namespace FA.EditorTools
{
    /// <summary>
    /// PHẦN ASSET của bộ tool dựng UI production (xem <see cref="UIProductionMenu"/>). 1 chức năng: TÌM các
    /// kênh Event Channel (per-player) + registry mà HUD/menu cần wire, theo TÊN trong project (ưu tiên tái
    /// dùng asset đã có ở P7/P8/P12). Chỉ TẠO mới khi thật sự thiếu — hiện chỉ kênh % per-player
    /// (OnPercentChangedChannel_P1/_P2) vì P7 mới có 1 kênh % chung.
    ///
    /// Decoupling §3.3: UI chỉ subscribe/raise kênh Core; KHÔNG ref hệ thống gameplay. Builder gắn các kênh
    /// này vào View/Raiser của FA.UI. EDITOR-ONLY.
    /// </summary>
    internal static class UIChannels
    {
        public const string DataFolder = "Assets/_Project/Data";

        public sealed class Bundle
        {
            // HUD — per player
            public FloatEventChannelSO PercentP1, PercentP2;       // OnPercentChangedChannel_Pn (raw 0..cap)
            public FloatEventChannelSO ManaP1, ManaP2;             // OnManaChangedChannel_Pn (fill 0..1)
            public IntEventChannelSO ManaSegP1, ManaSegP2;         // OnManaSegmentsChangedChannel_Pn (số ô)
            public FloatEventChannelSO RageP1, RageP2;             // OnRageChangedChannel_Pn (fill 0..1)
            public IntEventChannelSO LivesP1, LivesP2;             // OnLivesChangedChannel_Pn
            public IntEventChannelSO ComboP1, ComboP2;             // OnComboChangedChannel_Pn
            public FloatEventChannelSO Timer;                      // OnTimerChangedChannel

            // Menu
            public IntEventChannelSO CharSelP1, CharSelP2;         // CharSelectedChannel_Pn
            public IntEventChannelSO StageSel;                     // StageSelectedChannel
            public MatchResultEventChannelSO MatchResult;         // OnMatchResultChannel
            public VoidEventChannelSO Rematch, BackToCharSelect;  // RematchPressedChannel / BackToCharSelectChannel

            // GameFlow systems (Lobby wire MatchManager/WinCondition/MatchTimer — P12)
            public VoidEventChannelSO MatchEnded;                  // OnMatchEndedChannel (WinCondition → MatchManager)
            public VoidEventChannelSO TimeUp;                      // OnTimeUpChannel (MatchTimer → WinCondition)
            public GameObjectEventChannelSO OutOfLives;            // OnPlayerOutOfLivesChannel (LivesSystem → WinCondition)
            public MatchSettingsSO MatchSettings;                  // matchSeconds/startingLives

            // Registry + config
            public CharacterRegistrySO CharacterRegistry;
            public StageRegistrySO StageRegistry;
            public float PercentCap = 250f;                        // chuẩn hóa % bar (raw → 0..1)

            public readonly List<string> Created = new List<string>();
            public readonly List<string> Missing = new List<string>();

            // Accessor per-slot cho builder (isP1 ? _P1 : _P2).
            public FloatEventChannelSO Percent(bool isP1) => isP1 ? PercentP1 : PercentP2;
            public FloatEventChannelSO Mana(bool isP1) => isP1 ? ManaP1 : ManaP2;
            public IntEventChannelSO ManaSeg(bool isP1) => isP1 ? ManaSegP1 : ManaSegP2;
            public FloatEventChannelSO Rage(bool isP1) => isP1 ? RageP1 : RageP2;
            public IntEventChannelSO Lives(bool isP1) => isP1 ? LivesP1 : LivesP2;
            public IntEventChannelSO Combo(bool isP1) => isP1 ? ComboP1 : ComboP2;
            public IntEventChannelSO CharSel(bool isP1) => isP1 ? CharSelP1 : CharSelP2;
        }

        public static Bundle Resolve()
        {
            var b = new Bundle();

            // % chung P7 chỉ có 1 kênh → tạo per-player để HUD 2 người không lẫn. Ưu tiên kênh đã có nếu user đã tạo.
            b.PercentP1 = FindOrCreateFloat("OnPercentChangedChannel_P1", b);
            b.PercentP2 = FindOrCreateFloat("OnPercentChangedChannel_P2", b);

            b.ManaP1 = Find<FloatEventChannelSO>("OnManaChangedChannel_P1", b);
            b.ManaP2 = Find<FloatEventChannelSO>("OnManaChangedChannel_P2", b);
            b.ManaSegP1 = Find<IntEventChannelSO>("OnManaSegmentsChangedChannel_P1", b);
            b.ManaSegP2 = Find<IntEventChannelSO>("OnManaSegmentsChangedChannel_P2", b);
            b.RageP1 = Find<FloatEventChannelSO>("OnRageChangedChannel_P1", b);
            b.RageP2 = Find<FloatEventChannelSO>("OnRageChangedChannel_P2", b);
            b.LivesP1 = Find<IntEventChannelSO>("OnLivesChangedChannel_P1", b);
            b.LivesP2 = Find<IntEventChannelSO>("OnLivesChangedChannel_P2", b);
            b.ComboP1 = Find<IntEventChannelSO>("OnComboChangedChannel_P1", b);
            b.ComboP2 = Find<IntEventChannelSO>("OnComboChangedChannel_P2", b);
            b.Timer = Find<FloatEventChannelSO>("OnTimerChangedChannel", b);

            b.CharSelP1 = Find<IntEventChannelSO>("CharSelectedChannel_P1", b);
            b.CharSelP2 = Find<IntEventChannelSO>("CharSelectedChannel_P2", b);
            b.StageSel = Find<IntEventChannelSO>("StageSelectedChannel", b);
            b.MatchResult = Find<MatchResultEventChannelSO>("OnMatchResultChannel", b);
            b.Rematch = Find<VoidEventChannelSO>("RematchPressedChannel", b);
            b.BackToCharSelect = Find<VoidEventChannelSO>("BackToCharSelectChannel", b);

            b.MatchEnded = Find<VoidEventChannelSO>("OnMatchEndedChannel", b);
            b.TimeUp = Find<VoidEventChannelSO>("OnTimeUpChannel", b);
            b.OutOfLives = Find<GameObjectEventChannelSO>("OnPlayerOutOfLivesChannel", b);
            b.MatchSettings = FindAny<MatchSettingsSO>();

            b.CharacterRegistry = FindAny<CharacterRegistrySO>();
            b.StageRegistry = FindAny<StageRegistrySO>();

            var kbCfg = FindAny<KnockbackConfigSO>();
            if (kbCfg != null) b.PercentCap = Mathf.Max(1f, kbCfg.percentCap);

            LogResolution(b);
            return b;
        }

        /// <summary>Log chẩn đoán: đánh dấu "v2 name-based" để biết code MỚI có chạy không + từng kênh khớp/NULL.</summary>
        private static void LogResolution(Bundle b)
        {
            string N(Object o) => o != null ? o.name : "NULL";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[UIChannels] Resolve v2 (name-based lookup) — kết quả tra kênh/registry:");
            sb.AppendLine($"  % : P1={N(b.PercentP1)}  P2={N(b.PercentP2)}");
            sb.AppendLine($"  Mana fill: P1={N(b.ManaP1)}  P2={N(b.ManaP2)}  | seg: P1={N(b.ManaSegP1)}  P2={N(b.ManaSegP2)}");
            sb.AppendLine($"  Rage: P1={N(b.RageP1)}  P2={N(b.RageP2)}  | Lives: P1={N(b.LivesP1)}  P2={N(b.LivesP2)}  | Timer={N(b.Timer)}");
            sb.AppendLine($"  Combo: P1={N(b.ComboP1)}  P2={N(b.ComboP2)}");
            sb.AppendLine($"  CharSel: P1={N(b.CharSelP1)}  P2={N(b.CharSelP2)}  | StageSel={N(b.StageSel)}");
            sb.AppendLine($"  MatchResult={N(b.MatchResult)}  Rematch={N(b.Rematch)}  Back={N(b.BackToCharSelect)}");
            sb.AppendLine($"  GameFlow: MatchEnded={N(b.MatchEnded)}  TimeUp={N(b.TimeUp)}  OutOfLives={N(b.OutOfLives)}  Settings={N(b.MatchSettings)}");
            sb.AppendLine($"  Registry: Character={N(b.CharacterRegistry)}  Stage={N(b.StageRegistry)}  | PercentCap={b.PercentCap}");
            Debug.Log(sb.ToString());
        }

        // ───────────────────── helpers ─────────────────────

        private static FloatEventChannelSO FindOrCreateFloat(string assetName, Bundle b)
        {
            var found = FindExact<FloatEventChannelSO>(assetName);
            if (found != null) return found;

            string path = $"{DataFolder}/{assetName}.asset";
            var ch = ScriptableObject.CreateInstance<FloatEventChannelSO>();
            AssetDatabase.CreateAsset(ch, path);
            b.Created.Add(path);
            return ch;
        }

        private static T Find<T>(string assetName, Bundle b) where T : Object
        {
            var asset = FindExact<T>(assetName);
            if (asset == null) b.Missing.Add($"{typeof(T).Name} '{assetName}'");
            return asset;
        }

        /// <summary>
        /// Tìm asset theo TÊN file chính xác — QUÉT FILE System.IO thay vì <c>AssetDatabase.FindAssets</c>.
        /// ROOT-CAUSE (Lobby v3): FindAssets trên máy này trả RỖNG cho .asset (search index không tin được;
        /// cùng 1 lần build tìm THẤY sprite nhưng TRƯỢT toàn bộ kênh) → mọi ref trong scene = null lúc Play
        /// ("OnEnable subscribe P1=False"). Directory.GetFiles + LoadAssetAtPath = tất định, không phụ thuộc
        /// index. Editor-only nên chi phí quét chấp nhận được. FindAssets giữ làm fallback cuối.
        /// </summary>
        private static T FindExact<T>(string assetName) where T : Object
        {
            // v9: quét qua UIKit.FindProjectFiles (đường dẫn TUYỆT ĐỐI) — bản tương đối cũ chết theo CWD.
            foreach (string file in UIKit.FindProjectFiles(assetName + ".asset"))
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(file);
                if (a != null) return a;
            }
            // Fallback: FindAssets (nếu asset nằm ngoài _Project hoặc tên chỉ khớp một phần).
            foreach (string guid in AssetDatabase.FindAssets(assetName))
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null) return a;
            }
            return null;
        }

        private static T FindAny<T>() where T : Object
        {
            // Quét System.IO trước (lý do xem FindExact) — mọi .asset dưới _Project, load đúng T là nhận.
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
