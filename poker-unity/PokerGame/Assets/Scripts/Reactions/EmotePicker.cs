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
    /// Pop-up to send a reaction: pick who it is for ("Everyone" or one player), then tap an
    /// emote. Can be opened from anywhere (a button, a click on a player, the chat window).
    /// </summary>
    public class EmotePicker : MonoBehaviour
    {
        [SerializeField] private EmoteSet emotes;

        [Header("Window")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform card;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backdropButton;
        [SerializeField] private TMP_Text titleText;

        [Header("Who it is for")]
        [SerializeField] private RectTransform targetRow;
        [Tooltip("Hidden chip template: Button + Image + child text.")]
        [SerializeField] private Button targetTemplate;
        [SerializeField] private Color chipColor = new Color(1f, 1f, 1f, 0.08f);
        [SerializeField] private Color chipSelectedColor = new Color(0.96f, 0.78f, 0.27f, 1f);
        [SerializeField] private Color chipTextColor = Color.white;
        [SerializeField] private Color chipSelectedTextColor = new Color(0.07f, 0.09f, 0.14f, 1f);

        [Header("Emotes")]
        [SerializeField] private RectTransform emoteGrid;
        [Tooltip("Hidden button template with an 'Icon' Image child.")]
        [SerializeField] private Button emoteTemplate;

        public bool IsOpen { get; private set; }

        private readonly List<(string id, Button chip)> chips = new List<(string, Button)>();
        private ReactionClient client;
        private IReactionScene scene;
        private string selectedId; // null = everyone
        private Coroutine anim;

        private void Awake()
        {
            targetTemplate.gameObject.SetActive(false);
            emoteTemplate.gameObject.SetActive(false);
            closeButton.onClick.AddListener(Close);
            if (backdropButton != null) backdropButton.onClick.AddListener(Close);
            BuildEmoteButtons();
            SetVisible(false);
        }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        public void Bind(ReactionClient reactionClient, IReactionScene reactionScene)
        {
            client = reactionClient;
            scene = reactionScene;
        }

        /// <param name="targetId">Pre-select this player (e.g. their seat was clicked); null = everyone.</param>
        public void Open(string targetId = null)
        {
            if (client == null) return;
            BuildTargetChips();
            Select(HasTarget(targetId) ? targetId : null);
            IsOpen = true;
            SetVisible(true);
            Animate(0f, 1f, 0.85f, 1f);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            group.interactable = group.blocksRaycasts = false;
            Animate(group.alpha, 0f, 1f, 0.95f);
        }

        // ---------- Content ----------

        private void BuildEmoteButtons()
        {
            if (emotes == null) return;
            for (int i = 0; i < emotes.Count; i++)
            {
                var entry = emotes[i];
                var button = Instantiate(emoteTemplate, emoteGrid);
                button.name = "Emote_" + entry.id;
                button.transform.Find("Icon").GetComponent<Image>().sprite = entry.sprite;
                button.gameObject.SetActive(true);
                string id = entry.id;
                button.onClick.AddListener(() => SendEmote(id));
            }
        }

        private void BuildTargetChips()
        {
            foreach (var (_, chip) in chips) Destroy(chip.gameObject);
            chips.Clear();
            AddChip(null, Texts.Everyone);
            if (scene?.Targets == null) return;
            foreach (ReactionTarget target in scene.Targets)
                if (!string.IsNullOrEmpty(target.id) && target.id != client.LocalUserId) AddChip(target.id, target.name);
        }

        private void AddChip(string id, string label)
        {
            var chip = Instantiate(targetTemplate, targetRow);
            chip.name = "Target_" + (id ?? "everyone");
            chip.GetComponentInChildren<TMP_Text>().text = label;
            chip.gameObject.SetActive(true);
            chip.onClick.AddListener(() => Select(id));
            chips.Add((id, chip));
        }

        private bool HasTarget(string id) => !string.IsNullOrEmpty(id) && chips.Exists(c => c.id == id);

        private void Select(string id)
        {
            selectedId = id;
            string name = Texts.Everyone;
            foreach (var (chipId, chip) in chips)
            {
                bool on = chipId == id;
                chip.targetGraphic.color = on ? chipSelectedColor : chipColor;
                chip.GetComponentInChildren<TMP_Text>().color = on ? chipSelectedTextColor : chipTextColor;
                if (on) name = chip.GetComponentInChildren<TMP_Text>().text;
            }
            titleText.text = id == null ? Texts.Title : string.Format(Texts.ToPlayer, name);
        }

        private void SendEmote(string emoteId)
        {
            client?.Send(emoteId, selectedId);
            Close();
        }

        // ---------- Show / hide ----------

        private void SetVisible(bool visible)
        {
            group.alpha = visible ? group.alpha : 0f;
            group.interactable = group.blocksRaycasts = visible;
        }

        private void Animate(float fromAlpha, float toAlpha, float fromScale, float toScale)
        {
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(AnimateRoutine(fromAlpha, toAlpha, fromScale, toScale));
        }

        private IEnumerator AnimateRoutine(float fromAlpha, float toAlpha, float fromScale, float toScale)
        {
            const float seconds = 0.18f;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / seconds)
            {
                float e = 1f - (1f - t) * (1f - t);
                group.alpha = Mathf.Lerp(fromAlpha, toAlpha, e);
                card.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, e);
                yield return null;
            }
            group.alpha = toAlpha;
            card.localScale = Vector3.one * toScale;
            anim = null;
        }
    }
}
