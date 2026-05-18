using App.HotUpdate.GatebreakerArena.AI;
using App.HotUpdate.GatebreakerArena.Match;
using App.HotUpdate.GatebreakerArena.Mode;
using App.HotUpdate.GatebreakerArena.Network;
using App.HotUpdate.GatebreakerArena.UI;
using App.Shared.Contracts;

namespace App.HotUpdate.GatebreakerArena.Application
{
    public sealed class GatebreakerArenaApplicationContext
    {
        public GatebreakerArenaApplicationContext(
            IServiceContainer services,
            IAppLogger logger,
            ITickManager tickManager,
            IEventBus eventBus,
            IAssetsRuntime assetsRuntime,
            GatebreakerModeCatalog modeCatalog,
            GatebreakerMatchRuntime matchRuntime,
            GatebreakerInputService inputService,
            GatebreakerAiService aiService,
            GatebreakerArenaHudPresenter hudPresenter,
            GatebreakerArenaSceneBindingService sceneBindingService,
            LanRoomService lanRoomService,
            LanRoomTransportBridge lanRoomTransportBridge,
            GatebreakerNetworkMatchController networkMatchController)
        {
            Services = services;
            Logger = logger;
            TickManager = tickManager;
            EventBus = eventBus;
            AssetsRuntime = assetsRuntime;
            ModeCatalog = modeCatalog;
            MatchRuntime = matchRuntime;
            InputService = inputService;
            AiService = aiService;
            HudPresenter = hudPresenter;
            SceneBindingService = sceneBindingService;
            LanRoomService = lanRoomService;
            LanRoomTransportBridge = lanRoomTransportBridge;
            NetworkMatchController = networkMatchController;
        }

        public IServiceContainer Services { get; }
        public IAppLogger Logger { get; }
        public ITickManager TickManager { get; }
        public IEventBus EventBus { get; }
        public IAssetsRuntime AssetsRuntime { get; }
        public GatebreakerModeCatalog ModeCatalog { get; }
        public GatebreakerMatchRuntime MatchRuntime { get; }
        public GatebreakerInputService InputService { get; }
        public GatebreakerAiService AiService { get; }
        public GatebreakerArenaHudPresenter HudPresenter { get; }
        public GatebreakerArenaSceneBindingService SceneBindingService { get; }
        public LanRoomService LanRoomService { get; }
        public LanRoomTransportBridge LanRoomTransportBridge { get; }
        public GatebreakerNetworkMatchController NetworkMatchController { get; }
    }
}
