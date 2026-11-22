using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Poker.UI
{
    /// <summary>A gold ring fades in around a text field while the player is typing in it.</summary>
    [RequireComponent(typeof(TMP_InputField))]
    public class InputFocusHighlight : MonoBehaviour
    {
        [SerializeField] private CanvasGroup ring;

        private void Awake()
        {
            var input = GetComponent<TMP_InputField>();
            input.onSelect.AddListener(_ => Fade(1f));
            input.onDeselect.AddListener(_ => Fade(0f));
            ring.alpha = 0f;
        }

        private void OnDisable()
        {
            DOTween.Kill(this);
            ring.alpha = 0f;
        }

        private void Fade(float to)
        {
            DOTween.Kill(this);
            ring.DOFade(to, 0.15f).SetUpdate(true).SetId(this);
        }
    }
}
