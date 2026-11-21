using System.Collections.Generic;
using UnityEngine;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.Audio
{
    /// <summary>
    /// Which clips play for each SoundId, and how loud. Lives at Resources/SoundLibrary.
    /// Tools > Poker > Generate Placeholder Sounds fills empty entries with synthesized
    /// sounds; drop real recordings onto an entry to replace them.
    /// </summary>
    [CreateAssetMenu(menuName = "Poker/Sound Library", fileName = "SoundLibrary")]
    public class SoundLibrary : ScriptableObject
    {
        [SerializeField] private List<SoundEntry> entries = new List<SoundEntry>();

        private Dictionary<SoundId, SoundEntry> byId;

        public List<SoundEntry> Entries => entries;

        public static SoundLibrary Load()
        {
            var library = Resources.Load<SoundLibrary>(AudioConfig.LibraryResource);
            if (library == null)
            {
                Debug.LogWarning("Resources/SoundLibrary not found: the game runs without sound. Run Tools > Poker > Build Scenes.");
                library = CreateInstance<SoundLibrary>();
            }
            return library;
        }

        /// <summary>The entry for a sound, or null if it has no clips.</summary>
        public SoundEntry Get(SoundId id)
        {
            if (byId == null)
            {
                byId = new Dictionary<SoundId, SoundEntry>();
                foreach (SoundEntry e in entries) byId[e.id] = e;
            }
            return byId.TryGetValue(id, out SoundEntry entry) && entry.clips != null && entry.clips.Length > 0 ? entry : null;
        }

        /// <summary>Editor: forget the lookup after entries change.</summary>
        public void Invalidate() => byId = null;
    }
}
