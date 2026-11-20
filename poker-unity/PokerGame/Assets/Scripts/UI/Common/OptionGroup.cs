using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.UI
{
    /// <summary>
    /// A row of buttons where exactly one is selected (like radio buttons),
    /// e.g. "Play with bots / Play with friends" or the table size 2..6.
    /// </summary>
    public class OptionGroup : MonoBehaviour
    {
        [SerializeField] private Button[] options = new Button[0];
        [SerializeField] private Color selectedColor = new Color32(246, 199, 68, 255);   // Theme.Gold
        [SerializeField] private Color normalColor = new Color32(38, 53, 90, 255);       // Theme.PanelLight
        [SerializeField] private Color selectedTextColor = new Color32(11, 18, 32, 255); // Theme.Dark
        [SerializeField] private Color normalTextColor = new Color32(243, 246, 251, 255); // Theme.Text

        public event Action<int> Changed;
        public int Selected { get; private set; } = -1;
        public int Count => options.Length;

        private void Awake()
        {
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                options[i].onClick.AddListener(() => Select(index));
            }
        }

        public void Select(int index, bool notify = true)
        {
            index = Mathf.Clamp(index, 0, options.Length - 1);
            bool changed = index != Selected;
            Selected = index;

            for (int i = 0; i < options.Length; i++)
            {
                bool on = i == index;
                options[i].image.color = on ? selectedColor : normalColor;
                var label = options[i].GetComponentInChildren<TMP_Text>();
                if (label != null) label.color = on ? selectedTextColor : normalTextColor;
            }

            if (changed && notify)
            {
                UIAnim.Punch(options[index].transform, 0.08f);
                Changed?.Invoke(index);
            }
        }
    }
}
