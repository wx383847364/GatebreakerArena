using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.HotUpdate.GatebreakerArena.Mode;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.BrickDuel
{
    public sealed class BrickDuelSessionController : IDisposable
    {
        private const string DebugCollisionOverlayName = "BrickDuelDebugCollisionOverlay";
        private const string SceneDebugLayerName = "SceneDebug";
        private const int SceneDebugLayerFallback = 6;
        private const int DebugOverlaySortingOrder = 1200;
        private const float DebugOverlayDepth = -0.08f;
        private readonly BrickDuelVisualAssetService _assetService;
        private readonly Dictionary<int, BrickView> _brickViews = new Dictionary<int, BrickView>();
        private readonly Dictionary<int, CapsuleView> _capsuleViews = new Dictionary<int, CapsuleView>();
        private readonly Dictionary<BrickDuelBrickType, Stack<GameObject>> _brickPools =
            new Dictionary<BrickDuelBrickType, Stack<GameObject>>();
        private readonly HashSet<int> _liveBrickIds = new HashSet<int>();
        private readonly HashSet<int> _liveCapsuleIds = new HashSet<int>();
        private readonly List<SpecialFeedbackView> _specialFeedbackViews =
            new List<SpecialFeedbackView>();
        private readonly List<LineRenderer> _debugCollisionLines = new List<LineRenderer>();
        private BrickDuelVisualAssetSet _assets;
        private GameObject _root;
        private GameObject _scene;
        private GameObject _bottomPaddle;
        private GameObject _topPaddle;
        private GameObject _bottomBall;
        private GameObject _topBall;
        private Vector3 _bottomPaddleBaseScale = Vector3.one;
        private Vector3 _topPaddleBaseScale = Vector3.one;
        private Vector3 _bottomBallBaseScale = Vector3.one;
        private Vector3 _topBallBaseScale = Vector3.one;
        private Transform _debugCollisionOverlayRoot;
        private Material _debugOverlayMaterial;
        private BrickDuelWallOverlayBounds? _sceneWallInnerBounds;
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
                if (BrickDuelCollisionOverlayGeometry.TryResolveWallInnerBounds(
                        _scene.transform,
                        out BrickDuelWallOverlayBounds wallBounds) &&
                    BrickDuelCollisionOverlayGeometry.TryApplyWallInnerBoundsToRule(
                        rule,
                        wallBounds))
                {
                    _sceneWallInnerBounds = new BrickDuelWallOverlayBounds(
                        -rule.ArenaHalfWidth,
                        rule.ArenaHalfWidth,
                        -rule.CoreLineY,
                        rule.CoreLineY);
                }
                else
                {
                    _sceneWallInnerBounds = null;
                }
                _bottomPaddle = InstantiateRuntimeObject(_assets.Paddle.Prefab, "BottomPaddle");
                _topPaddle = InstantiateRuntimeObject(_assets.Paddle.Prefab, "TopPaddle");
                _topPaddle.transform.rotation = Quaternion.Euler(0f, 0f, 180f);
                _bottomBall = InstantiateRuntimeObject(_assets.PlayerBall.Prefab, "BottomBall");
                _topBall = InstantiateRuntimeObject(_assets.AiBall.Prefab, "TopBall");
                _bottomPaddleBaseScale = _bottomPaddle.transform.localScale;
                _topPaddleBaseScale = _topPaddle.transform.localScale;
                _bottomBallBaseScale = _bottomBall.transform.localScale;
                _topBallBaseScale = _topBall.transform.localScale;
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
            _capsuleViews.Clear();
            _brickPools.Clear();
            _liveBrickIds.Clear();
            _liveCapsuleIds.Clear();
            _specialFeedbackViews.Clear();
            ClearDebugCollisionOverlay();
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }

            _root = null;
            _scene = null;
            _sceneWallInnerBounds = null;
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
            ApplyPaddleScale(_bottomPaddle, _bottomPaddleBaseScale, snapshot.BottomPaddleHalfWidth);
            ApplyPaddleScale(_topPaddle, _topPaddleBaseScale, snapshot.TopPaddleHalfWidth);
            ApplyBallScale(_bottomBall, _bottomBallBaseScale, snapshot.BottomBallRadius);
            ApplyBallScale(_topBall, _topBallBaseScale, snapshot.TopBallRadius);
            SyncBrickViews(snapshot.Bricks);
            SyncCapsuleViews(snapshot.Capsules);
            SyncDebugCollisionOverlay(snapshot);
        }

        private void ApplyPaddleScale(GameObject paddle, Vector3 baseScale, float halfWidth)
        {
            if (paddle == null || Runtime?.Rule == null)
            {
                return;
            }

            float multiplier = halfWidth / Mathf.Max(0.0001f, Runtime.Rule.PaddleHalfWidth);
            paddle.transform.localScale = new Vector3(
                baseScale.x * multiplier,
                baseScale.y,
                baseScale.z);
        }

        private void ApplyBallScale(GameObject ball, Vector3 baseScale, float radius)
        {
            if (ball == null || Runtime?.Rule == null)
            {
                return;
            }

            float multiplier = radius / Mathf.Max(0.0001f, Runtime.Rule.BallRadius);
            ball.transform.localScale = baseScale * multiplier;
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

        private void SyncCapsuleViews(IReadOnlyList<BrickDuelItemCapsuleState> capsules)
        {
            _liveCapsuleIds.Clear();
            if (capsules == null)
            {
                ReleaseStaleCapsuleViews();
                return;
            }

            for (int i = 0; i < capsules.Count; i++)
            {
                BrickDuelItemCapsuleState capsule = capsules[i];
                _liveCapsuleIds.Add(capsule.CapsuleId);
                if (!_capsuleViews.TryGetValue(capsule.CapsuleId, out CapsuleView view))
                {
                    view = CreateCapsuleView(capsule.ItemId);
                    _capsuleViews[capsule.CapsuleId] = view;
                }

                view.GameObject.transform.position = new Vector3(
                    capsule.Position.x,
                    capsule.Position.y,
                    view.GameObject.transform.position.z);
            }

            ReleaseStaleCapsuleViews();
        }

        private void ReleaseStaleCapsuleViews()
        {
            if (_capsuleViews.Count == _liveCapsuleIds.Count)
            {
                return;
            }

            var staleIds = new List<int>();
            foreach (KeyValuePair<int, CapsuleView> pair in _capsuleViews)
            {
                if (!_liveCapsuleIds.Contains(pair.Key))
                {
                    staleIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < staleIds.Count; i++)
            {
                CapsuleView view = _capsuleViews[staleIds[i]];
                _capsuleViews.Remove(staleIds[i]);
                UnityEngine.Object.Destroy(view.GameObject);
            }
        }

        private CapsuleView CreateCapsuleView(string itemId)
        {
            var gameObject = new GameObject($"ItemCapsule_{itemId}");
            gameObject.transform.SetParent(_root.transform, false);
            gameObject.transform.localScale = Vector3.one * 0.45f;
            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 40;
            Sprite sprite = _assets != null ? _assets.GetItemSprite(itemId) : null;
            if (sprite != null)
            {
                renderer.sprite = sprite;
            }
            else
            {
                renderer.sprite = CreateFallbackCapsuleSprite();
                renderer.color = ResolveItemColor(itemId);
            }

            return new CapsuleView(gameObject, renderer);
        }

        private static Sprite _fallbackCapsuleSprite;

        private static Sprite CreateFallbackCapsuleSprite()
        {
            if (_fallbackCapsuleSprite != null)
            {
                return _fallbackCapsuleSprite;
            }

            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[64];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _fallbackCapsuleSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 8f, 8f),
                new Vector2(0.5f, 0.5f),
                8f);
            return _fallbackCapsuleSprite;
        }

        private static Color ResolveItemColor(string itemId)
        {
            switch (itemId)
            {
                case BrickDuelItemIds.WidePaddle:
                    return new Color(0.2f, 0.9f, 0.95f, 1f);
                case BrickDuelItemIds.LargeBall:
                    return new Color(0.85f, 0.92f, 1f, 1f);
                case BrickDuelItemIds.PhaseDrill:
                    return new Color(0.72f, 0.35f, 1f, 1f);
                case BrickDuelItemIds.DampingPulse:
                    return new Color(0.45f, 0.8f, 1f, 1f);
                case BrickDuelItemIds.CoreBuffer:
                    return new Color(1f, 0.84f, 0.25f, 1f);
                default:
                    return Color.white;
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

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void SyncDebugCollisionOverlay(BrickDuelSnapshot snapshot)
        {
            if (_root == null || Runtime?.Rule == null || snapshot == null)
            {
                return;
            }

            EnsureDebugCollisionOverlayRoot();
            IReadOnlyList<BrickDuelCollisionOverlayLine> lines =
                BrickDuelCollisionOverlayGeometry.BuildLines(
                    Runtime.Rule,
                    snapshot,
                    _sceneWallInnerBounds);
            for (int i = 0; i < lines.Count; i++)
            {
                BrickDuelCollisionOverlayLine source = lines[i];
                LineRenderer line = EnsureDebugCollisionLine(i);
                line.gameObject.SetActive(true);
                line.positionCount = 2;
                line.SetPosition(0, new Vector3(source.Start.x, source.Start.y, DebugOverlayDepth));
                line.SetPosition(1, new Vector3(source.End.x, source.End.y, DebugOverlayDepth));
                ApplyDebugCollisionStyle(line, source.Kind);
            }

            for (int i = lines.Count; i < _debugCollisionLines.Count; i++)
            {
                if (_debugCollisionLines[i] != null)
                {
                    _debugCollisionLines[i].gameObject.SetActive(false);
                }
            }
        }

        private void EnsureDebugCollisionOverlayRoot()
        {
            if (_debugCollisionOverlayRoot != null)
            {
                return;
            }

            var overlayObject = new GameObject(DebugCollisionOverlayName);
            overlayObject.layer = GetSceneDebugLayer();
            _debugCollisionOverlayRoot = overlayObject.transform;
            _debugCollisionOverlayRoot.SetParent(_root.transform, false);
            _debugCollisionOverlayRoot.localPosition = Vector3.zero;
            _debugCollisionOverlayRoot.localRotation = Quaternion.identity;
            _debugCollisionOverlayRoot.localScale = Vector3.one;
        }

        private LineRenderer EnsureDebugCollisionLine(int index)
        {
            while (_debugCollisionLines.Count <= index)
            {
                var lineObject = new GameObject("Brick Duel Debug Collision Line");
                lineObject.layer = GetSceneDebugLayer();
                lineObject.transform.SetParent(_debugCollisionOverlayRoot, false);
                LineRenderer line = lineObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.sharedMaterial = GetDebugOverlayMaterial();
                line.textureMode = LineTextureMode.Stretch;
                line.alignment = LineAlignment.View;
                line.numCapVertices = 2;
                line.numCornerVertices = 2;
                line.sortingOrder = DebugOverlaySortingOrder;
                _debugCollisionLines.Add(line);
            }

            return _debugCollisionLines[index];
        }

        private Material GetDebugOverlayMaterial()
        {
            if (_debugOverlayMaterial != null)
            {
                return _debugOverlayMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default") ??
                            Shader.Find("Universal Render Pipeline/Unlit") ??
                            Shader.Find("Unlit/Color") ??
                            Shader.Find("Standard") ??
                            Shader.Find("Diffuse");
            _debugOverlayMaterial = new Material(shader);
            _debugOverlayMaterial.color = Color.white;
            return _debugOverlayMaterial;
        }

        private static void ApplyDebugCollisionStyle(
            LineRenderer line,
            BrickDuelCollisionOverlayLineKind kind)
        {
            Color color;
            float width;
            switch (kind)
            {
                case BrickDuelCollisionOverlayLineKind.Paddle:
                    color = new Color(1f, 0.1f, 1f, 1f);
                    width = 0.02f;
                    break;
                case BrickDuelCollisionOverlayLineKind.Brick:
                    color = new Color(1f, 0.88f, 0.1f, 0.72f);
                    width = 0.008f;
                    break;
                case BrickDuelCollisionOverlayLineKind.Wall:
                default:
                    color = new Color(1f, 0.08f, 0.04f, 1f);
                    width = 0.034f;
                    break;
            }

            line.startColor = color;
            line.endColor = color;
            line.startWidth = width;
            line.endWidth = width;
        }

        private void ClearDebugCollisionOverlay()
        {
            _debugCollisionLines.Clear();
            if (_debugCollisionOverlayRoot != null)
            {
                UnityEngine.Object.Destroy(_debugCollisionOverlayRoot.gameObject);
                _debugCollisionOverlayRoot = null;
            }

            if (_debugOverlayMaterial != null)
            {
                UnityEngine.Object.Destroy(_debugOverlayMaterial);
                _debugOverlayMaterial = null;
            }
        }

        private static int GetSceneDebugLayer()
        {
            int layer = LayerMask.NameToLayer(SceneDebugLayerName);
            return layer >= 0 ? layer : SceneDebugLayerFallback;
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

        private sealed class CapsuleView
        {
            public CapsuleView(GameObject gameObject, SpriteRenderer renderer)
            {
                GameObject = gameObject;
                Renderer = renderer;
            }

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
