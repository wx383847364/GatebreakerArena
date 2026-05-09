using System;
using App.Shared.Contracts;
using UnityEngine;

namespace App.AOT.Platform.WeChat
{
    /// <summary>
    /// 微信平台桥接：JSBridge 封装（登录、广告、支付唤起、网络状态）。
    /// 当前项目里“窗口尺寸 / 安全区”这条链也经由这里向 UI 层提供。
    /// </summary>
    public class WeChatBridge : IWeChatBridge
    {
        private IAppLogger _logger;
        private WeChatWindowInfo _windowInfo;

        public WeChatBridge()
        {
        }

        /// <summary>
        /// 设置日志器（通过DI注入）
        /// </summary>
        public void SetLogger(IAppLogger logger)
        {
            _logger = logger;
        }

        public void Initialize()
        {
            _logger?.LogInfo("WeChatBridge: 初始化微信桥接...");
            RefreshWindowInfo();
        }

        public void Update(float deltaTime)
        {
            // 不需要每帧更新
        }

        public void Shutdown()
        {
            _logger?.LogInfo("WeChatBridge: 已关闭");
        }

        public bool TryGetWindowInfo(out WeChatWindowInfo windowInfo)
        {
            // 目前真实微信 JSBridge 尚未接入，所以这里先走 Screen.safeArea 的 fallback 路径。
            RefreshWindowInfo();
            if (_windowInfo == null || !_windowInfo.IsAvailable)
            {
                windowInfo = null;
                return false;
            }

            windowInfo = _windowInfo.Clone();
            return true;
        }

        /// <summary>
        /// 微信登录
        /// </summary>
        public async System.Threading.Tasks.Task<string> LoginAsync()
        {
            _logger?.LogInfo("WeChatBridge: 开始微信登录...");
            // TODO: 调用微信登录API
            await System.Threading.Tasks.Task.Delay(100);
            return "mock_code"; // 返回临时code
        }

        /// <summary>
        /// 显示激励视频广告
        /// </summary>
        public async System.Threading.Tasks.Task<bool> ShowRewardedAdAsync(string adUnitId)
        {
            _logger?.LogInfo("WeChatBridge: 显示激励视频广告: {0}", adUnitId);
            // TODO: 调用微信广告API
            await System.Threading.Tasks.Task.Delay(100);
            return true;
        }

        /// <summary>
        /// 发起支付
        /// </summary>
        public async System.Threading.Tasks.Task<bool> RequestPaymentAsync(string orderId, string paymentParams)
        {
            _logger?.LogInfo("WeChatBridge: 发起支付: {0}", orderId);
            // TODO: 调用微信支付API
            await System.Threading.Tasks.Task.Delay(100);
            return true;
        }

        /// <summary>
        /// 获取网络状态
        /// </summary>
        public NetworkState GetNetworkState()
        {
            // TODO: 从微信API获取网络状态
            return NetworkState.Unknown;
        }

        private void RefreshWindowInfo()
        {
            // 先把 Unity Screen.safeArea 转成项目内部统一的 WeChatWindowInfo 结构，
            // 后续接真实 JSBridge 时，这层接口可以保持不变。
            Rect safeArea = Screen.safeArea;
            int screenWidth = Mathf.Max(Screen.width, 1);
            int screenHeight = Mathf.Max(Screen.height, 1);

            int left = Mathf.RoundToInt(safeArea.xMin);
            int bottom = Mathf.RoundToInt(safeArea.yMin);
            int safeWidth = Mathf.RoundToInt(safeArea.width);
            int safeHeight = Mathf.RoundToInt(safeArea.height);
            int right = Mathf.Max(0, screenWidth - left - safeWidth);
            int top = Mathf.Max(0, screenHeight - bottom - safeHeight);

            // 只有窗口信息真的变化时才刷新并打印日志，避免 UI 轮询把 Console 刷爆。
            if (!HasWindowInfoChanged(screenWidth, screenHeight, left, right, top, bottom, safeWidth, safeHeight))
            {
                return;
            }

            _windowInfo = new WeChatWindowInfo
            {
                ScreenWidth = screenWidth,
                ScreenHeight = screenHeight,
                WindowWidth = screenWidth,
                WindowHeight = screenHeight,
                PixelRatio = 1f,
                StatusBarHeight = top,
                IsFallback = true,
                IsAvailable = true,
                SafeArea = new UiSafeAreaInfo
                {
                    Left = left,
                    Right = right,
                    Top = top,
                    Bottom = bottom,
                    Width = safeWidth,
                    Height = safeHeight,
                },
            };

            _logger?.LogDebug(
                "WeChatBridge: window info refreshed. screen={0}x{1}, safeArea={2},{3},{4},{5}, top={6}, bottom={7}, left={8}, right={9}",
                _windowInfo.ScreenWidth,
                _windowInfo.ScreenHeight,
                safeArea.xMin,
                safeArea.yMin,
                safeArea.width,
                safeArea.height,
                top,
                bottom,
                left,
                right);
        }

        private bool HasWindowInfoChanged(int screenWidth, int screenHeight, int left, int right, int top, int bottom, int safeWidth, int safeHeight)
        {
            // 这里只比较 UI 真正关心的字段：屏幕尺寸和安全区边距。
            if (_windowInfo == null)
            {
                return true;
            }

            UiSafeAreaInfo safeArea = _windowInfo.SafeArea;
            if (safeArea == null)
            {
                return true;
            }

            return _windowInfo.ScreenWidth != screenWidth ||
                   _windowInfo.ScreenHeight != screenHeight ||
                   _windowInfo.WindowWidth != screenWidth ||
                   _windowInfo.WindowHeight != screenHeight ||
                   safeArea.Left != left ||
                   safeArea.Right != right ||
                   safeArea.Top != top ||
                   safeArea.Bottom != bottom ||
                   safeArea.Width != safeWidth ||
                   safeArea.Height != safeHeight;
        }
    }

    public enum NetworkState
    {
        Unknown,
        None,
        Wifi,
        Mobile
    }
}
