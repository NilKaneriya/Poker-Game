using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Poker.UI
{
    /// <summary>
    /// All UI animations in one place (DOTween). Every method snaps to the final
    /// state when not in Play mode, so views also look right in the Editor.
    /// </summary>
    public static class UIAnim
    {
        private static bool Live => Application.isPlaying;

        /// <summary>Scale up from zero with a small overshoot.</summary>
        public static void PopIn(Transform t, float duration = 0.3f, float delay = 0f)
        {
            t.DOKill();
            if (!Live) { t.localScale = Vector3.one; return; }
            t.localScale = Vector3.zero;
            t.DOScale(1f, duration).SetDelay(delay).SetEase(Ease.OutBack);
        }

        /// <summary>Quick "bump" to draw attention to a value change.</summary>
        public static void Punch(Transform t, float strength = 0.15f)
        {
            if (!Live) return;
            t.DOKill(true);
            t.localScale = Vector3.one;
            t.DOPunchScale(Vector3.one * strength, 0.3f, 6, 0.5f);
        }

        /// <summary>Flip a card: shrink on X, run onHalfway (swap the face), grow back.</summary>
        public static void Flip(Transform t, Action onHalfway, float duration = 0.3f)
        {
            t.DOKill();
            if (!Live) { onHalfway(); t.localScale = Vector3.one; return; }
            DOTween.Sequence()
                .Append(t.DOScaleX(0f, duration / 2).SetEase(Ease.InQuad))
                .AppendCallback(() => onHalfway())
                .Append(t.DOScaleX(1f, duration / 2).SetEase(Ease.OutQuad))
                .SetTarget(t);
        }

        /// <summary>Gentle endless breathing scale, used on the active seat.</summary>
        public static void StartPulse(Transform t, float scale = 1.06f)
        {
            if (!Live) return;
            t.DOKill();
            t.localScale = Vector3.one;
            t.DOScale(scale, 0.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        }

        public static void StopPulse(Transform t)
        {
            t.DOKill();
            t.localScale = Vector3.one;
        }

        public static void FadeIn(CanvasGroup group, float duration = 0.2f)
        {
            group.DOKill();
            group.gameObject.SetActive(true);
            group.interactable = group.blocksRaycasts = true;
            if (!Live) { group.alpha = 1f; return; }
            group.DOFade(1f, duration);
        }

        public static void FadeOut(CanvasGroup group, float duration = 0.2f)
        {
            group.DOKill();
            group.interactable = group.blocksRaycasts = false;
            if (!Live) { group.alpha = 0f; group.gameObject.SetActive(false); return; }
            group.DOFade(0f, duration).OnComplete(() => group.gameObject.SetActive(false));
        }

        /// <summary>Count a number up/down in a text field, e.g. chip stacks.</summary>
        public static void CountTo(TMP_Text text, int from, int to, Func<int, string> format, float duration = 0.4f)
        {
            text.DOKill();
            if (!Live || from == to) { text.text = format(to); return; }
            int value = from;
            DOTween.To(() => value, v => { value = v; text.text = format(v); }, to, duration).SetTarget(text);
        }
    }
}
