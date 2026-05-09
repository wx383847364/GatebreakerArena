using System.Threading.Tasks;
using App.Shared.Contracts;

namespace App.HotUpdate.GatebreakerArena.Bootstrap
{
    /// <summary>
    /// Gatebreaker Arena 热更业务骨架入口。
    /// 第一轮迁移只注册占位启动链路，正式玩法模块后续从这里接入。
    /// </summary>
    public static class GatebreakerArenaGameBootstrap
    {
        public static Task StartAsync(IServiceContainer serviceContainer)
        {
            IAppLogger logger = serviceContainer?.Get<IAppLogger>();
            logger?.LogInfo("GatebreakerArenaGameBootstrap: 占位业务骨架启动完成。");
            return Task.CompletedTask;
        }
    }
}
