using App.Shared.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace App.AOT.Bootstrap
{
    /// <summary>
    /// AOT 场景引用桥：只保存 Unity 场景中的 UI 对象引用，不承载玩法或 UI 业务。
    /// </summary>
    public sealed class GatebreakerArenaSceneUiBinding : MonoBehaviour, IGatebreakerArenaSceneUiBinding
    {
        [SerializeField] private Button _skillButton;
        [SerializeField] private TMP_Text _ballCountText;

        public Object SkillButtonObject => _skillButton;
        public Object BallCountTextObject => _ballCountText;
        public bool HasRequiredBindings => _skillButton != null && _ballCountText != null;

        private void Awake()
        {
            GatebreakerArenaSceneUiBindingRegistry.Register(this);
        }

        private void OnDestroy()
        {
            GatebreakerArenaSceneUiBindingRegistry.Clear(this);
        }

#if UNITY_EDITOR
        public void AssignForEditor(Button skillButton, TMP_Text ballCountText)
        {
            _skillButton = skillButton;
            _ballCountText = ballCountText;
        }
#endif
    }
}
