using UnityEngine;

namespace App.AOT.Bootstrap
{
    /// <summary>
    /// Keeps gameplay and UI cameras inside a centered square viewport.
    /// </summary>
    public sealed class SquareViewportCameraAdapter : MonoBehaviour
    {
        [SerializeField]
        private Camera[] _cameras;

        [SerializeField]
        private bool _updateEveryFrame = true;

        private int _lastScreenWidth = -1;
        private int _lastScreenHeight = -1;

        private void Awake()
        {
            ApplyIfChanged(true);
        }

        private void OnEnable()
        {
            ApplyIfChanged(true);
        }

        private void LateUpdate()
        {
            if (_updateEveryFrame)
            {
                ApplyIfChanged(false);
            }
        }

        private void ApplyIfChanged(bool force)
        {
            int screenWidth = Mathf.Max(Screen.width, 1);
            int screenHeight = Mathf.Max(Screen.height, 1);
            if (!force && screenWidth == _lastScreenWidth && screenHeight == _lastScreenHeight)
            {
                return;
            }

            _lastScreenWidth = screenWidth;
            _lastScreenHeight = screenHeight;
            Rect viewport = CalculateSquareViewport(screenWidth, screenHeight);

            if (_cameras == null)
            {
                return;
            }

            for (int i = 0; i < _cameras.Length; i++)
            {
                Camera camera = _cameras[i];
                if (camera != null)
                {
                    camera.rect = viewport;
                }
            }
        }

        public static Rect CalculateSquareViewport(int screenWidth, int screenHeight)
        {
            int width = Mathf.Max(screenWidth, 1);
            int height = Mathf.Max(screenHeight, 1);
            float side = Mathf.Min(width, height);
            return new Rect(
                (width - side) * 0.5f / width,
                (height - side) * 0.5f / height,
                side / width,
                side / height);
        }
    }
}
