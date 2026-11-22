using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Poker.UI
{
    /// <summary>
    /// Makes a button feel physical: grows a little under the mouse, squeezes when pressed,
    /// springs back on release. Does nothing while the button is disabled.
    /// Added to every button by UIFactory.MakeButton.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public class ButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private float hoverScale = 1.04f;
        [SerializeField] private float pressScale = 0.95f;

        private Selectable selectable;
        private bool hovered;

        private void Awake() => selectable = GetComponent<Selectable>();

        private void OnDisable()
        {
            hovered = false;
            DOTween.Kill(this);
            transform.localScale = Vector3.one;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            ScaleTo(hoverScale, 0.15f, Ease.OutQuad);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            ScaleTo(1f, 0.15f, Ease.OutQuad);
        }

        public void OnPointerDown(PointerEventData eventData) => ScaleTo(pressScale, 0.08f, Ease.OutQuad);

        public void OnPointerUp(PointerEventData eventData) => ScaleTo(hovered ? hoverScale : 1f, 0.25f, Ease.OutBack);

        private void ScaleTo(float scale, float duration, Ease ease)
        {
            if (!Application.isPlaying || !selectable.IsInteractable()) scale = 1f;
            DOTween.Kill(this);
            transform.DOScale(scale, duration).SetEase(ease).SetUpdate(true).SetId(this);
        }
    }
}
