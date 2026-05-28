namespace App.Shared.Contracts
{
    public sealed class UiSafeAreaInfo
    {
        public int Left { get; set; }
        public int Right { get; set; }
        public int Top { get; set; }
        public int Bottom { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public UiSafeAreaInfo Clone()
        {
            return new UiSafeAreaInfo
            {
                Left = Left,
                Right = Right,
                Top = Top,
                Bottom = Bottom,
                Width = Width,
                Height = Height,
            };
        }
    }

    public sealed class WeChatWindowInfo
    {
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        public float PixelRatio { get; set; } = 1f;
        public int StatusBarHeight { get; set; }
        public bool IsFallback { get; set; }
        public bool IsAvailable { get; set; }
        public UiSafeAreaInfo SafeArea { get; set; } = new UiSafeAreaInfo();

        public WeChatWindowInfo Clone()
        {
            return new WeChatWindowInfo
            {
                ScreenWidth = ScreenWidth,
                ScreenHeight = ScreenHeight,
                WindowWidth = WindowWidth,
                WindowHeight = WindowHeight,
                PixelRatio = PixelRatio,
                StatusBarHeight = StatusBarHeight,
                IsFallback = IsFallback,
                IsAvailable = IsAvailable,
                SafeArea = SafeArea != null ? SafeArea.Clone() : new UiSafeAreaInfo(),
            };
        }
    }

    /// <summary>
    /// 服务接口基类，所有服务都应实现此接口
    /// </summary>
    public interface IService
    {
        /// <summary>
        /// 初始化服务
        /// </summary>
        void Initialize();

        /// <summary>
        /// 更新服务（每帧调用）
        /// </summary>
        void Update(float deltaTime);

        /// <summary>
        /// 销毁服务
        /// </summary>
        void Shutdown();
    }

    /// <summary>
    /// 日志接口（Shared层，供AOT和HotUpdate共用）
    /// </summary>
    public interface IAppLogger
    {
        void Log(LogLevel level, string message, params object[] args);
        void LogDebug(string message, params object[] args);
        void LogInfo(string message, params object[] args);
        void LogWarning(string message, params object[] args);
        void LogError(string message, params object[] args);
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// 通用音频通道。
    /// </summary>
    public enum AudioChannel
    {
        Music = 0,
        Sfx = 1,
    }

    /// <summary>
    /// 音频播放参数，供 prefab 挂件和 HotUpdate 业务层共用。
    /// </summary>
    public struct AudioPlayParameters
    {
        public AudioPlayParameters(bool loop, float volume)
        {
            Loop = loop;
            Volume = volume;
        }

        public bool Loop { get; set; }
        public float Volume { get; set; }

        public static AudioPlayParameters Default => new AudioPlayParameters(false, 1f);
    }

    public interface IAudioPlaybackHandle
    {
        bool IsPlaying { get; }
        void Stop();
    }

    /// <summary>
    /// 通用音频服务。AOT 提供 Unity 实现，HotUpdate 只依赖这个契约。
    /// </summary>
    public interface IAudioService : IService
    {
        IAudioPlaybackHandle PlayMusic(UnityEngine.AudioClip clip, AudioPlayParameters parameters);
        System.Threading.Tasks.Task<IAudioPlaybackHandle> PlayMusicAsync(string assetLocation, AudioPlayParameters parameters);
        IAudioPlaybackHandle PlaySfx(UnityEngine.AudioClip clip, AudioPlayParameters parameters);
        System.Threading.Tasks.Task<IAudioPlaybackHandle> PlaySfxAsync(string assetLocation, AudioPlayParameters parameters);
        void StopMusic();
        void StopAllSfx();
        void SetVolume(AudioChannel channel, float volume);
        float GetVolume(AudioChannel channel);
    }

    public static class AudioServiceRegistry
    {
        public static IAudioService Current { get; private set; }

        public static void Register(IAudioService audioService)
        {
            Current = audioService;
        }

        public static void Clear(IAudioService audioService)
        {
            if (object.ReferenceEquals(Current, audioService))
            {
                Current = null;
            }
        }
    }

    /// <summary>
    /// 服务容器接口（Shared层，供AOT和HotUpdate共用）
    /// </summary>
    public interface IServiceContainer
    {
        void RegisterSingleton<T>(T instance) where T : class;
        T Get<T>() where T : class;
        bool IsRegistered<T>();
    }

    /// <summary>
    /// Gatebreaker Arena 场景 UI 引用桥，只承载宿主场景里的显式引用。
    /// </summary>
    public interface IGatebreakerArenaSceneUiBinding
    {
        UnityEngine.Object SkillButtonObject { get; }
        UnityEngine.Object BallCountTextObject { get; }
        UnityEngine.Object HudRootObject { get; }
        UnityEngine.Object HudTitleTextObject { get; }
        UnityEngine.Object HudStatusTextObject { get; }
        UnityEngine.Object HudScoreTextObject { get; }
        UnityEngine.Object HudServeTextObject { get; }
        UnityEngine.Object HudBallTextObject { get; }
        UnityEngine.Object[] PlayerScoreTextObjects { get; }
        UnityEngine.Object[] PlayerHitTextObjects { get; }
        UnityEngine.Object ResultRootObject { get; }
        UnityEngine.Object ResultTitleTextObject { get; }
        UnityEngine.Object ResultBodyTextObject { get; }
        UnityEngine.Object ResultScoreTextObject { get; }
        UnityEngine.Object GmRootObject { get; }
        UnityEngine.Object GmHitOffsetSliderObject { get; }
        UnityEngine.Object GmHitOffsetValueTextObject { get; }
        UnityEngine.Object GmPaddleVelocitySliderObject { get; }
        UnityEngine.Object GmPaddleVelocityValueTextObject { get; }
        UnityEngine.Object GmMinimumOutwardSliderObject { get; }
        UnityEngine.Object GmMinimumOutwardValueTextObject { get; }
        UnityEngine.Object LanRootObject { get; }
        UnityEngine.Object LanCreateButtonObject { get; }
        UnityEngine.Object LanDiscoverButtonObject { get; }
        UnityEngine.Object LanJoinButtonObject { get; }
        UnityEngine.Object LanReadyButtonObject { get; }
        UnityEngine.Object LanStartButtonObject { get; }
        UnityEngine.Object LanLeaveButtonObject { get; }
        UnityEngine.Object LanAcknowledgeStartButtonObject { get; }
        UnityEngine.Object LanPlayerNameInputObject { get; }
        UnityEngine.Object LanRoomCodeInputObject { get; }
        UnityEngine.Object LanStateTextObject { get; }
        UnityEngine.Object LanRoomCodeTextObject { get; }
        UnityEngine.Object LanPlayerCountTextObject { get; }
        UnityEngine.Object LanLocalIpTextObject { get; }
        UnityEngine.Object LanRoomIpTextObject { get; }
        UnityEngine.Object LanErrorTextObject { get; }
    }

    public static class GatebreakerArenaSceneUiBindingRegistry
    {
        public static IGatebreakerArenaSceneUiBinding Current { get; private set; }

        public static void Register(IGatebreakerArenaSceneUiBinding binding)
        {
            Current = binding;
        }

        public static void Clear(IGatebreakerArenaSceneUiBinding binding)
        {
            if (object.ReferenceEquals(Current, binding))
            {
                Current = null;
            }
        }
    }

    /// <summary>
    /// 网络客户端接口（Shared层）
    /// </summary>
    public interface INetClient : IService
    {
        bool IsConnected { get; }
        System.Threading.Tasks.Task<TransportResponse> SendRequestAsync(string url, string method = "GET", byte[] body = null, System.Collections.Generic.Dictionary<string, string> headers = null);
    }

    /// <summary>
    /// 持久化接口（Shared层）
    /// </summary>
    public interface IPersistence
    {
        System.Threading.Tasks.Task<bool> SaveAsync(string key, byte[] data);
        System.Threading.Tasks.Task<byte[]> LoadAsync(string key);
        System.Threading.Tasks.Task<bool> DeleteAsync(string key);
        bool Exists(string key);
    }

    /// <summary>
    /// 微信平台桥接接口（Shared层）
    /// </summary>
    public interface IWeChatBridge : IService
    {
        System.Threading.Tasks.Task<string> LoginAsync();
        System.Threading.Tasks.Task<bool> ShowRewardedAdAsync(string adUnitId);
        System.Threading.Tasks.Task<bool> RequestPaymentAsync(string orderId, string paymentParams);
        bool TryGetWindowInfo(out WeChatWindowInfo windowInfo);
    }

    /// <summary>
    /// 资源句柄（Shared层，用于跨层传递加载结果）
    /// </summary>
    public interface IAssetHandle
    {
        UnityEngine.Object AssetObject { get; }
        void Release();
    }

    /// <summary>
    /// 资源服务接口（Shared层）
    /// </summary>
    public interface IAssetsRuntime
    {
        System.Threading.Tasks.Task InitializeAsync();
        System.Threading.Tasks.Task<bool> RunPatchFlowAsync(string packageVersion = null);
        System.Threading.Tasks.Task<IAssetHandle> LoadAssetAsync(string location);
        void Shutdown();
    }

    /// <summary>
    /// Tick接口（Shared层）
    /// </summary>
    public interface ITickable
    {
        void Tick(float deltaTime);
    }

    /// <summary>
    /// Tick管理器接口（Shared层）
    /// </summary>
    public interface ITickManager
    {
        void Register(ITickable tickable);
        void Unregister(ITickable tickable);
    }

    /// <summary>
    /// 请求进入对局的事件（Shared层，供AOT和HotUpdate层跨层通信）
    /// </summary>
    public class RequestEnterMatchEvent
    {
        public string SceneName { get; set; } = "ArenaMatch";
    }

    /// <summary>
    /// 事件总线接口（Shared层，供AOT和HotUpdate层跨层通信）
    /// </summary>
    /// <remarks>
    /// 作用域订阅句柄。
    /// 新事件系统推荐 UI 页面、弹窗、短生命周期对象保存这个句柄，并在销毁时调用 Dispose。
    /// 这样调用方不需要重新保存原始委托再手动 Unsubscribe，可以减少遗漏退订导致的残留监听。
    /// </remarks>
    public interface IEventSubscription : System.IDisposable
    {
    }

    /// <summary>
    /// 强类型事件总线。
    /// AOT 层提供具体实现，HotUpdate 层只依赖这个 Shared 契约发布和订阅事件。
    /// </summary>
    /// <remarks>
    /// Subscribe / Unsubscribe / Publish 是早期接口，继续保留给已有代码和测试使用。
    /// SubscribeScoped 是新接口，支持优先级、条件监听和 IDisposable 自动退订。
    /// 这两个模型共用同一条事件通道，方便旧代码渐进迁移到更安全的作用域订阅方式。
    /// </remarks>
    public interface IEventBus
    {
        /// <summary>
        /// 传统订阅方式。
        /// 调用方必须在生命周期结束时使用同一个 handler 调用 Unsubscribe。
        /// </summary>
        void Subscribe<T>(System.Action<T> handler) where T : class;

        /// <summary>
        /// 传统退订方式。
        /// 如果同一个 handler 被重复订阅，一次 Unsubscribe 只移除其中一个订阅。
        /// </summary>
        void Unsubscribe<T>(System.Action<T> handler) where T : class;

        /// <summary>
        /// 带生命周期句柄的订阅方式。
        /// </summary>
        /// <param name="handler">事件满足条件后实际执行的回调。</param>
        /// <param name="priority">优先级，数值越大越先执行；相同优先级保持订阅先后顺序。</param>
        /// <param name="condition">可选条件。返回 false 时跳过 handler；条件异常会被事件总线隔离。</param>
        /// <returns>可 Dispose 的订阅句柄，Dispose 可重复调用且不会报错。</returns>
        IEventSubscription SubscribeScoped<T>(
            System.Action<T> handler,
            int priority = 0,
            System.Predicate<T> condition = null) where T : class;

        /// <summary>
        /// 发布强类型事件。
        /// 只有订阅了同一个 T 类型的监听者会收到事件。
        /// </summary>
        void Publish<T>(T eventData) where T : class;
    }
}
