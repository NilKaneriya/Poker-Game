using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Poker.Platform
{
    /// <summary>
    /// Copy text and open the device's share sheet.
    ///   WebGL   - browser clipboard and the Web Share API (Assets/Plugins/WebGL/PokerShare.jslib)
    ///   Android - the system "Share via" chooser
    ///   others  - clipboard only
    /// GUIUtility.systemCopyBuffer alone does not reach the browser clipboard in WebGL.
    /// </summary>
    public static class ShareService
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void PokerCopyText(string text);
        [DllImport("__Internal")] private static extern int PokerShareText(string title, string text, string url);
        [DllImport("__Internal")] private static extern void PokerRemoveUrlParam(string key);
#endif

        public static void Copy(string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PokerCopyText(text);
#else
            GUIUtility.systemCopyBuffer = text;
#endif
        }

        /// <summary>
        /// Opens the share sheet. Returns false when there is none on this device;
        /// the message and link are copied to the clipboard instead.
        /// </summary>
        public static bool Share(string title, string message, string url)
        {
            string full = string.IsNullOrEmpty(url) ? message : $"{message}\n{url}";
#if UNITY_WEBGL && !UNITY_EDITOR
            if (PokerShareText(title, message, url ?? "") == 1) return true;
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                ShareOnAndroid(title, full);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Share] Android share failed, copying instead: {e.Message}");
            }
#endif
            Copy(full);
            return false;
        }

        /// <summary>WebGL: drop a query parameter from the page address without reloading.</summary>
        public static void RemoveUrlParam(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PokerRemoveUrlParam(key);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void ShareOnAndroid(string title, string text)
        {
            using var intentClass = new AndroidJavaClass("android.content.Intent");
            using var intent = new AndroidJavaObject("android.content.Intent");
            intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND")).Dispose();
            intent.Call<AndroidJavaObject>("setType", "text/plain").Dispose();
            intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text).Dispose();

            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, title);
            activity.Call("startActivity", chooser);
        }
#endif
    }
}
