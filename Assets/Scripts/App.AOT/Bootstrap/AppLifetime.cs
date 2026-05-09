using UnityEngine;

namespace App.AOT.Bootstrap
{
    /// <summary>
    /// 应用生命周期管理：处理pause/resume、网络变化等
    /// </summary>
    public class AppLifetime : MonoBehaviour
    {
        private void OnApplicationPause(bool pauseStatus)
        {
            // 应用进入后台/前台
            if (pauseStatus)
            {
                OnApplicationPaused();
            }
            else
            {
                OnApplicationResumed();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                OnApplicationLostFocus();
            }
            else
            {
                OnApplicationGainedFocus();
            }
        }

        private void OnApplicationPaused()
        {
            Debug.Log("AppLifetime: 应用进入后台");
            // 可以在这里保存数据、暂停网络等
        }

        private void OnApplicationResumed()
        {
            Debug.Log("AppLifetime: 应用恢复前台");
            // 可以在这里恢复网络连接、同步时间等
        }

        private void OnApplicationLostFocus()
        {
            Debug.Log("AppLifetime: 应用失去焦点");
        }

        private void OnApplicationGainedFocus()
        {
            Debug.Log("AppLifetime: 应用获得焦点");
        }
    }
}
