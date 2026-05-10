using System.Threading.Tasks;
using App.HotUpdate.GatebreakerArena.AI;
using App.HotUpdate.GatebreakerArena.Application;
using App.HotUpdate.GatebreakerArena.Ball;
using App.HotUpdate.GatebreakerArena.Match;
using App.HotUpdate.GatebreakerArena.Mode;
using App.HotUpdate.GatebreakerArena.Serve;
using App.HotUpdate.GatebreakerArena.UI;
using App.HotUpdate.GatebreakerArena.Zone;
using App.Shared.Contracts;

namespace App.HotUpdate.GatebreakerArena.Bootstrap
{
    /// <summary>
    /// Gatebreaker Arena 热更业务骨架入口。
    /// 只负责组合 HotUpdate 玩法服务，不在这里实现具体对局规则。
    /// </summary>
    public static class GatebreakerArenaGameBootstrap
    {
        public static GatebreakerArenaApplicationContext Context { get; private set; }

        public static Task StartAsync(IServiceContainer serviceContainer)
        {
            if (serviceContainer == null)
            {
                throw new System.ArgumentNullException(nameof(serviceContainer));
            }

            if (Context != null)
            {
                Context.Logger?.LogWarning("GatebreakerArenaGameBootstrap: 业务骨架已启动，跳过重复初始化。");
                return Task.CompletedTask;
            }

            IAppLogger logger = serviceContainer.Get<IAppLogger>();
            ITickManager tickManager = serviceContainer.Get<ITickManager>();
            IEventBus eventBus = serviceContainer.Get<IEventBus>();
            IAssetsRuntime assetsRuntime = serviceContainer.Get<IAssetsRuntime>();

            if (logger == null || tickManager == null || eventBus == null || assetsRuntime == null)
            {
                throw new System.InvalidOperationException("GatebreakerArenaGameBootstrap: AOT 基础设施依赖不完整。");
            }

            var modeCatalog = GatebreakerModeCatalog.CreateDefault();
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
            tickManager.Register(matchRuntime);

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
                sceneBindingService);

            matchRuntime.StartLocalPrototype();
            logger.LogInfo("GatebreakerArenaGameBootstrap: 本地可玩原型业务骨架启动完成。");
            return Task.CompletedTask;
        }
    }
}
