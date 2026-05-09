using App.Shared.Contracts;

namespace App.AOT.Infrastructure.Tick
{
    /// <summary>
    /// 主线程调度与定时器管理器
    /// </summary>
    public class TickManager : IService, ITickManager
    {
        private System.Collections.Generic.List<ITickable> _tickables = new System.Collections.Generic.List<ITickable>();
        private System.Collections.Generic.List<ITimer> _timers = new System.Collections.Generic.List<ITimer>();

        public void Initialize()
        {
        }

        public void Update(float deltaTime)
        {
            // 更新所有可tick对象
            for (int i = _tickables.Count - 1; i >= 0; i--)
            {
                if (i < _tickables.Count)
                {
                    _tickables[i].Tick(deltaTime);
                }
            }

            // 更新所有定时器
            for (int i = _timers.Count - 1; i >= 0; i--)
            {
                if (i < _timers.Count)
                {
                    var timer = _timers[i];
                    timer.Update(deltaTime);
                    if (timer.IsExpired)
                    {
                        timer.OnExpired?.Invoke();
                        if (timer.AutoRemove)
                        {
                            _timers.RemoveAt(i);
                        }
                    }
                }
            }
        }

        public void Shutdown()
        {
            _tickables.Clear();
            _timers.Clear();
        }

        public void Register(ITickable tickable)
        {
            if (!_tickables.Contains(tickable))
            {
                _tickables.Add(tickable);
            }
        }

        public void Unregister(ITickable tickable)
        {
            _tickables.Remove(tickable);
        }

        public ITimer CreateTimer(float duration, System.Action onExpired, bool autoRemove = true)
        {
            var timer = new Timer(duration, onExpired, autoRemove);
            _timers.Add(timer);
            return timer;
        }
    }

    public interface ITimer
    {
        bool IsExpired { get; }
        bool AutoRemove { get; }
        System.Action OnExpired { get; set; }
        void Update(float deltaTime);
    }

    public class Timer : ITimer
    {
        private float _duration;
        private float _elapsed;

        public bool IsExpired => _elapsed >= _duration;
        public bool AutoRemove { get; }
        public System.Action OnExpired { get; set; }

        public Timer(float duration, System.Action onExpired, bool autoRemove)
        {
            _duration = duration;
            OnExpired = onExpired;
            AutoRemove = autoRemove;
        }

        public void Update(float deltaTime)
        {
            _elapsed += deltaTime;
        }
    }
}
