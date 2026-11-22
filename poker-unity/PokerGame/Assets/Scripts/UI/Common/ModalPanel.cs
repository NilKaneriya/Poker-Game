using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.UI
{
    /// <summary>
    /// Shared open/close behaviour for pop-up panels (settings, profile): the backdrop fades
    /// in while the card springs up from slightly smaller; closing fades it away. Closes with
    /// the close button, a click on the dimmed backdrop, or Escape.
    /// </summary>
    public abstract class ModalPanel : MonoBehaviour
    {
        private const float OpenSeconds = 0.28f;
        private const float CloseSeconds = 0.16f;
        private const float StartScale = 0.88f;

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
            IsOpen = false;
            group.alpha = 0f;
            group.interactable = group.blocksRaycasts = false;
        }

        protected virtual void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        protected virtual void OnDestroy() => DOTween.Kill(this);

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            group.interactable = group.blocksRaycasts = true;
            DOTween.Kill(this);
            card.localScale = Vector3.one * StartScale;
            DOTween.Sequence()
                .Append(group.DOFade(1f, OpenSeconds * 0.7f))
                .Join(card.DOScale(1f, OpenSeconds).SetEase(Ease.OutBack))
                .SetUpdate(true).SetId(this);
            OnOpened();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            group.interactable = group.blocksRaycasts = false; // clicks go through at once
            DOTween.Kill(this);
            DOTween.Sequence()
                .Append(group.DOFade(0f, CloseSeconds))
                .Join(card.DOScale(0.95f, CloseSeconds).SetEase(Ease.InQuad))
                .SetUpdate(true).SetId(this);
            OnClosed();
        }

        protected virtual void OnOpened() { }
        protected virtual void OnClosed() { }
    }
}
