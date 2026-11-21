using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Poker.Audio;
using UnityEditor;
using UnityEngine;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;
using Random = System.Random;

namespace Poker.EditorTools.Audio
{
    /// <summary>
    /// Tools > Poker > Generate Placeholder Sounds: synthesizes every sound in SoundId
    /// (chips, cards, chimes, two music loops) as WAV files in Assets/Audio/Generated and
    /// fills the SoundLibrary asset. Entries that already have clips are never touched,
    /// so real recordings dropped into the library stay. Also runs from Build Scenes.
    /// </summary>
    public static class PlaceholderSoundGenerator
    {
        private const string Folder = "Assets/Audio/Generated";
        private const string LibraryPath = "Assets/Resources/SoundLibrary.asset";
        private const int EffectRate = 44100;
        private const int MusicRate = 22050;

        private delegate float[] Recipe(Random rng);

        /// <summary>Each sound: how many variations, and how to make one.</summary>
        private static readonly Dictionary<SoundId, (int variants, Recipe make)> Recipes = new Dictionary<SoundId, (int, Recipe)>
        {
            { SoundId.MusicLobby, (1, _ => LobbyMusic()) },
            { SoundId.MusicTable, (1, _ => TableMusic()) },
            { SoundId.CardDeal, (3, CardDeal) },
            { SoundId.CardFlip, (2, CardFlip) },
            { SoundId.ChipBet, (3, rng => Chips(rng, count: 3, spread: 0.12f, length: 0.35f)) },
            { SoundId.ChipRaise, (2, rng => Chips(rng, count: 6, spread: 0.25f, length: 0.5f)) },
            { SoundId.ChipAllIn, (1, AllIn) },
            { SoundId.ChipCollect, (1, rng => ChipSlide(rng, count: 7, length: 0.6f)) },
            { SoundId.ChipsWin, (1, rng => ChipSlide(rng, count: 16, length: 1.0f)) },
            { SoundId.Check, (1, Check) },
            { SoundId.Fold, (1, Fold) },
            { SoundId.YourTurn, (1, _ => YourTurn()) },
            { SoundId.TimerTick, (1, _ => TimerTick()) },
            { SoundId.TimerUrgent, (1, _ => TimerUrgent()) },
            { SoundId.Win, (1, _ => Win()) },
            { SoundId.Lose, (1, _ => Lose()) },
            { SoundId.PlayerJoined, (1, _ => PlayerJoined()) },
            { SoundId.GameStart, (1, _ => GameStart()) },
            { SoundId.ButtonClick, (1, ButtonClick) },
            { SoundId.Error, (1, _ => Error()) },
        };

        [MenuItem("Tools/Poker/Generate Placeholder Sounds")]
        public static void GenerateFromMenu()
        {
            Generate();
            Debug.Log($"Placeholder sounds ready in {Folder}; SoundLibrary filled at {LibraryPath}.");
        }

        /// <summary>Write missing WAVs, import them and fill empty SoundLibrary entries.</summary>
        public static void Generate()
        {
            Directory.CreateDirectory(Folder);
            var written = new Dictionary<SoundId, List<string>>();

            foreach (var pair in Recipes)
            {
                var paths = new List<string>();
                for (int v = 0; v < pair.Value.variants; v++)
                {
                    string path = $"{Folder}/{pair.Key}{(pair.Value.variants > 1 ? "_" + (v + 1) : "")}.wav";
                    if (!File.Exists(path))
                    {
                        float[] samples = pair.Value.make(new Random(1000 * (int)pair.Key + v));
                        WavWriter.Write(path, samples, SoundDefaults.IsMusic(pair.Key) ? MusicRate : EffectRate);
                    }
                    paths.Add(path);
                }
                written[pair.Key] = paths;
            }

            AssetDatabase.Refresh();
            foreach (var pair in written)
                foreach (string path in pair.Value) ConfigureImport(path, SoundDefaults.IsMusic(pair.Key));

            FillLibrary(written);
        }

        private static void ConfigureImport(string path, bool music)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) return;
            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            settings.quality = 0.6f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.loadInBackground = music;
            importer.SaveAndReimport();
        }

        private static void FillLibrary(Dictionary<SoundId, List<string>> clips)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
            var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SoundLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            foreach (SoundId id in Enum.GetValues(typeof(SoundId)))
            {
                SoundEntry entry = library.Entries.FirstOrDefault(e => e.id == id);
                if (entry == null)
                {
                    entry = new SoundEntry
                    {
                        id = id,
                        volume = SoundDefaults.Volume.TryGetValue(id, out float vol) ? vol : 0.6f,
                        pitchJitter = SoundDefaults.PitchJitter.TryGetValue(id, out float jitter) ? jitter : 0f,
                    };
                    library.Entries.Add(entry);
                }
                bool hasClips = entry.clips != null && entry.clips.Any(c => c != null);
                if (!hasClips && clips.TryGetValue(id, out List<string> paths))
                    entry.clips = paths.Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(c => c != null).ToArray();
            }

            library.Invalidate();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }

        // =====================================================================
        // Sound recipes
        // =====================================================================

        private static SynthBuffer Effect(float seconds) => new SynthBuffer(seconds, EffectRate);

        private static float Vary(Random rng, float value, float amount) => value * (1f + ((float)rng.NextDouble() * 2 - 1) * amount);

        /// <summary>One ceramic chip landing: a bright tick plus two short resonances.</summary>
        private static void ChipClack(SynthBuffer b, float t, float amp, Random rng)
        {
            b.Noise(t, 0.012f, amp * 0.6f, 250, 9000, 5000, 1500, rng);
            b.Tone(t, 0.06f, Vary(rng, 3100, 0.12f), amp * 0.5f, 90, harmonics: new[] { 0.4f });
            b.Tone(t, 0.05f, Vary(rng, 4700, 0.1f), amp * 0.3f, 120);
        }

        private static float[] Chips(Random rng, int count, float spread, float length)
        {
            var b = Effect(length);
            for (int i = 0; i < count; i++)
                ChipClack(b, spread * i / count + (float)rng.NextDouble() * 0.02f, 1f - 0.08f * i, rng);
            return b.Normalized(0.9f);
        }

        /// <summary>Chips sliding across the felt, then clacking into a stack.</summary>
        private static float[] ChipSlide(Random rng, int count, float length)
        {
            var b = Effect(length);
            b.Noise(0f, length * 0.5f, 0.25f, 6, 3000, 1500, 400, rng, attack: 0.04f);
            for (int i = 0; i < count; i++)
                ChipClack(b, length * 0.25f + length * 0.5f * i / count + (float)rng.NextDouble() * 0.02f, 0.9f, rng);
            return b.Normalized(0.9f);
        }

        private static float[] AllIn(Random rng)
        {
            var b = Effect(0.8f);
            b.Tone(0f, 0.35f, 75, 0.7f, 10, freqEnd: 50);                 // the stack hits the table
            b.Noise(0f, 0.3f, 0.3f, 8, 2500, 1200, 300, rng, attack: 0.02f); // pushed across the felt
            for (int i = 0; i < 12; i++) ChipClack(b, 0.05f + 0.4f * i / 12 + (float)rng.NextDouble() * 0.02f, 0.9f, rng);
            return b.Normalized(0.92f);
        }

        private static float[] CardDeal(Random rng)
        {
            var b = Effect(0.2f);
            b.Noise(0f, Vary(rng, 0.13f, 0.1f), 0.8f, 18, 7000, 2500, 600, rng, attack: 0.01f); // swish through the air
            b.Noise(0.11f, 0.015f, 0.3f, 300, 4000, 4000, 300, rng);                         // lands on the felt
            return b.Normalized(0.85f);
        }

        private static float[] CardFlip(Random rng)
        {
            var b = Effect(0.12f);
            b.Noise(0f, 0.05f, 0.7f, 60, 6000, 3000, 800, rng);
            b.Tone(0.035f, 0.04f, Vary(rng, 1500, 0.1f), 0.25f, 120);
            return b.Normalized(0.85f);
        }

        /// <summary>Two knuckle knocks on the table.</summary>
        private static float[] Check(Random rng)
        {
            var b = Effect(0.35f);
            foreach (float t in new[] { 0f, 0.13f })
            {
                b.Tone(t, 0.12f, 180, 0.7f, 35, harmonics: new[] { 0.3f });
                b.Noise(t, 0.03f, 0.4f, 120, 1200, 1200, 100, rng);
            }
            return b.Normalized(0.9f);
        }

        private static float[] Fold(Random rng)
        {
            var b = Effect(0.3f);
            b.Noise(0f, 0.22f, 0.5f, 10, 2500, 900, 300, rng, attack: 0.03f);
            return b.Normalized(0.7f);
        }

        /// <summary>A soft bell: fundamental, a few overtones and a faint shimmer.</summary>
        private static void Bell(SynthBuffer b, float t, float freq, float amp, float length = 0.9f, float decay = 4.5f)
        {
            b.Tone(t, length, freq, amp, decay, harmonics: new[] { 0.15f, 0.25f, 0f, 0.08f });
            b.Tone(t, length * 0.6f, freq * 2.76f, amp * 0.15f, decay * 2f);
        }

        private static float[] YourTurn()
        {
            var b = Effect(1.1f);
            Bell(b, 0f, 880f, 0.5f);       // A5
            Bell(b, 0.13f, 1318.5f, 0.5f); // E6
            return b.Normalized(0.85f);
        }

        private static float[] TimerTick()
        {
            var b = Effect(0.08f);
            b.Tone(0f, 0.05f, 1100, 0.6f, 70);
            return b.Normalized(0.8f);
        }

        private static float[] TimerUrgent()
        {
            var b = Effect(0.12f);
            b.Tone(0f, 0.09f, 1650, 0.6f, 40, harmonics: new[] { 0.3f });
            b.Tone(0f, 0.09f, 825, 0.4f, 40);
            return b.Normalized(0.9f);
        }

        private static float[] Win()
        {
            var b = Effect(1.7f);
            float[] arpeggio = { 523.25f, 659.25f, 783.99f, 1046.5f }; // C5 E5 G5 C6
            for (int i = 0; i < arpeggio.Length; i++)
                Bell(b, i * 0.09f, arpeggio[i], i == arpeggio.Length - 1 ? 0.6f : 0.45f, i == arpeggio.Length - 1 ? 1.4f : 0.6f);
            foreach (float f in arpeggio.Take(3)) b.Tone(0.27f, 1.3f, f, 0.12f, 1.5f, harmonics: new[] { 0.2f }, attack: 0.05f);
            return b.Normalized(0.85f);
        }

        private static float[] Lose()
        {
            var b = Effect(1.2f);
            b.Tone(0f, 0.5f, 392f, 0.4f, 3, harmonics: new[] { 0.2f }, attack: 0.02f, vibratoHz: 5, vibratoDepth: 0.004f);
            b.Tone(0.28f, 0.9f, 311.13f, 0.4f, 2.5f, harmonics: new[] { 0.2f }, attack: 0.02f, vibratoHz: 5, vibratoDepth: 0.004f);
            return b.Normalized(0.7f);
        }

        private static float[] PlayerJoined()
        {
            var b = Effect(0.6f);
            b.Tone(0f, 0.12f, 600, 0.35f, 10, freqEnd: 1100);
            Bell(b, 0.08f, 1318.5f, 0.3f, 0.5f);
            return b.Normalized(0.8f);
        }

        private static float[] GameStart()
        {
            var b = Effect(1.6f);
            float[] notes = { 523.25f, 659.25f, 783.99f };
            for (int i = 0; i < notes.Length; i++) Bell(b, i * 0.12f, notes[i], 0.45f, 0.5f);
            foreach (float f in notes.Append(1046.5f)) b.Tone(0.38f, 1.2f, f, 0.25f, 2f, harmonics: new[] { 0.25f, 0.1f }, attack: 0.01f);
            return b.Normalized(0.85f);
        }

        private static float[] ButtonClick(Random rng)
        {
            var b = Effect(0.04f);
            b.Tone(0f, 0.025f, 2300, 0.5f, 200);
            b.Noise(0f, 0.008f, 0.3f, 400, 8000, 8000, 2000, rng);
            return b.Normalized(0.7f);
        }

        private static float[] Error()
        {
            var b = Effect(0.3f);
            float[] buzz = { 0f, 0.33f, 0f, 0.2f };
            b.Tone(0f, 0.09f, 160, 0.5f, 8, harmonics: buzz);
            b.Tone(0.12f, 0.12f, 140, 0.5f, 8, harmonics: buzz);
            return b.Normalized(0.75f);
        }

        // =====================================================================
        // Music: a soft electric-piano loop with bass and brushes
        // =====================================================================

        /// <summary>(bass root, chord notes) as MIDI note numbers.</summary>
        private static readonly (int root, int[] notes)[] LobbyChords =
        {
            (48, new[] { 60, 64, 67, 71 }), // Cmaj7
            (45, new[] { 57, 64, 67, 72 }), // Am7
            (50, new[] { 57, 62, 65, 72 }), // Dm7
            (43, new[] { 55, 62, 65, 71 }), // G7
        };

        private static readonly (int root, int[] notes)[] TableChords =
        {
            (45, new[] { 57, 60, 64, 71 }), // Am9
            (41, new[] { 57, 60, 64, 65 }), // Fmaj7
            (38, new[] { 57, 60, 62, 65 }), // Dm7
            (40, new[] { 56, 59, 62, 64 }), // E7
        };

        private static float[] LobbyMusic() => Music(LobbyChords, bpm: 76, seed: 7);
        private static float[] TableMusic() => Music(TableChords, bpm: 88, seed: 11);

        private static float[] Music((int root, int[] notes)[] chords, float bpm, int seed)
        {
            const int barsPerLoop = 8;
            float beat = 60f / bpm;
            float bar = beat * 4;
            var rng = new Random(seed);
            var b = new SynthBuffer(bar * barsPerLoop, MusicRate, loop: true);

            for (int i = 0; i < barsPerLoop; i++)
            {
                var (root, notes) = chords[i % chords.Length];
                float t0 = i * bar;

                // Electric piano: chord on beat 1, softer push on the "and" of 2.
                for (int n = 0; n < notes.Length; n++)
                {
                    float f = Midi(notes[n]);
                    b.Tone(t0 + n * 0.012f, bar * 0.9f, f, 0.16f, 0.9f, harmonics: new[] { 0.35f, 0.08f },
                        attack: 0.01f, tremoloHz: 4.5f, tremoloDepth: 0.2f);
                    b.Tone(t0 + beat * 2.5f + n * 0.01f, beat * 1.5f, f, 0.07f, 1.4f, harmonics: new[] { 0.3f }, attack: 0.01f);
                }

                // Bass: root on 1, fifth on 3.
                b.Tone(t0, beat * 2f, Midi(root), 0.38f, 1.5f, harmonics: new[] { 0.25f }, attack: 0.01f);
                b.Tone(t0 + beat * 2f, beat * 2f, Midi(root + 7), 0.3f, 1.5f, harmonics: new[] { 0.25f }, attack: 0.01f);

                // Brushes: swish on 2 and 4, quiet shaker on every eighth.
                for (int e = 0; e < 8; e++)
                {
                    float t = t0 + e * beat / 2f;
                    bool backbeat = e == 2 || e == 6;
                    b.Noise(t, backbeat ? 0.14f : 0.05f, backbeat ? 0.05f : 0.014f, backbeat ? 22 : 60, 6000, 4000, 2000, rng);
                }

                // A few sparse melody notes from the chord, an octave up.
                for (int m = 0; m < 2; m++)
                {
                    if (rng.NextDouble() < 0.35) continue;
                    float t = t0 + beat * (rng.Next(0, 4) + (rng.NextDouble() < 0.5 ? 0.5f : 0f));
                    b.Tone(t, beat * 1.5f, Midi(notes[rng.Next(notes.Length)] + 12), 0.07f, 2.2f,
                        harmonics: new[] { 0.15f, 0.2f }, attack: 0.005f);
                }
            }
            return b.Normalized(0.8f);
        }

        private static float Midi(int note) => 440f * (float)Math.Pow(2, (note - 69) / 12.0);
    }
}
