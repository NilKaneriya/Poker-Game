using System;
using UnityEngine;

namespace Poker.Reactions
{
    /// <summary>The art for each emote id. One asset, shared by the layer and the picker.</summary>
    [CreateAssetMenu(menuName = "Reactions/Emote Set", fileName = "EmoteSet")]
    public class EmoteSet : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string id;
            public Sprite sprite;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public int Count => entries.Length;
        public Entry this[int index] => entries[index];

        public Sprite Get(string id)
        {
            foreach (var entry in entries) if (entry.id == id) return entry.sprite;
            return null;
        }

        /// <summary>Used by the editor tool that builds this asset.</summary>
        public void SetEntries(Entry[] value) => entries = value ?? Array.Empty<Entry>();
    }
}
