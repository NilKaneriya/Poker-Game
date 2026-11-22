using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Poker.Reactions.ReactionConstants;
using static Poker.Reactions.ReactionModels;

namespace Poker.Reactions
{
    /// <summary>
    /// Full-screen overlay that plays reactions: an emote pops up over the sender, or is
    /// thrown in an arc from the sender to the player it was aimed at. Where people are on
    /// screen comes from the host (IReactionScene), so this works on any screen.
    /// </summary>
    public class ReactionLayer : MonoBehaviour
    {
        [SerializeField] private EmoteSet emotes;
        [Tooltip("Hidden template: CanvasGroup root with an 'Icon' Image and a 'Caption' text.")]
        [SerializeField] private RectTransform itemTemplate;
        [Tooltip("Where reactions from people not on screen appear (their name is shown with it).")]
        [SerializeField] private RectTransform fallbackAnchor;
        [SerializeField] private float popOffsetY = 95f;

        /// <summary>A reaction started playing (e.g. for the host to play a sound).</summary>
        public event Action<Reaction> Played;

        private readonly Queue<GameObject> live = new Queue<GameObject>();
        private RectTransform rect;
        private ReactionClient client;
        private IReactionScene scene;

        private void Awake()
        {
            rect = (RectTransform)transform;
            itemTemplate.gameObject.SetActive(false);
        }

        public void Bind(ReactionClient reactionClient, IReactionScene reactionScene)
        {
            Unbind();
            client = reactionClient;
            scene = reactionScene;
            if (client != null) client.Received += Play;
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (client != null) client.Received -= Play;
            client = null;
        }

        /// <summary>Plays a reaction (also handy for previews and tests).</summary>
        public void Play(Reaction reaction)
        {
            Sprite sprite = emotes != null ? emotes.Get(reaction?.emoteId) : null;
            if (sprite == null || !isActiveAndEnabled) return;

            bool senderShown = TryLocal(reaction.fromId, out Vector2 from);
            if (!senderShown) from = fallbackAnchor != null ? Local(fallbackAnchor) : Vector2.zero;

            var item = Instantiate(itemTemplate, rect);
            item.gameObject.SetActive(true);
            item.Find("Icon").GetComponent<Image>().sprite = sprite;
            var caption = item.Find("Caption")?.GetComponent<TMP_Text>();
            if (caption != null)
            {
                caption.gameObject.SetActive(!senderShown);
                caption.text = reaction.fromName;
            }

            live.Enqueue(item.gameObject);
            while (live.Count > Timings.MaxOnScreen) Destroy(live.Dequeue());

            Vector2 to = default;
            bool thrown = reaction.HasTarget && TryLocal(reaction.targetId, out to);
            StartCoroutine(thrown ? Throw(item, from, to) : Pop(item, from + Vector2.up * popOffsetY));
            Played?.Invoke(reaction);
        }

        // ---------- Animations (unscaled time: they keep playing during pauses) ----------

        private IEnumerator Pop(RectTransform item, Vector2 at)
        {
            item.anchoredPosition = at;
            yield return Scale(item, 0f, 1f, Timings.PopSeconds, EaseOutBack);
            yield return HoldThenFade(item);
        }

        private IEnumerator Throw(RectTransform item, Vector2 from, Vector2 to)
        {
            item.localScale = Vector3.one * 0.7f;
            float spin = UnityEngine.Random.value < 0.5f ? -360f : 360f;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Timings.FlySeconds)
            {
                if (item == null) yield break;
                float e = EaseInOut(t);
                item.anchoredPosition = Vector2.Lerp(from, to, e) + Vector2.up * (Timings.FlyArcHeight * 4f * e * (1f - e));
                item.localRotation = Quaternion.Euler(0, 0, spin * e);
                yield return null;
            }
            if (item == null) yield break;
            item.anchoredPosition = to;
            item.localRotation = Quaternion.identity;
            yield return Scale(item, 1.45f, 1f, Timings.PopSeconds, EaseOutBack); // lands with a bump
            yield return HoldThenFade(item);
        }

        private IEnumerator HoldThenFade(RectTransform item)
        {
            for (float t = 0f; t < Timings.HoldSeconds; t += Time.unscaledDeltaTime)
            {
                if (item == null) yield break;
                yield return null;
            }

            var group = item.GetComponent<CanvasGroup>();
            Vector2 start = item.anchoredPosition;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Timings.FadeSeconds)
            {
                if (item == null) yield break;
                if (group != null) group.alpha = 1f - t;
                item.anchoredPosition = start + Vector2.up * (Timings.DriftUp * t);
                yield return null;
            }
            if (item != null) Destroy(item.gameObject);
        }

        private static IEnumerator Scale(RectTransform item, float from, float to, float seconds, Func<float, float> ease)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / seconds)
            {
                if (item == null) yield break;
                item.localScale = Vector3.one * Mathf.LerpUnclamped(from, to, ease(t));
                yield return null;
            }
            if (item != null) item.localScale = Vector3.one * to;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            t -= 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        private static float EaseInOut(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

        // ---------- Positions ----------

        private bool TryLocal(string userId, out Vector2 local)
        {
            local = Vector2.zero;
            if (scene == null || string.IsNullOrEmpty(userId) || !scene.TryGetAnchor(userId, out RectTransform anchor) || anchor == null)
                return false;
            if (!anchor.gameObject.activeInHierarchy) return false;
            local = Local(anchor);
            return true;
        }

        /// <summary>The anchor's centre in this layer's coordinates (items are centred in the layer).</summary>
        private Vector2 Local(RectTransform anchor)
        {
            Vector3 world = anchor.TransformPoint(anchor.rect.center);
            return rect.InverseTransformPoint(world);
        }
    }
}
