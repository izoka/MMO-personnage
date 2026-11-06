using UnityEngine;

namespace MMO.Prototype
{
    internal sealed class ImpactAudio : MonoBehaviour
    {
        private const int VoiceCount = 8;
        private static readonly float[] PitchVariation = { 1f, 0.96f, 1.035f, 0.98f, 1.05f };
        private static ImpactAudio instance;
        private readonly AudioSource[] voices = new AudioSource[VoiceCount];
        private AudioClip light, heavy, block, parry, arcane;
        private int played;

        internal static void Prepare()
        {
            if (instance == null)
                instance = new GameObject("Sons des impacts").AddComponent<ImpactAudio>();
        }

        private void Awake()
        {
            light = Resources.Load<AudioClip>("ImpactAudio/Light");
            heavy = Resources.Load<AudioClip>("ImpactAudio/Heavy");
            block = Resources.Load<AudioClip>("ImpactAudio/Block");
            parry = Resources.Load<AudioClip>("ImpactAudio/Parry");
            arcane = Resources.Load<AudioClip>("ImpactAudio/Arcane");
            for (int i = 0; i < voices.Length; i++)
            {
                var voice = new GameObject("Impact " + (i + 1)).AddComponent<AudioSource>();
                voice.transform.SetParent(transform, false);
                voice.playOnAwake = false;
                voice.spatialBlend = 1f;
                voice.dopplerLevel = 0f;
                voice.rolloffMode = AudioRolloffMode.Linear;
                voice.minDistance = 3.5f;
                voice.maxDistance = 19f;
                voices[i] = voice;
            }
        }

        internal static void Play(Vector3 point, CombatHit hit, HitResult result)
        {
            if (result == HitResult.Miss || !Application.isPlaying) return;
            Prepare();
            float strength = ImpactRecoil.Strength(hit, result);
            AudioClip clip = result switch
            {
                HitResult.Parried => instance.parry,
                HitResult.Blocked => instance.block,
                HitResult.GuardBroken => instance.heavy,
                _ => hit.arcane ? instance.arcane : strength >= 0.72f ? instance.heavy : instance.light
            };
            if (clip == null) return;

            foreach (AudioSource voice in instance.voices)
            {
                if (voice.isPlaying) continue;
                voice.transform.position = point;
                voice.clip = clip;
                voice.pitch = PitchVariation[instance.played % PitchVariation.Length];
                instance.played = (instance.played + 1) % PitchVariation.Length;
                voice.volume = Mathf.Lerp(0.60f, 0.85f, strength);
                voice.Play();
                return;
            }
        }
    }
}
