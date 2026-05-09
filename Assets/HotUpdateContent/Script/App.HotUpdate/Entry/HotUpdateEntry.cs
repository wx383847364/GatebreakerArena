using System;
using System.Threading.Tasks;
using App.Shared.Contracts;
using App.HotUpdate.GatebreakerArena.Bootstrap;

namespace App.HotUpdate.Entry
{
    /// <summary>
    /// 热更层总入口。
    /// AOT 会通过反射调用这里的 Start 方法，HotUpdate 再把控制权交给 Gatebreaker Arena 的正式业务骨架。
    /// </summary>
    public static class HotUpdateEntry
    {
        /// <summary>
        /// 启动热更层。
        /// 这里保持入口签名稳定，避免影响 AOT 的反射加载逻辑。
        /// </summary>
        public static void Start(IServiceContainer serviceContainer)
        {
            StartAsync(serviceContainer).GetAwaiter().GetResult();
        }

        public static async Task StartAsync(IServiceContainer serviceContainer)
        {
            var logger = serviceContainer?.Get<IAppLogger>();
            try
            {
                logger?.LogInfo("HotUpdateEntry: 热更层启动，准备进入 Gatebreaker Arena 业务骨架。");

                await GatebreakerArenaGameBootstrap.StartAsync(serviceContainer);

                logger?.LogInfo("HotUpdateEntry: Gatebreaker Arena 业务骨架接线完成。");
            }
            catch (Exception ex)
            {
                logger?.LogError("HotUpdateEntry: Gatebreaker Arena 业务骨架启动失败。{0}", ex);
                throw;
            }
        }
    }
}
