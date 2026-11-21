using DG.Tweening;
using Poker.Core;
using TMPro;
using UnityEngine;
using static Poker.Core.Constants;

namespace Poker.UI.Game
{
    /// <summary>
    /// One playing card: face up, face down, or empty slot.
    /// Animates only when what it shows actually changes (deal pop, flip on reveal).
    /// </summary>
    public class CardView : MonoBehaviour
    {
        [SerializeField] private GameObject face;
        [SerializeField] private GameObject back;
        [SerializeField] private TMP_Text rankText;
        [SerializeField] private TMP_Text cornerSuitText;
        [SerializeField] private TMP_Text centerSuitText;
        [SerializeField] private GameObject highlight; // gold outline for winning cards
        [SerializeField] private CanvasGroup canvasGroup;

        [SerializeField] private Color redSuit = new Color(0.86f, 0.15f, 0.2f);
        [SerializeField] private Color blackSuit = new Color(0.1f, 0.12f, 0.16f);

        private enum Shown { Empty, Back, Face }
        private Shown shown = Shown.Empty;
        private string shownCode;

        private Vector3 homePosition; // where the layout put this card; deal animations return here
        private bool homeKnown;

        public void ShowFace(string code)
        {
            if (shown == Shown.Face && shownCode == code) return;

            bool wasBack = shown == Shown.Back;
            shown = Shown.Face;
            shownCode = code;
            gameObject.SetActive(true);

            if (wasBack) { SnapHome(); UIAnim.Flip(transform, () => ApplyFace(code)); } // reveal
            else { ApplyFace(code); UIAnim.PopIn(transform, 0.3f); }                     // freshly dealt
        }

        /// <summary>Deal animation: slide in from 'from' (world position, e.g. the table centre).</summary>
        public void FlyIn(Vector3 from, float delay)
        {
            if (!Application.isPlaying) return;
            RememberHome();
            transform.DOKill();
            transform.localScale = Vector3.zero;
            transform.position = from;
            DOTween.Sequence()
                .AppendInterval(delay)
                .Append(transform.DOScale(0.6f, 0.05f))
                .Append(transform.DOLocalMove(homePosition, AnimationTimings.DealFlight).SetEase(Ease.OutCubic))
                .Join(transform.DOScale(1f, AnimationTimings.DealFlight).SetEase(Ease.OutBack))
                .OnKill(SnapHome) // never leave a card stranded mid-flight
                .SetTarget(transform);
        }

        private void RememberHome()
        {
            if (homeKnown) return;
            homePosition = transform.localPosition;
            homeKnown = true;
        }

        private void SnapHome()
        {
            if (homeKnown) transform.localPosition = homePosition;
        }

        public void ShowBack()
        {
            if (shown == Shown.Back) return;
            shown = Shown.Back;
            shownCode = null;
            gameObject.SetActive(true);
            face.SetActive(false);
            back.SetActive(true);
            UIAnim.PopIn(transform, 0.3f);
        }

        public void Clear()
        {
            shown = Shown.Empty;
            shownCode = null;
            transform.DOKill();
            SnapHome();
            transform.localScale = Vector3.one;
            SetHighlighted(false);
            SetDimmed(false);
            gameObject.SetActive(false);
        }

        public void SetHighlighted(bool on)
        {
            if (highlight != null) highlight.SetActive(on);
        }

        /// <summary>Faded look for folded hands and non-winning board cards.</summary>
        public void SetDimmed(bool on)
        {
            if (canvasGroup != null) canvasGroup.alpha = on ? 0.4f : 1f;
        }

        private void ApplyFace(string code)
        {
            back.SetActive(false);
            face.SetActive(true);
            if (!CardFormat.TryParse(code, out string rank, out string suit, out bool isRed)) return;

            Color color = isRed ? redSuit : blackSuit;
            rankText.text = rank;
            cornerSuitText.text = suit;
            centerSuitText.text = suit;
            rankText.color = cornerSuitText.color = centerSuitText.color = color;
        }
    }
}
