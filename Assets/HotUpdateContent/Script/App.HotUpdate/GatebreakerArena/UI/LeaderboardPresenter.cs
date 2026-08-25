using System;
using System.Collections.Generic;
using System.Linq;
using App.Shared.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace App.HotUpdate.GatebreakerArena.UI
{
    public sealed class LeaderboardEntry
    {
        public LeaderboardEntry(
            string playerId,
            string playerName,
            string heroName,
            int rating,
            int wins,
            int losses,
            bool isLocalPlayer = false)
        {
            PlayerId = string.IsNullOrWhiteSpace(playerId) ? throw new ArgumentException("Player id is required.", nameof(playerId)) : playerId.Trim();
            PlayerName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
            HeroName = string.IsNullOrWhiteSpace(heroName) ? "未知" : heroName.Trim();
            Rating = Mathf.Max(0, rating);
            Wins = Mathf.Max(0, wins);
            Losses = Mathf.Max(0, losses);
            IsLocalPlayer = isLocalPlayer;
        }

        public string PlayerId { get; }
        public string PlayerName { get; }
        public string HeroName { get; }
        public int Rating { get; }
        public int Wins { get; }
        public int Losses { get; }
        public int Matches => Wins + Losses;
        public bool IsLocalPlayer { get; }
        public int WinRatePercent => Matches == 0
            ? 0
            : Mathf.RoundToInt(Wins * 100f / Matches);
    }

    public interface ILeaderboardDataSource
    {
        IReadOnlyList<LeaderboardEntry> LoadEntries();
    }

    /// <summary>
    /// v0.3 本地占位数据源。后端就绪后只需替换 ILeaderboardDataSource。
    /// </summary>
    public sealed class LocalMockLeaderboardDataSource : ILeaderboardDataSource
    {
        private readonly IReadOnlyList<LeaderboardEntry> _entries = new[]
        {
            new LeaderboardEntry("runner-01", "星轨", "脉冲", 2218, 188, 42),
            new LeaderboardEntry("runner-02", "破晓", "棱镜", 2146, 171, 49),
            new LeaderboardEntry("runner-03", "零度", "壁垒", 2075, 153, 57),
            new LeaderboardEntry("runner-04", "逐光者", "脉冲", 1992, 139, 61),
            new LeaderboardEntry("runner-05", "深蓝", "引力", 1938, 126, 64),
            new LeaderboardEntry("runner-06", "小怪物", "棱镜", 1881, 118, 67),
            new LeaderboardEntry("runner-07", "逆风", "壁垒", 1816, 103, 65),
            new LeaderboardEntry("local-player", "我", "脉冲", 1790, 1240, 760, true),
            new LeaderboardEntry("runner-09", "回声", "引力", 1742, 91, 71),
            new LeaderboardEntry("runner-10", "白昼", "棱镜", 1688, 84, 76),
            new LeaderboardEntry("runner-11", "跃迁", "脉冲", 1621, 75, 73),
            new LeaderboardEntry("runner-12", "北辰", "壁垒", 1574, 69, 76),
            new LeaderboardEntry("runner-13", "火花", "引力", 1498, 61, 79),
            new LeaderboardEntry("runner-14", "长夜", "棱镜", 1427, 54, 81),
            new LeaderboardEntry("runner-15", "新月", "脉冲", 1365, 47, 83),
        };

        public IReadOnlyList<LeaderboardEntry> LoadEntries()
        {
            return _entries;
        }
    }

    public static class LeaderboardRanking
    {
        public static IReadOnlyList<LeaderboardEntry> Sort(IReadOnlyList<LeaderboardEntry> entries)
        {
            if (entries == null)
            {
                return Array.Empty<LeaderboardEntry>();
            }

            return entries
                .Where(entry => entry != null)
                .OrderByDescending(entry => entry.Rating)
                .ThenByDescending(entry => entry.Wins)
                .ThenBy(entry => entry.Losses)
                .ThenBy(entry => entry.PlayerId, StringComparer.Ordinal)
                .ToArray();
        }
    }

    /// <summary>
    /// 将排行榜业务接到场景中已经搭好的 RankPanel；场景引用从现有 UI 根节点一次性解析。
    /// </summary>
    public sealed class LeaderboardPresenter : IDisposable
    {
        private static readonly Color Gold = new Color(1f, 0.78f, 0.2f, 1f);
        private static readonly Color Silver = new Color(0.78f, 0.86f, 0.96f, 1f);
        private static readonly Color Bronze = new Color(0.88f, 0.55f, 0.28f, 1f);
        private readonly ILeaderboardDataSource _dataSource;
        private readonly GameObject _modeSelectRoot;
        private readonly GameObject _rankRoot;
        private readonly Button _openButton;
        private readonly Button _backButton;
        private readonly RectTransform _content;
        private readonly GameObject _rowTemplate;
        private readonly GameObject _localRow;
        private readonly ScrollRect _scrollRect;
        private readonly List<GameObject> _rows = new List<GameObject>();
        private readonly IAppLogger _logger;
        private readonly Sprite _defaultRowBackgroundSprite;
        private readonly Sprite _localRowBackgroundSprite;
        private bool _disposed;

        private LeaderboardPresenter(
            ILeaderboardDataSource dataSource,
            GameObject modeSelectRoot,
            GameObject rankRoot,
            Button openButton,
            Button backButton,
            RectTransform content,
            GameObject rowTemplate,
            GameObject localRow,
            ScrollRect scrollRect,
            IAppLogger logger)
        {
            _dataSource = dataSource;
            _modeSelectRoot = modeSelectRoot;
            _rankRoot = rankRoot;
            _openButton = openButton;
            _backButton = backButton;
            _content = content;
            _rowTemplate = rowTemplate;
            _localRow = localRow;
            _scrollRect = scrollRect;
            _logger = logger;
            _defaultRowBackgroundSprite = FindDescendant(_rowTemplate.transform, "Image")?.GetComponent<Image>()?.sprite;
            _localRowBackgroundSprite = FindDescendant(_localRow.transform, "Image")?.GetComponent<Image>()?.sprite;

            _rows.Add(_rowTemplate);
            _openButton.onClick.AddListener(Open);
            _backButton.onClick.AddListener(Close);
            _rankRoot.SetActive(false);
        }

        public bool IsOpen => !_disposed && _rankRoot != null && _rankRoot.activeSelf;

        public static LeaderboardPresenter TryCreate(
            IGatebreakerArenaSceneUiBinding binding,
            ILeaderboardDataSource dataSource,
            IAppLogger logger = null)
        {
            GameObject modeSelectRoot = binding?.ModeSelectRootObject as GameObject;
            if (modeSelectRoot == null)
            {
                logger?.LogWarning("LeaderboardPresenter: ModeSelectRoot is unavailable; leaderboard was not bound.");
                return null;
            }

            Transform sceneUiRoot = modeSelectRoot.transform.root;
            Transform rankTransform = FindDescendant(sceneUiRoot, "RankPanel");
            Transform openTransform = FindDescendant(modeSelectRoot.transform, "RankButton");
            Transform backTransform = FindDescendant(rankTransform, "RankBackButton");
            Transform contentTransform = FindDescendant(rankTransform, "ListContent");
            Transform rowTransform = FindDescendant(contentTransform, "PlayerInfo");
            Transform localRowTransform = FindDescendant(rankTransform, "MyInfo");
            ScrollRect scrollRect = FindDescendant(rankTransform, "LeaderList")?.GetComponent<ScrollRect>();
            Button openButton = openTransform?.GetComponent<Button>();
            Button backButton = backTransform?.GetComponent<Button>();

            if (rankTransform == null || openButton == null || backButton == null ||
                contentTransform == null || rowTransform == null || localRowTransform == null)
            {
                logger?.LogWarning("LeaderboardPresenter: RankPanel hierarchy is incomplete; leaderboard was not bound.");
                rankTransform?.gameObject.SetActive(false);
                return null;
            }

            return new LeaderboardPresenter(
                dataSource ?? new LocalMockLeaderboardDataSource(),
                modeSelectRoot,
                rankTransform.gameObject,
                openButton,
                backButton,
                contentTransform as RectTransform,
                rowTransform.gameObject,
                localRowTransform.gameObject,
                scrollRect,
                logger);
        }

        public void Open()
        {
            if (_disposed)
            {
                return;
            }

            Refresh();
            _modeSelectRoot.SetActive(false);
            _rankRoot.SetActive(true);
            if (_scrollRect != null)
            {
                _scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        public void Close()
        {
            if (_disposed)
            {
                return;
            }

            _rankRoot.SetActive(false);
            _modeSelectRoot.SetActive(true);
        }

        public void Refresh()
        {
            IReadOnlyList<LeaderboardEntry> entries;
            try
            {
                entries = LeaderboardRanking.Sort(_dataSource.LoadEntries());
            }
            catch (Exception exception)
            {
                _logger?.LogError("LeaderboardPresenter: failed to load leaderboard. {0}", exception);
                entries = Array.Empty<LeaderboardEntry>();
            }

            EnsureRowCount(entries.Count);
            LeaderboardEntry localEntry = null;
            int localRank = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                bool visible = i < entries.Count;
                _rows[i].SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                LeaderboardEntry entry = entries[i];
                PopulateRow(_rows[i].transform, entry, i + 1, entry.IsLocalPlayer);
                if (entry.IsLocalPlayer && localEntry == null)
                {
                    localEntry = entry;
                    localRank = i + 1;
                }
            }

            _localRow.SetActive(localEntry != null);
            if (localEntry != null)
            {
                PopulateRow(_localRow.transform, localEntry, localRank, true);
            }

            ResizeContent(entries.Count);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_openButton != null)
            {
                _openButton.onClick.RemoveListener(Open);
            }

            if (_backButton != null)
            {
                _backButton.onClick.RemoveListener(Close);
            }
        }

        private void EnsureRowCount(int count)
        {
            while (_rows.Count < count)
            {
                GameObject row = UnityEngine.Object.Instantiate(_rowTemplate, _content, false);
                row.name = "PlayerInfo_" + (_rows.Count + 1);
                _rows.Add(row);
            }
        }

        private void ResizeContent(int rowCount)
        {
            if (_content == null || _rowTemplate == null)
            {
                return;
            }

            float rowHeight = (_rowTemplate.transform as RectTransform)?.rect.height ?? 100f;
            VerticalLayoutGroup layout = _content.GetComponent<VerticalLayoutGroup>();
            float spacing = layout != null ? layout.spacing : 0f;
            float height = Mathf.Max(0f, rowCount * rowHeight + Mathf.Max(0, rowCount - 1) * spacing);
            _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }

        private void PopulateRow(Transform row, LeaderboardEntry entry, int rank, bool useLocalBackground)
        {
            SetText(row, "LeadNum", rank.ToString());
            SetText(row, "PlayerName", entry.PlayerName);
            SetText(row, "HeroName", entry.HeroName);
            SetText(row, "LeadPoint", entry.Rating.ToString());
            SetText(row, "WinningRate", entry.WinRatePercent + "%");
            SetText(row, "Session", entry.Matches.ToString());

            TMP_Text rankText = FindDescendant(row, "LeadNum")?.GetComponent<TMP_Text>();
            if (rankText != null)
            {
                rankText.color = rank == 1 ? Gold : rank == 2 ? Silver : rank == 3 ? Bronze : Color.white;
            }

            Image background = FindDescendant(row, "Image")?.GetComponent<Image>();
            if (background != null)
            {
                background.color = Color.white;
                background.sprite = useLocalBackground && _localRowBackgroundSprite != null
                    ? _localRowBackgroundSprite
                    : _defaultRowBackgroundSprite;
            }
        }

        private static void SetText(Transform root, string childName, string value)
        {
            TMP_Text text = FindDescendant(root, childName)?.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.text = value ?? string.Empty;
            }
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            if (string.Equals(root.name, objectName, StringComparison.Ordinal))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindDescendant(root.GetChild(i), objectName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}
