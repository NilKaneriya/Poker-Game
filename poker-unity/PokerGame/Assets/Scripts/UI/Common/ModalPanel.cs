using UnityEngine;
using UnityEngine.UI;

namespace Poker.UI
{
    /// <summary>
    /// Shared open/close behaviour for pop-up panels (settings, profile): fade + pop,
    /// close with the close button, the dimmed backdrop or Escape.
    /// </summary>
    public abstract class ModalPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform card;
        [SerializeField] private Button openButton;     // optional: the button that opens this panel
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backdropButton; // click outside the card

        public bool IsOpen { get; private set; }

        protected virtual void Start()
        {
            if (openButton != null) openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            if (backdropButton != null) backdropButton.onClick.AddListener(Close);
            SetVisible(false);
        }

        protected virtual void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        public void Open()
        {
            if (IsOpen) return;
            SetVisible(true);
            UIAnim.PopIn(card, 0.25f);
            OnOpened();
        }

        public void Close()
        {
            if (!IsOpen) return;
            SetVisible(false);
            OnClosed();
        }

        protected virtual void OnOpened() { }
        protected virtual void OnClosed() { }

        private void SetVisible(bool visible)
        {
            IsOpen = visible;
            group.alpha = visible ? 1f : 0f;
            group.interactable = group.blocksRaycasts = visible;
        }
    }
}
