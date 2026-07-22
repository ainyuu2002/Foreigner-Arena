using UnityEngine;
using UnityEngine.UI;
using FA.Core;

namespace FA.Items
{
    /// <summary>Hiển thị các trạng thái item của hai người chơi.</summary>
    [DisallowMultipleComponent]
    public sealed class ItemBuffHudView : MonoBehaviour
    {
        [Header("Status sprites")]
        [SerializeField] private Sprite damageBuffSprite;
        [SerializeField] private Sprite slowFallSprite;
        [SerializeField] private Font labelFont;
        [SerializeField] private GameObject statusIconPrefab;

        [Header("Layout above mana")]
        [SerializeField] private Vector2 p1Position = new Vector2(28f, 68f);
        [SerializeField] private Vector2 p2Position = new Vector2(-28f, 68f);
        [Min(24f)] [SerializeField] private float iconSize = 66f;
        [Min(0f)] [SerializeField] private float spacing = 8f;
        [Min(8)] [SerializeField] private int percentFontSize = 15;
        [Range(0f, 1f)] [SerializeField] private float cooldownShadeOpacity = 0.62f;

        private Transform _playerP1;
        private Transform _playerP2;
        private readonly PlayerStatuses _statusP1 = new PlayerStatuses();
        private readonly PlayerStatuses _statusP2 = new PlayerStatuses();
        private StatusRow _rowP1;
        private StatusRow _rowP2;
        private float _nextPlayerLookup;
        private float _nextStatusLookup;

        private void Awake()
        {
            RectTransform parent = transform as RectTransform;
            if (parent == null) return;

            _rowP1 = CreateRow(parent, "ItemBuffs_P1", false, p1Position);
            _rowP2 = CreateRow(parent, "ItemBuffs_P2", true, p2Position);
        }

        private void Update()
        {
            if ((_playerP1 == null || _playerP2 == null) && Time.unscaledTime >= _nextPlayerLookup)
            {
                _nextPlayerLookup = Time.unscaledTime + 0.5f;
                if (_playerP1 == null) _playerP1 = FindPlayerRoot(GameLayers.Player1);
                if (_playerP2 == null) _playerP2 = FindPlayerRoot(GameLayers.Player2);
                _statusP1.SetPlayer(_playerP1);
                _statusP2.SetPlayer(_playerP2);
            }

            if (Time.unscaledTime >= _nextStatusLookup)
            {
                _nextStatusLookup = Time.unscaledTime + 0.1f;
                _statusP1.Refresh();
                _statusP2.Refresh();
            }

            RefreshRow(_rowP1, _statusP1);
            RefreshRow(_rowP2, _statusP2);
        }

        private StatusRow CreateRow(RectTransform parent, string rowName, bool rightAligned, Vector2 position)
        {
            GameObject rowObject = new GameObject(rowName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowObject.layer = gameObject.layer;
            RectTransform rowRect = rowObject.GetComponent<RectTransform>();
            rowRect.SetParent(parent, false);
            rowRect.anchorMin = rowRect.anchorMax = new Vector2(rightAligned ? 1f : 0f, 0f);
            rowRect.pivot = new Vector2(rightAligned ? 1f : 0f, 0f);
            rowRect.anchoredPosition = position;
            rowRect.sizeDelta = new Vector2(iconSize * 2f + spacing, iconSize);

            HorizontalLayoutGroup layout = rowObject.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = rightAligned ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            return new StatusRow
            {
                Damage = CreateIcon(rowRect, "DamageBuff", damageBuffSprite),
                SlowFall = CreateIcon(rowRect, "SlowFall", slowFallSprite)
            };
        }

        private StatusIcon CreateIcon(RectTransform parent, string iconName, Sprite sprite)
        {
            if (statusIconPrefab == null)
            {
                Debug.LogError("[ItemBuffHudView] Missing statusIconPrefab.", this);
                return new StatusIcon(null, null, null);
            }

            GameObject iconObject = Instantiate(statusIconPrefab, parent, false);
            iconObject.name = iconName;
            iconObject.layer = gameObject.layer;
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.sizeDelta = Vector2.one * iconSize;

            Image image = iconObject.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            LayoutElement element = iconObject.GetComponent<LayoutElement>();
            element.preferredWidth = iconSize;
            element.preferredHeight = iconSize;

            Transform sweepTransform = iconRect.Find("CooldownSweep");
            Image sweep = sweepTransform != null ? sweepTransform.GetComponent<Image>() : null;
            if (sweep == null)
            {
                Debug.LogError("[ItemBuffHudView] Status icon prefab is missing CooldownSweep/Image.", this);
                return new StatusIcon(iconObject, null, null);
            }
            sweep.sprite = sprite;
            sweep.fillAmount = 1f;
            sweep.color = new Color(0f, 0f, 0f, cooldownShadeOpacity);

            Text percent = CreateText(iconRect, "Percent", percentFontSize, TextAnchor.MiddleCenter);
            percent.rectTransform.anchorMin = Vector2.zero;
            percent.rectTransform.anchorMax = Vector2.one;
            percent.rectTransform.offsetMin = new Vector2(2f, 2f);
            percent.rectTransform.offsetMax = new Vector2(-2f, -2f);

            iconObject.SetActive(false);
            return new StatusIcon(iconObject, sweep, percent);
        }

        private Text CreateText(RectTransform parent, string objectName, int fontSize, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text), typeof(Outline));
            textObject.layer = gameObject.layer;
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            Text text = textObject.GetComponent<Text>();
            text.font = labelFont;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            Outline outline = textObject.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        private static Transform FindPlayerRoot(int playerLayer)
        {
            if (playerLayer < 0) return null;

            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate.gameObject.layer != playerLayer) continue;

                Transform root = candidate.root;
                if (root.gameObject.layer == playerLayer) return root;
            }

            return null;
        }

        private static void RefreshRow(StatusRow row, PlayerStatuses statuses)
        {
            if (row == null) return;
            if (statuses == null || statuses.Player == null)
            {
                row.HideAll();
                return;
            }

            ItemDamageBuff damage = statuses.Damage;
            SlowFallStatus slowFall = statuses.SlowFall;
            row.Damage.Set(damage != null && damage.IsActive,
                damage != null ? $"+{Mathf.RoundToInt(damage.BonusFraction * 100f)}%" : string.Empty,
                damage != null ? damage.TimeLeft : 0f,
                damage != null ? damage.Duration : 1f);
            row.SlowFall.Set(slowFall != null && slowFall.IsActive,
                slowFall != null ? $"-{Mathf.RoundToInt(slowFall.GravityReductionFraction * 100f)}%" : string.Empty,
                slowFall != null ? slowFall.TimeLeft : 0f,
                slowFall != null ? slowFall.Duration : 1f);
        }

        /// <summary>Giữ reference status, tránh GetComponent mỗi frame.</summary>
        private sealed class PlayerStatuses
        {
            public Transform Player { get; private set; }
            public ItemDamageBuff Damage { get; private set; }
            public SlowFallStatus SlowFall { get; private set; }

            public void SetPlayer(Transform player)
            {
                if (Player == player) return;
                Player = player;
                Damage = null;
                SlowFall = null;
            }

            public void Refresh()
            {
                if (Player == null)
                {
                    SetPlayer(null);
                    return;
                }

                if (Damage == null) Damage = Player.GetComponentInChildren<ItemDamageBuff>();
                if (SlowFall == null) SlowFall = Player.GetComponentInChildren<SlowFallStatus>();
            }
        }

        private sealed class StatusRow
        {
            public StatusIcon Damage;
            public StatusIcon SlowFall;

            public void HideAll()
            {
                Damage.Set(false, string.Empty, 0f, 1f);
                SlowFall.Set(false, string.Empty, 0f, 1f);
            }
        }

        private sealed class StatusIcon
        {
            private readonly GameObject _root;
            private readonly Image _cooldownSweep;
            private readonly Text _percent;

            public StatusIcon(GameObject root, Image cooldownSweep, Text percent)
            {
                _root = root;
                _cooldownSweep = cooldownSweep;
                _percent = percent;
            }

            public void Set(bool visible, string percent, float timeLeft, float duration)
            {
                if (_root == null || _cooldownSweep == null || _percent == null) return;
                if (_root.activeSelf != visible) _root.SetActive(visible);
                if (!visible) return;

                _percent.text = percent;
                _cooldownSweep.fillAmount = Mathf.Clamp01(timeLeft / Mathf.Max(0.1f, duration));
            }
        }
    }
}
