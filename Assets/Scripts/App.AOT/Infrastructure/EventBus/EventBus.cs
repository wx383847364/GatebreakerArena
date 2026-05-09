using System;
using System.Collections.Generic;
using App.Shared.Contracts;

namespace App.AOT.Infrastructure.EventBus
{
    /// <summary>
    /// 事件总线：支持强类型事件
    /// </summary>
    /// <remarks>
    /// 这个类是 Shared 层 IEventBus 的 AOT 实现，负责承接 HotUpdate 业务发布的强类型事件。
    /// 它不理解 Gatebreaker Arena 的玩法含义，只负责“把 T 类型事件交给订阅了 T 的回调”。
    ///
    /// 本实现有几个关键约定：
    /// 1. 发布前会复制监听列表快照，避免 handler 内部订阅/退订时修改正在遍历的集合。
    /// 2. priority 越大越先执行；priority 相同时按订阅顺序执行，保证流程稳定可排查。
    /// 3. condition 和 handler 的异常都会被隔离并记录日志，避免一个监听者拖垮整条事件链。
    /// 4. SubscribeScoped 返回的句柄可以重复 Dispose，适合 UI 页面在 OnDestroy 中兜底释放。
    /// </remarks>
    public class EventBus : IEventBus
    {
        // 每个事件类型各自维护监听列表，Key 是事件 DTO 的运行时 Type。
        private readonly Dictionary<Type, List<HandlerEntry>> _handlers = new Dictionary<Type, List<HandlerEntry>>();

        // 订阅序号用于稳定排序：同优先级下，先订阅的监听者先执行。
        private long _nextOrder;

        /// <summary>
        /// 传统订阅入口。
        /// 内部复用 SubscribeScoped，但不把返回句柄暴露出去，兼容旧代码的显式 Unsubscribe 模式。
        /// </summary>
        public void Subscribe<T>(Action<T> handler) where T : class
        {
            SubscribeScoped(handler);
        }

        /// <summary>
        /// 传统退订入口。
        /// 如果同一个 handler 被重复订阅，这里只移除一个匹配项，保持和常见 C# event -= 的直觉一致。
        /// </summary>
        public void Unsubscribe<T>(Action<T> handler) where T : class
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var handlers))
            {
                for (int i = 0; i < handlers.Count; i++)
                {
                    if (handlers[i].Matches(handler))
                    {
                        handlers[i].IsDisposed = true;
                        handlers.RemoveAt(i);
                        break;
                    }
                }

                if (handlers.Count == 0)
                {
                    _handlers.Remove(type);
                }
            }
        }

        /// <summary>
        /// 新的推荐订阅入口。
        /// 调用方可以保存返回的 IEventSubscription，在页面销毁、弹窗关闭或服务释放时 Dispose。
        /// </summary>
        public IEventSubscription SubscribeScoped<T>(
            Action<T> handler,
            int priority = 0,
            Predicate<T> condition = null) where T : class
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var handlers))
            {
                handlers = new List<HandlerEntry>();
                _handlers[type] = handlers;
            }

            var entry = new HandlerEntry(
                handler,
                condition,
                priority,
                _nextOrder++);
            handlers.Add(entry);
            return new EventSubscription(this, type, entry);
        }

        /// <summary>
        /// 发布事件。
        /// 这里会按照快照执行，因此当前发布过程中新增的监听者不会收到本次事件；
        /// 当前发布过程中被退订的监听者，如果还没轮到执行，会被 IsDisposed / ContainsHandler 检查跳过。
        /// </summary>
        public void Publish<T>(T eventData) where T : class
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var handlers))
            {
                HandlerEntry[] snapshot = handlers.ToArray();
                Array.Sort(snapshot, CompareHandlers);
                foreach (var handler in snapshot)
                {
                    if (handler.IsDisposed || !ContainsHandler(type, handler))
                    {
                        continue;
                    }

                    if (handler.Condition is Predicate<T> condition)
                    {
                        bool shouldHandle;
                        try
                        {
                            shouldHandle = condition(eventData);
                        }
                        catch (Exception ex)
                        {
                            UnityEngine.Debug.LogError($"EventBus: Error evaluating condition for event {type.Name}: {ex}");
                            continue;
                        }

                        if (!shouldHandle)
                        {
                            continue;
                        }
                    }

                    if (handler.Handler is Action<T> action)
                    {
                        try
                        {
                            action(eventData);
                        }
                        catch (Exception ex)
                        {
                            UnityEngine.Debug.LogError($"EventBus: Error handling event {type.Name}: {ex}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 清除所有订阅。
        /// 主要用于测试或应用关闭阶段；清除前会把条目标记为 disposed，让已经拿到的句柄也进入失效状态。
        /// </summary>
        public void Clear()
        {
            foreach (var pair in _handlers)
            {
                foreach (var handler in pair.Value)
                {
                    handler.IsDisposed = true;
                }
            }

            _handlers.Clear();
        }

        /// <summary>
        /// 事件执行顺序：
        /// priority 大的先执行；priority 相同则订阅 order 小的先执行。
        /// </summary>
        private static int CompareHandlers(HandlerEntry left, HandlerEntry right)
        {
            int priorityComparison = right.Priority.CompareTo(left.Priority);
            return priorityComparison != 0
                ? priorityComparison
                : left.Order.CompareTo(right.Order);
        }

        /// <summary>
        /// 快照发布时的安全检查。
        /// 如果监听者已经在本次发布过程中被退订，即使还留在快照数组里也不会再执行。
        /// </summary>
        private bool ContainsHandler(Type type, HandlerEntry entry)
        {
            return _handlers.TryGetValue(type, out var handlers) && handlers.Contains(entry);
        }

        /// <summary>
        /// 作用域订阅句柄最终会调用这里移除监听。
        /// entry.IsDisposed 先置位，确保快照发布中的后续检查能立刻看到退订状态。
        /// </summary>
        private void Remove(Type type, HandlerEntry entry)
        {
            if (entry == null || entry.IsDisposed)
            {
                return;
            }

            entry.IsDisposed = true;
            if (_handlers.TryGetValue(type, out var handlers))
            {
                handlers.Remove(entry);
                if (handlers.Count == 0)
                {
                    _handlers.Remove(type);
                }
            }
        }

        /// <summary>
        /// 单个监听者的内部记录。
        /// Handler 是真正的 Action&lt;T&gt;，Condition 是可选 Predicate&lt;T&gt;。
        /// 这里用 object 保存是为了让不同 T 的委托可以统一放进同一个内部结构里。
        /// </summary>
        private sealed class HandlerEntry
        {
            public HandlerEntry(object handler, object condition, int priority, long order)
            {
                Handler = handler;
                Condition = condition;
                Priority = priority;
                Order = order;
            }

            public object Handler { get; }

            public object Condition { get; }

            public int Priority { get; }

            public long Order { get; }

            public bool IsDisposed { get; set; }

            /// <summary>
            /// 用于传统 Unsubscribe 匹配委托。
            /// </summary>
            public bool Matches(object handler)
            {
                return ReferenceEquals(Handler, handler) || Equals(Handler, handler);
            }
        }

        /// <summary>
        /// SubscribeScoped 返回给调用方的退订句柄。
        /// 它只保存 EventBus、事件类型和内部 entry，不暴露具体集合结构。
        /// </summary>
        private sealed class EventSubscription : IEventSubscription
        {
            private readonly EventBus _eventBus;
            private readonly Type _type;
            private readonly HandlerEntry _entry;
            private bool _disposed;

            public EventSubscription(EventBus eventBus, Type type, HandlerEntry entry)
            {
                _eventBus = eventBus;
                _type = type;
                _entry = entry;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _eventBus.Remove(_type, _entry);
            }
        }
    }
}
