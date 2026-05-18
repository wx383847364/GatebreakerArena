using System.Threading.Tasks;
using App.HotUpdate.GatebreakerArena.AI;
using App.HotUpdate.GatebreakerArena.Application;
using App.HotUpdate.GatebreakerArena.Ball;
using App.HotUpdate.GatebreakerArena.Match;
using App.HotUpdate.GatebreakerArena.Mode;
using App.HotUpdate.GatebreakerArena.Network;
using App.HotUpdate.GatebreakerArena.Prototype;
using App.HotUpdate.GatebreakerArena.Serve;
using App.HotUpdate.GatebreakerArena.UI;
using App.HotUpdate.GatebreakerArena.Zone;
using App.Shared.Contracts;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.Bootstrap
{
    /// <summary>
    /// Gatebreaker Arena 热更业务骨架入口。
    /// 只负责组合 HotUpdate 玩法服务，不在这里实现具体对局规则。
    /// </summary>
    public static class GatebreakerArenaGameBootstrap
    {
        public static GatebreakerArenaApplicationContext Context { get; private set; }

        public static async Task StartAsync(IServiceContainer serviceContainer)
        {
            if (serviceContainer == null)
            {
                throw new System.ArgumentNullException(nameof(serviceContainer));
            }

            if (Context != null)
            {
                Context.Logger?.LogWarning("GatebreakerArenaGameBootstrap: 业务骨架已启动，跳过重复初始化。");
                return;
            }

            IAppLogger logger = serviceContainer.Get<IAppLogger>();
            ITickManager tickManager = serviceContainer.Get<ITickManager>();
            IEventBus eventBus = serviceContainer.Get<IEventBus>();
            IAssetsRuntime assetsRuntime = serviceContainer.Get<IAssetsRuntime>();

            if (logger == null || tickManager == null || eventBus == null || assetsRuntime == null)
            {
                throw new System.InvalidOperationException("GatebreakerArenaGameBootstrap: AOT 基础设施依赖不完整。");
            }

            var configLoader = new GatebreakerConfigRuntimeLoader();
            GatebreakerConfigLoadResult configLoadResult = await configLoader.LoadAsync(assetsRuntime);
            GatebreakerModeCatalog modeCatalog;
            if (configLoadResult.Succeeded)
            {
                modeCatalog = configLoadResult.Catalog;
                logger.LogInfo(
                    "GatebreakerArenaGameBootstrap: 已加载 Gatebreaker 配置。source={0}, version={1}",
                    configLoadResult.Source,
                    configLoadResult.Version.HasValue ? configLoadResult.Version.Value.ToString() : "unknown");
            }
            else
            {
                modeCatalog = GatebreakerModeCatalog.CreateDefault();
                logger.LogWarning(
                    "GatebreakerArenaGameBootstrap: 配置加载失败，Editor 原型回退默认值。reason={0}, message={1}",
                    configLoadResult.FailureReason,
                    configLoadResult.Message);
            }

            var ballSimulation = new BallSimulationSystem();
            var serveResourceSystem = new ServeResourceSystem();
            var goalJudgeSystem = new GoalJudgeSystem();
            var scoreSystem = new ScoreSystem();
            var matchRuntime = new GatebreakerMatchRuntime(
                modeCatalog,
                ballSimulation,
                serveResourceSystem,
                goalJudgeSystem,
                scoreSystem,
                logger);
            var inputService = new GatebreakerInputService();
            var aiService = new GatebreakerAiService();
            var hudPresenter = new GatebreakerArenaHudPresenter(matchRuntime);
            var sceneBindingService = new GatebreakerArenaSceneBindingService();
            var lanRoomService = new LanRoomService(logger);
            ILanTransport lanTransport = serviceContainer.Get<ILanTransport>();
            LanRoomTransportBridge lanRoomTransportBridge = lanTransport != null
                ? new LanRoomTransportBridge(lanRoomService, lanTransport)
                : null;
            var networkMatchController = new GatebreakerNetworkMatchController(lanRoomService, matchRuntime);

            serviceContainer.RegisterSingleton(configLoader);
            serviceContainer.RegisterSingleton(modeCatalog);
            serviceContainer.RegisterSingleton(ballSimulation);
            serviceContainer.RegisterSingleton(serveResourceSystem);
            serviceContainer.RegisterSingleton(goalJudgeSystem);
            serviceContainer.RegisterSingleton(scoreSystem);
            serviceContainer.RegisterSingleton(matchRuntime);
            serviceContainer.RegisterSingleton(inputService);
            serviceContainer.RegisterSingleton(aiService);
            serviceContainer.RegisterSingleton(hudPresenter);
            serviceContainer.RegisterSingleton(sceneBindingService);
            serviceContainer.RegisterSingleton(lanRoomService);
            serviceContainer.RegisterSingleton(networkMatchController);
            if (lanRoomTransportBridge != null)
            {
                serviceContainer.RegisterSingleton(lanRoomTransportBridge);
            }
            tickManager.Register(networkMatchController);
            tickManager.Register(lanRoomService);

            Context = new GatebreakerArenaApplicationContext(
                serviceContainer,
                logger,
                tickManager,
                eventBus,
                assetsRuntime,
                modeCatalog,
                matchRuntime,
                inputService,
                aiService,
                hudPresenter,
                sceneBindingService,
                lanRoomService,
                lanRoomTransportBridge,
                networkMatchController);

            matchRuntime.StartLocalPrototype();
            CreatePrototypeRunner(Context);
            logger.LogInfo("GatebreakerArenaGameBootstrap: 本地可玩原型业务骨架启动完成。");
        }

        private static void CreatePrototypeRunner(GatebreakerArenaApplicationContext context)
        {
            var runnerObject = new GameObject("Gatebreaker Prototype Runner");
            UnityEngine.Object.DontDestroyOnLoad(runnerObject);
            GatebreakerPrototypeRunner runner = runnerObject.AddComponent<GatebreakerPrototypeRunner>();
            runner.Initialize(context);
        }
    }
}
