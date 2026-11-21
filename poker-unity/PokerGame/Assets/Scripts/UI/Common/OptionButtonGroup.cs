using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Poker.Core.Constants;

namespace Poker.UI
{
    /// <summary>
    /// A row of buttons where exactly one is selected (like radio buttons),
    /// e.g. "Play vs bots / Play with friends" or the table size 2..6.
    /// </summary>
    public class OptionButtonGroup : MonoBehaviour
    {
        [SerializeField] private Button[] options = new Button[0];
        [SerializeField] private Color selectedColor = UIColors.Gold;
        [SerializeField] private Color normalColor = UIColors.PanelLight;
        [SerializeField] private Color selectedTextColor = UIColors.Dark;
        [SerializeField] private Color normalTextColor = UIColors.Text;

        public event Action<int> Changed;
        public int Selected { get; private set; } = -1;

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
