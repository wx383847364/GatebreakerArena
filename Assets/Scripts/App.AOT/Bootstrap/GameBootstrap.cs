using System;
using UnityEngine;
using App.Shared.Contracts;
using App.AOT.Infrastructure.DI;
using App.AOT.Infrastructure.Logger;
using App.AOT.Infrastructure.Tick;
using App.AOT.Infrastructure.EventBus;
using App.AOT.Infrastructure.Persistence;
using App.AOT.YooRuntimeAssets;
using App.AOT.HotUpdate;
using App.AOT.Networking;
using App.AOT.Platform.WeChat;

namespace App.AOT.Bootstrap
{
    /// <summary>
    /// 游戏启动器：AOT 层入口，挂在首场景。
    /// 这一层只负责宿主基础设施初始化，不承载 Gatebreaker Arena 业务规则。
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        // 下面这些字段都属于“宿主能力”：
        // HotUpdate 层通过接口拿到它们，而不是直接依赖这些具体实现。
        private ServiceContainer _serviceContainer;
        private UnityLogger _logger;
        private TickManager _tickManager;
        private EventBus _eventBus;
        private FilePersistenceProvider _persistence;
        private YooAssetsRuntime _yooAssets;
        private HybridClrLoader _hybridClrLoader;
        private NetClient _netClient;
        private WeChatBridge _weChatBridge;

        private async void Start()
        {
            // 启动器跨场景常驻；真正业务入口会在基础设施准备完后进入 HotUpdate。
            DontDestroyOnLoad(gameObject);
            await InitializeAsync();
        }

        /// <summary>
        /// 获取服务容器（供AOT层其他组件使用）
        /// </summary>
        public IServiceContainer GetServiceContainer()
        {
            return _serviceContainer;
        }

        private async System.Threading.Tasks.Task InitializeAsync()
        {
            // 初始化顺序很重要：
            // 1. 先起日志和容器，保证后续任何失败都能输出诊断信息。
            InitializeInfrastructure();

            _logger.LogInfo("GameBootstrap: 开始初始化...");

            // 2. 宿主平台桥接，例如微信窗口信息、登录、广告等。
            InitializePlatform();

            // 3. 网络底座。
            InitializeNetworking();

            // 4. 正式运行时资源入口。
            await InitializeYooAssetsAsync();

            // 5. 最后才进入 HybridCLR / HotUpdate。
            await InitializeHybridClrAsync();

            _logger.LogInfo("GameBootstrap: 初始化完成，进入热更层");
        }

        private void InitializeInfrastructure()
        {
            _serviceContainer = new ServiceContainer();

            _logger = new UnityLogger();
            _logger.Initialize();
            // 同时注册为接口和具体类型，方便 AOT 和 HotUpdate 两边统一取用。
            _serviceContainer.RegisterSingleton<IAppLogger>(_logger);

            _tickManager = new TickManager();
            _tickManager.Initialize();
            _serviceContainer.RegisterSingleton<TickManager>(_tickManager);
            _serviceContainer.RegisterSingleton<ITickManager>(_tickManager);

            _eventBus = new EventBus();
            _serviceContainer.RegisterSingleton<EventBus>(_eventBus);
            _serviceContainer.RegisterSingleton<IEventBus>(_eventBus);

            _persistence = new FilePersistenceProvider();
            _serviceContainer.RegisterSingleton<IPersistence>(_persistence);

            // 把容器自己也注册进去，后面 HotUpdate 业务组合层会从这里继续挂服务。
            _serviceContainer.RegisterSingleton<IServiceContainer>(_serviceContainer);

            _logger.LogInfo("基础设施初始化完成");
        }

        private void InitializePlatform()
        {
            _weChatBridge = new WeChatBridge();
            _weChatBridge.SetLogger(_logger);
            _weChatBridge.Initialize();
            _serviceContainer.RegisterSingleton<IWeChatBridge>(_weChatBridge);
            _logger.LogInfo("平台桥接初始化完成");
        }

        private void InitializeNetworking()
        {
            _netClient = new NetClient();
            _netClient.SetLogger(_logger);
            _netClient.Initialize();
            _serviceContainer.RegisterSingleton<INetClient>(_netClient);
            _logger.LogInfo("网络客户端初始化完成");
        }

        private async System.Threading.Tasks.Task InitializeYooAssetsAsync()
        {
            _yooAssets = new YooAssetsRuntime(_logger);
            await _yooAssets.InitializeAsync();
            _serviceContainer.RegisterSingleton<IAssetsRuntime>(_yooAssets);
            _serviceContainer.RegisterSingleton<YooAssetsRuntime>(_yooAssets);
            _logger.LogInfo("YooAssets初始化完成");
        }

        private async System.Threading.Tasks.Task InitializeHybridClrAsync()
        {
            // HotUpdate 入口加载完成后，会在热更新层里继续组装 Gatebreaker Arena 正式业务骨架。
            _hybridClrLoader = new HybridClrLoader(_logger, _yooAssets, _serviceContainer);
            await _hybridClrLoader.LoadAsync();
            _logger.LogInfo("HybridCLR热更代码加载完成");
        }

        private void Update()
        {
            // 这里仍然只推进宿主级服务；业务逐帧逻辑若存在，会通过 HotUpdate 自己接入 TickManager。
            var deltaTime = Time.deltaTime;
            _tickManager?.Update(deltaTime);
            _netClient?.Update(deltaTime);
        }

        private void OnDestroy()
        {
            _hybridClrLoader?.Shutdown();
            _yooAssets?.Shutdown();
            _netClient?.Shutdown();
            _weChatBridge?.Shutdown();
            _tickManager?.Shutdown();
            _logger?.Shutdown();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
        }

        private void OnApplicationQuit()
        {
        }
    }
}
