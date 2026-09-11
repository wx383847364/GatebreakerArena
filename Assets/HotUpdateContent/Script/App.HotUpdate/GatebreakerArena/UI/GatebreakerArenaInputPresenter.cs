using System.Collections.Generic;
using App.HotUpdate.GatebreakerArena.Application;
using UnityEngine;

namespace App.HotUpdate.GatebreakerArena.UI
{
    // 仅转换输入状态，技能与挡板运动规则仍由对局负责。
    public sealed class GatebreakerArenaInputPresenter
    {
        private readonly List<int> _touchOrder = new List<int>();
        private readonly List<int> _blockedTouchIds = new List<int>();

        // 重置触点归属，恢复操作后必须重新按下，避免旧触摸穿透菜单或暂停界面。
        public void ResetMovement(IReadOnlyList<Touch> currentTouches = null)
        {
            _touchOrder.Clear();
            _blockedTouchIds.Clear();
            if (currentTouches == null) return;
            for (int i = 0; i < currentTouches.Count; i++)
            {
                Touch touch = currentTouches[i];
                if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled)
                    _blockedTouchIds.Add(touch.fingerId);
            }
        }

        // 按最早按下的有效触点解析全屏方向；触摸存在及释放当帧独占移动轴。
        public float ResolveMoveAxis(IReadOnlyList<Touch> touches, float screenWidth,
            bool movementEnabled, float fallbackAxis, out bool pointerOwnsMovement)
        {
            pointerOwnsMovement = touches.Count > 0 || _touchOrder.Count > 0;
            if (!movementEnabled)
            {
                // 仅阻止界面上已有的手指，进入战斗后新按下的手指仍可当帧响应。
                ResetMovement(touches);
                pointerOwnsMovement = true;
                return 0f;
            }
            for (int i = _blockedTouchIds.Count - 1; i >= 0; i--)
            {
                if (!TryGetActiveTouch(touches, _blockedTouchIds[i], out _))
                    _blockedTouchIds.RemoveAt(i);
            }

            for (int i = 0; i < touches.Count; i++)
            {
                Touch touch = touches[i];
                if (touch.phase == TouchPhase.Began && !_blockedTouchIds.Contains(touch.fingerId) &&
                    !_touchOrder.Contains(touch.fingerId))
                    _touchOrder.Add(touch.fingerId);
            }

            // 触点数组顺序可能变化，按 fingerId 保持归属；遗漏结束事件也会清理。
            for (int i = _touchOrder.Count - 1; i >= 0; i--)
            {
                if (!TryGetActiveTouch(touches, _touchOrder[i], out _))
                    _touchOrder.RemoveAt(i);
            }

            if (_touchOrder.Count > 0 && TryGetActiveTouch(touches, _touchOrder[0], out Touch owner))
                return screenWidth > 0f ? (owner.position.x < screenWidth * 0.5f ? -1f : 1f) : 0f;

            return pointerOwnsMovement ? 0f : Mathf.Clamp(fallbackAxis, -1f, 1f);
        }

        // 查找仍按住的指定手指，结束和取消均视为释放。
        private static bool TryGetActiveTouch(IReadOnlyList<Touch> touches, int fingerId, out Touch active)
        {
            for (int i = 0; i < touches.Count; i++)
            {
                Touch touch = touches[i];
                if (touch.fingerId == fingerId && touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled)
                {
                    active = touch;
                    return true;
                }
            }
            active = default;
            return false;
        }

        // 将按钮与方向状态转发为玩家输入帧，不消费技能或发球资源。
        public PlayerInputFrame BuildFrame(
            int playerId,
            float moveAxis,
            bool servePressed,
            Vector2 aimDirection,
            bool abilityPressed)
        {
            return new PlayerInputFrame(playerId, moveAxis, servePressed, aimDirection, abilityPressed);
        }
    }
}
