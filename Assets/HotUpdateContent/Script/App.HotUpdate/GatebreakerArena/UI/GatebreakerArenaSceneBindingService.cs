namespace App.HotUpdate.GatebreakerArena.UI
{
    public sealed class GatebreakerArenaSceneBindingService
    {
        public bool IsBound { get; private set; }

        public void MarkBound()
        {
            IsBound = true;
        }

        public void Clear()
        {
            IsBound = false;
        }
    }
}
