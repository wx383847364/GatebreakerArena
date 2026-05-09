using System;
using System.Collections.Generic;
using App.Shared.Contracts;

namespace App.AOT.Infrastructure.DI
{
    /// <summary>
    /// 极简依赖注入容器
    /// </summary>
    public class ServiceContainer : IServiceContainer
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        private readonly Dictionary<Type, Func<object>> _factories = new Dictionary<Type, Func<object>>();

        /// <summary>
        /// 注册单例服务
        /// </summary>
        public void RegisterSingleton<T>(T instance) where T : class
        {
            _services[typeof(T)] = instance;
        }

        /// <summary>
        /// 注册工厂方法
        /// </summary>
        public void RegisterFactory<T>(Func<T> factory) where T : class
        {
            _factories[typeof(T)] = () => factory();
        }

        /// <summary>
        /// 获取服务
        /// </summary>
        public T Get<T>() where T : class
        {
            var type = typeof(T);

            // 先查单例
            if (_services.TryGetValue(type, out var instance))
            {
                return instance as T;
            }

            // 再查工厂
            if (_factories.TryGetValue(type, out var factory))
            {
                var obj = factory();
                _services[type] = obj; // 缓存为单例
                return obj as T;
            }

            return null;
        }

        /// <summary>
        /// 检查服务是否已注册
        /// </summary>
        public bool IsRegistered<T>()
        {
            return _services.ContainsKey(typeof(T)) || _factories.ContainsKey(typeof(T));
        }

        /// <summary>
        /// 清除所有服务
        /// </summary>
        public void Clear()
        {
            _services.Clear();
            _factories.Clear();
        }
    }
}
