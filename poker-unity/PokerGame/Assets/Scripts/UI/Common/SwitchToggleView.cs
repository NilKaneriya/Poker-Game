using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.UI
{
    /// <summary>
    /// Draws a Toggle as a sliding pill switch: the knob slides right and the track turns
    /// gold when on. Follows the toggle's value however it changes (click, code,
    /// SetIsOnWithoutNotify), so panels can set values without firing their listeners.
    /// </summary>
    [RequireComponent(typeof(Toggle))]
    public class SwitchToggleView : MonoBehaviour
    {
        [SerializeField] private Image track;
        [SerializeField] private RectTransform knob;
        [SerializeField] private Color onColor = new Color(0.96f, 0.78f, 0.27f);
        [SerializeField] private Color offColor = new Color(0.23f, 0.29f, 0.4f);
        [SerializeField] private float knobTravel = 18f; // knob x offset from the centre when on/off

        private Toggle toggle;
        private bool? shown;

        private void Awake() => toggle = GetComponent<Toggle>();

        private void OnEnable() => Show(toggle.isOn, animate: false);

        private void OnDisable() => DOTween.Kill(this);

        private void LateUpdate()
        {
            if (shown != toggle.isOn) Show(toggle.isOn, animate: true);
        }

        private void Show(bool on, bool animate)
        {
            shown = on;
            float x = on ? knobTravel : -knobTravel;
            Color color = on ? onColor : offColor;
            DOTween.Kill(this);
            if (!animate || !Application.isPlaying)
            {
                knob.anchoredPosition = new Vector2(x, knob.anchoredPosition.y);
                track.color = color;
                return;
            }
            knob.DOAnchorPosX(x, 0.18f).SetEase(Ease.OutBack).SetUpdate(true).SetId(this);
            track.DOColor(color, 0.18f).SetUpdate(true).SetId(this);
        }
    }
}
