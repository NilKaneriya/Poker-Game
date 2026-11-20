using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Poker.UI
{
    /// <summary>Short message that fades in and out, e.g. "Code copied" or a server error.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ToastView : MonoBehaviour
    {
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private float visibleSeconds = 2.5f;

        private CanvasGroup group;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }

        public void Show(string message)
        {
            messageText.text = message;
            group.DOKill();
            DOTween.Sequence()
                .Append(group.DOFade(1f, 0.15f))
                .AppendInterval(visibleSeconds)
                .Append(group.DOFade(0f, 0.3f))
                .SetTarget(group);
            UIAnim.Punch(transform, 0.08f);
        }

        private void OnDestroy() => group.DOKill();
    }
}
