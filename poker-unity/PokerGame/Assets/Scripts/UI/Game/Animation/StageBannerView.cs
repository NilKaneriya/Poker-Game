using DG.Tweening;
using TMPro;
using UnityEngine;
using static Poker.Core.Constants;

namespace Poker.UI.Game
{
    /// <summary>Short announcement when a street starts: PRE-FLOP, THE FLOP, THE TURN, THE RIVER, SHOWDOWN.</summary>
    public class StageBannerView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text label;

        private void Awake() => group.alpha = 0f;

        public void Show(string stage)
        {
            if (!Application.isPlaying || !PokerStages.Announcements.TryGetValue(stage ?? "", out string text)) return;

            label.text = text;
            group.DOKill();
            transform.DOKill();
            group.alpha = 0f;
            transform.localScale = Vector3.one * 0.7f;

            DOTween.Sequence()
                .Append(group.DOFade(1f, AnimationTimings.BannerFadeIn))
                .Join(transform.DOScale(1f, AnimationTimings.BannerPop).SetEase(Ease.OutBack))
                .AppendInterval(AnimationTimings.BannerHold)
                .Append(group.DOFade(0f, AnimationTimings.BannerFadeOut))
                .SetTarget(group);
        }

        private void OnDestroy()
        {
            group.DOKill();
            transform.DOKill();
        }
    }
}
