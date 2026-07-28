using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.HotUpdate.GatebreakerArena.Mode;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.BrickDuel
{
    public sealed class BrickDuelSessionController : IDisposable
    {
        private readonly BrickDuelVisualAssetService _assetService;
        private readonly Dictionary<int, BrickView> _brickViews = new Dictionary<int, BrickView>();
        private readonly Dictionary<BrickDuelBrickType, Stack<GameObject>> _brickPools =
            new Dictionary<BrickDuelBrickType, Stack<GameObject>>();
        private readonly HashSet<int> _liveBrickIds = new HashSet<int>();
        private readonly List<SpecialFeedbackView> _specialFeedbackViews =
            new List<SpecialFeedbackView>();
        private BrickDuelVisualAssetSet _assets;
        private GameObject _root;
        private GameObject _scene;
        private GameObject _bottomPaddle;
        private GameObject _topPaddle;
        private GameObject _bottomBall;
        private GameObject _topBall;
        private float _frameAccumulator;
        private int _operationVersion;
        private bool _disposed;

        public BrickDuelSessionController(BrickDuelVisualAssetService assetService)
        {
            _assetService = assetService ?? throw new ArgumentNullException(nameof(assetService));
        }

        public BrickDuelRuntime Runtime { get; private set; }
        public bool IsActive => Runtime != null;
        public string LastError { get; private set; } = string.Empty;
        public BrickDuelSnapshot Snapshot => Runtime?.CreateSnapshot();

        public async Task<bool> StartAsync(
            BrickDuelRuleDefinition rule,
            AiRuleDefinition aiRule,
            Transform parent = null)
        {
            if (_disposed)
            {
                LastError = "1v1 会话已释放。";
                return false;
            }

            Stop();
            int operationVersion = _operationVersion;
            LastError = string.Empty;
            BrickDuelVisualAssetSet loadedAssets = await _assetService.LoadAsync(rule);
            if (!IsOperationCurrent(operationVersion))
            {
                loadedAssets?.Dispose();
                return false;
            }

            if (loadedAssets == null || !loadedAssets.IsComplete)
            {
                loadedAssets?.Dispose();
                LastError = "1v1 资源加载失败，请返回后重试。";
                return false;
            }

            try
            {
                _assets = loadedAssets;
                Runtime = new BrickDuelRuntime(rule, aiRule);
                _root = new GameObject("BrickDuelRuntimeRoot");
                if (parent != null)
                {
                    _root.transform.SetParent(parent, false);
                }

                _scene = UnityEngine.Object.Instantiate(
                    _assets.Scene.Prefab,
                    Vector3.zero,
                    Quaternion.identity,
                    _root.transform);
                _scene.name = "SceneSingle_Runtime";
                _bottomPaddle = InstantiateRuntimeObject(_assets.Paddle.Prefab, "BottomPaddle");
                _topPaddle = InstantiateRuntimeObject(_assets.Paddle.Prefab, "TopPaddle");
                _topPaddle.transform.rotation = Quaternion.Euler(0f, 0f, 180f);
                _bottomBall = InstantiateRuntimeObject(_assets.PlayerBall.Prefab, "BottomBall");
                _topBall = InstantiateRuntimeObject(_assets.AiBall.Prefab, "TopBall");
                Runtime.BeginCountdown();
                SyncViews(Runtime.CreateSnapshot());
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"1v1 场景创建失败：{ex.Message}";
                Stop();
                return false;
            }
        }

        public void Tick(float deltaTime, float playerMoveAxis)
        {
            if (Runtime == null || deltaTime <= 0f)
            {
                return;
            }

            if (Runtime.IsPaused)
            {
                _frameAccumulator = 0f;
                SyncViews(Runtime.CreateSnapshot());
                return;
            }

            _frameAccumulator += Mathf.Min(deltaTime, 0.25f);
            float frameDelta = Runtime.FrameDelta;
            int steps = 0;
            while (_frameAccumulator + 0.000001f >= frameDelta && steps < 8)
            {
                Runtime.StepFrame(new BrickDuelFrameInput(playerMoveAxis));
                SpawnMysteryBreakFeedback(Runtime.LastFrameEvents.MysteryDestroyedBrickIds);
                _frameAccumulator -= frameDelta;
                steps++;
            }

            if (steps == 8 && _frameAccumulator >= frameDelta)
            {
                _frameAccumulator %= frameDelta;
            }

            UpdateSpecialFeedback(deltaTime);
            SyncViews(Runtime.CreateSnapshot());
        }

        public void SetPaused(bool paused)
        {
            Runtime?.SetPaused(paused);
            _frameAccumulator = 0f;
        }

        public void Stop()
        {
            _operationVersion++;
            Runtime = null;
            _frameAccumulator = 0f;
            _brickViews.Clear();
            _brickPools.Clear();
            _liveBrickIds.Clear();
            _specialFeedbackViews.Clear();
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }

            _root = null;
            _scene = null;
            _bottomPaddle = null;
            _topPaddle = null;
            _bottomBall = null;
            _topBall = null;
            _assets?.Dispose();
            _assets = null;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Stop();
        }

        private bool IsOperationCurrent(int operationVersion)
        {
            return !_disposed && operationVersion == _operationVersion;
        }

        private GameObject InstantiateRuntimeObject(GameObject prefab, string name)
        {
            GameObject instance = UnityEngine.Object.Instantiate(
                prefab,
                Vector3.zero,
                Quaternion.identity,
                _root.transform);
            instance.name = name;
            return instance;
        }

        private void SyncViews(BrickDuelSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            SetPosition(_bottomPaddle, snapshot.BottomPaddle.Position);
            SetPosition(_topPaddle, snapshot.TopPaddle.Position);
            SetPosition(_bottomBall, snapshot.BottomBall.Position);
            SetPosition(_topBall, snapshot.TopBall.Position);
            SetActive(_bottomBall, snapshot.BottomBall.IsActive);
            SetActive(_topBall, snapshot.TopBall.IsActive);
            SyncBrickViews(snapshot.Bricks);
        }

        private void SyncBrickViews(IReadOnlyList<BrickDuelBrickState> bricks)
        {
            _liveBrickIds.Clear();
            for (int i = 0; i < bricks.Count; i++)
            {
                BrickDuelBrickState brick = bricks[i];
                _liveBrickIds.Add(brick.BrickId);
                if (!_brickViews.TryGetValue(brick.BrickId, out BrickView view))
                {
                    view = AcquireBrickView(brick.InitialType);
                    _brickViews[brick.BrickId] = view;
                }

                view.GameObject.transform.position = new Vector3(
                    brick.Position.x,
                    brick.Position.y,
                    view.GameObject.transform.position.z);
                ApplyBrickSprite(view, brick.VisualType);
            }

            if (_brickViews.Count == _liveBrickIds.Count)
            {
                return;
            }

            var staleIds = new List<int>();
            foreach (KeyValuePair<int, BrickView> pair in _brickViews)
            {
                if (!_liveBrickIds.Contains(pair.Key))
                {
                    staleIds.Add(pair.Key);
                }
            }
            for (int i = 0; i < staleIds.Count; i++)
            {
                BrickView view = _brickViews[staleIds[i]];
                _brickViews.Remove(staleIds[i]);
                view.GameObject.SetActive(false);
                GetPool(view.InitialType).Push(view.GameObject);
            }
        }

        private BrickView AcquireBrickView(BrickDuelBrickType type)
        {
            Stack<GameObject> pool = GetPool(type);
            GameObject instance = pool.Count > 0
                ? pool.Pop()
                : InstantiateRuntimeObject(_assets.GetBrick(type).Prefab, $"Brick_{type}");
            instance.SetActive(true);
            return new BrickView(type, instance, instance.GetComponent<SpriteRenderer>());
        }

        private Stack<GameObject> GetPool(BrickDuelBrickType type)
        {
            if (!_brickPools.TryGetValue(type, out Stack<GameObject> pool))
            {
                pool = new Stack<GameObject>();
                _brickPools[type] = pool;
            }
            return pool;
        }

        private void ApplyBrickSprite(BrickView view, BrickDuelBrickType visualType)
        {
            if (view.Renderer == null)
            {
                return;
            }

            GameObject source = _assets.GetBrick(visualType)?.Prefab;
            SpriteRenderer sourceRenderer = source != null ? source.GetComponent<SpriteRenderer>() : null;
            if (sourceRenderer != null)
            {
                view.Renderer.sprite = sourceRenderer.sprite;
            }
        }

        private void ReleaseAllBrickViewsToPool()
        {
            foreach (BrickView view in _brickViews.Values)
            {
                view.GameObject.SetActive(false);
                GetPool(view.InitialType).Push(view.GameObject);
            }
            _brickViews.Clear();
        }

        private void SpawnMysteryBreakFeedback(IReadOnlyList<int> destroyedBrickIds)
        {
            if (destroyedBrickIds == null || destroyedBrickIds.Count == 0 || _root == null)
            {
                return;
            }

            for (int i = 0; i < destroyedBrickIds.Count; i++)
            {
                if (!_brickViews.TryGetValue(destroyedBrickIds[i], out BrickView sourceView))
                {
                    continue;
                }

                GameObject feedback = InstantiateRuntimeObject(
                    _assets.GetBrick(BrickDuelBrickType.Mystery).Prefab,
                    "MysteryBrickBreakFeedback");
                feedback.transform.position = sourceView.GameObject.transform.position;
                feedback.transform.rotation = sourceView.GameObject.transform.rotation;
                feedback.transform.localScale =
                    sourceView.GameObject.transform.localScale * 1.15f;
                _specialFeedbackViews.Add(new SpecialFeedbackView(feedback, 0.18f));
            }
        }

        private void UpdateSpecialFeedback(float deltaTime)
        {
            for (int i = _specialFeedbackViews.Count - 1; i >= 0; i--)
            {
                SpecialFeedbackView feedback = _specialFeedbackViews[i];
                feedback.RemainingSeconds -= Mathf.Max(0f, deltaTime);
                if (feedback.RemainingSeconds <= 0f)
                {
                    UnityEngine.Object.Destroy(feedback.GameObject);
                    _specialFeedbackViews.RemoveAt(i);
                    continue;
                }

                float progress = 1f - feedback.RemainingSeconds / feedback.DurationSeconds;
                feedback.GameObject.transform.localScale =
                    feedback.InitialScale * Mathf.Lerp(1f, 1.35f, progress);
            }
        }

        private void ReleaseSpecialFeedbackViews()
        {
            for (int i = 0; i < _specialFeedbackViews.Count; i++)
            {
                UnityEngine.Object.Destroy(_specialFeedbackViews[i].GameObject);
            }
            _specialFeedbackViews.Clear();
        }

        private static void SetPosition(GameObject target, Vector2 position)
        {
            if (target == null)
            {
                return;
            }
            Vector3 current = target.transform.position;
            target.transform.position = new Vector3(position.x, position.y, current.z);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private sealed class BrickView
        {
            public BrickView(
                BrickDuelBrickType initialType,
                GameObject gameObject,
                SpriteRenderer renderer)
            {
                InitialType = initialType;
                GameObject = gameObject;
                Renderer = renderer;
            }

            public BrickDuelBrickType InitialType { get; }
            public GameObject GameObject { get; }
            public SpriteRenderer Renderer { get; }
        }

        private sealed class SpecialFeedbackView
        {
            public SpecialFeedbackView(GameObject gameObject, float durationSeconds)
            {
                GameObject = gameObject;
                DurationSeconds = durationSeconds;
                RemainingSeconds = durationSeconds;
                InitialScale = gameObject.transform.localScale;
            }

            public GameObject GameObject { get; }
            public float DurationSeconds { get; }
            public float RemainingSeconds { get; set; }
            public Vector3 InitialScale { get; }
        }
    }
}
