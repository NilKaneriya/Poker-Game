using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine.EventSystems;
#endif

namespace Poker.Platform
{
    /// <summary>
    /// Unity WebGL cannot open the phone keyboard for a TMP_InputField. On a touch device this
    /// opens a small HTML input bar (defined in the WebGL page template) when a field is tapped,
    /// and puts the typed text back into the field. Does nothing in the Editor, on desktop
    /// browsers, or in Android builds (those use the normal system keyboard).
    /// </summary>
    public class MobileKeyboardBridge : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int PokerKeyboardIsTouch();
        [DllImport("__Internal")] private static extern void PokerKeyboardOpen(string goName, string text, string label, int secret, int numeric, int maxLength);

        private const string ObjectName = "MobileKeyboardBridge";
        private TMP_InputField _active;
        private GameObject _lastSelected;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (PokerKeyboardIsTouch() == 0) return;
            // Otherwise Unity swallows every key press and the HTML input cannot receive text.
            WebGLInput.captureAllKeyboardInput = false;
            var go = new GameObject(ObjectName);
            DontDestroyOnLoad(go);
            go.AddComponent<MobileKeyboardBridge>();
        }

        private void Update()
        {
            var system = EventSystem.current;
            if (system == null) return;
            GameObject selected = system.currentSelectedGameObject;
            if (selected == _lastSelected) return;
            _lastSelected = selected;
            if (selected == null || _active != null) return;

            var field = selected.GetComponent<TMP_InputField>();
            if (field == null || !field.interactable) return;

            _active = field;
            string label = field.placeholder is TMP_Text ph ? ph.text : "";
            bool secret = field.contentType == TMP_InputField.ContentType.Password;
            bool numeric = field.contentType == TMP_InputField.ContentType.IntegerNumber
                        || field.contentType == TMP_InputField.ContentType.DecimalNumber;
            PokerKeyboardOpen(ObjectName, field.text, label, secret ? 1 : 0, numeric ? 1 : 0, field.characterLimit);
        }

        /// <summary>Called from the page: "1|text" = OK, "0|" = cancelled.</summary>
        public void OnMobileKeyboardResult(string payload)
        {
            TMP_InputField field = _active;
            _active = null;
            if (field != null && payload != null && payload.StartsWith("1|"))
            {
                string typed = payload.Substring(2);
                var kept = new System.Text.StringBuilder();
                foreach (char c in typed)
                {
                    char ch = c;
                    if (field.onValidateInput != null) ch = field.onValidateInput(kept.ToString(), kept.Length, c);
                    if (ch != '\0') kept.Append(ch);
                }
                string text = kept.ToString();
                if (field.characterLimit > 0 && text.Length > field.characterLimit) text = text.Substring(0, field.characterLimit);
                field.text = text;
                field.onEndEdit.Invoke(text);
            }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            _lastSelected = null;
        }
#endif
    }
}
