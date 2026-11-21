using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using static Poker.Core.Constants;
using Random = UnityEngine.Random;

namespace Poker.UI.Game
{
    /// <summary>
    /// Flies chip images from one point of the screen to another (the "how" of chip
    /// animations; TableChangeAnimator decides "when"). Chips are pooled copies of a
    /// hidden template, so nothing is allocated per hand.
    /// </summary>
    public class ChipAnimator : MonoBehaviour
    {
        [SerializeField] private RectTransform chipTemplate; // inactive child with an Image

        private readonly Stack<RectTransform> pool = new Stack<RectTransform>();
        private readonly List<RectTransform> flying = new List<RectTransform>();
        private float pixelScale = 1f; // canvas scale, so jitter/jump look the same at any resolution

        private void Awake()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) pixelScale = canvas.scaleFactor;
            chipTemplate.gameObject.SetActive(false);
        }

        /// <summary>How many chips to show for an amount: small bets 2, big ones up to 7.</summary>
        public static int ChipsFor(int amount, int bigBlind) =>
            Mathf.Clamp(1 + amount / Mathf.Max(1, bigBlind), ChipFlights.MinChips, ChipFlights.MaxChips);

        /// <summary>Fly 'count' chips between two world positions; onArrive runs when the last one lands.</summary>
        public void Fly(Vector3 from, Vector3 to, int count, float delay = 0f, Action onArrive = null)
        {
            if (!Application.isPlaying || count <= 0)
            {
                onArrive?.Invoke();
                return;
            }

            for (int i = 0; i < count; i++)
            {
                RectTransform chip = Take();
                chip.position = from + Jitter(AnimationTimings.ChipStartJitter);
                chip.localScale = Vector3.zero;
                chip.GetComponent<Image>().color = UIColors.ChipPalette[Random.Range(0, UIColors.ChipPalette.Length)];

                bool last = i == count - 1;
                DOTween.Sequence()
                    .AppendInterval(delay + i * AnimationTimings.ChipStagger)
                    .Append(chip.DOScale(1f, AnimationTimings.ChipAppear))
                    .Append(chip.DOJump(to + Jitter(AnimationTimings.ChipEndJitter),
                        AnimationTimings.ChipJumpHeight * pixelScale, 1, AnimationTimings.ChipFlight).SetEase(Ease.OutQuad))
                    .AppendCallback(() => { if (last) onArrive?.Invoke(); })
                    .Append(chip.DOScale(0f, AnimationTimings.ChipVanish))
                    .OnKill(() => Return(chip))
                    .SetTarget(chip);
            }
        }

        private Vector3 Jitter(float radius) => (Vector3)(Random.insideUnitCircle * radius * pixelScale);

        private RectTransform Take()
        {
            RectTransform chip = pool.Count > 0 ? pool.Pop() : Instantiate(chipTemplate, transform);
            chip.gameObject.SetActive(true);
            chip.SetAsLastSibling();
            flying.Add(chip);
            return chip;
        }

        private void Return(RectTransform chip)
        {
            if (chip == null || !flying.Remove(chip)) return;
            chip.gameObject.SetActive(false);
            pool.Push(chip);
        }

        private void OnDestroy()
        {
            foreach (RectTransform chip in flying.ToArray())
            {
                if (chip != null) chip.DOKill();
            }
        }
    }
}
