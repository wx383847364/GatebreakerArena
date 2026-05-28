using System;
using System.Globalization;
using App.Shared.Contracts;
using TMPro;
using UnityEngine.UI;

namespace App.HotUpdate.GatebreakerArena.UI
{
    public sealed class GatebreakerArenaSceneBindingService
    {
        private Button _skillButton;
        private TMP_Text _ballCountText;
        private Action _serveRequested;
        private IAppLogger _logger;
        private string _lastBallCountText;

        public bool IsBound { get; private set; }
        public bool HasSkillButtonBinding => _skillButton != null;
        public bool HasBallCountTextBinding => _ballCountText != null;

        public void Bind(
            IGatebreakerArenaSceneUiBinding binding,
            Action serveRequested,
            IAppLogger logger)
        {
            Clear();
            _serveRequested = serveRequested;
            _logger = logger;
            IsBound = true;

            if (binding == null)
            {
                _logger?.LogWarning("GatebreakerArenaSceneBindingService: scene UI binding is missing.");
                return;
            }

            _skillButton = binding.SkillButtonObject as Button;
            _ballCountText = binding.BallCountTextObject as TMP_Text;

            if (_skillButton == null)
            {
                _logger?.LogWarning("GatebreakerArenaSceneBindingService: SkillButtonObject is not a UnityEngine.UI.Button.");
            }
            else
            {
                _skillButton.onClick.AddListener(HandleSkillButtonClicked);
            }

            if (_ballCountText == null)
            {
                _logger?.LogWarning("GatebreakerArenaSceneBindingService: BallCountTextObject is not a TMP_Text.");
            }
        }

        public void MarkBound()
        {
            IsBound = true;
        }

        public void UpdateBallCount(GatebreakerHudSnapshot snapshot)
        {
            if (_ballCountText == null || snapshot == null)
            {
                return;
            }

            string countText = snapshot.CurrentServeAmmo.ToString(CultureInfo.InvariantCulture);
            if (countText == _lastBallCountText)
            {
                return;
            }

            _ballCountText.text = countText;
            _lastBallCountText = countText;
        }

        public void Clear()
        {
            if (_skillButton != null)
            {
                _skillButton.onClick.RemoveListener(HandleSkillButtonClicked);
            }

            _skillButton = null;
            _ballCountText = null;
            _serveRequested = null;
            _logger = null;
            _lastBallCountText = null;
            IsBound = false;
        }

        private void HandleSkillButtonClicked()
        {
            _serveRequested?.Invoke();
        }
    }
}
