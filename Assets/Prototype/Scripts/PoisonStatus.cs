using UnityEngine;

namespace MMO.Prototype
{
    public sealed class PoisonStatus : MonoBehaviour
    {
        private const int TotalPulses = 4;
        private const int PulseDamage = 6;
        private PuppetMotor player;
        private PracticeAlly ally;
        private float nextPulse;

        public int PulsesLeft { get; private set; }
        private bool Alive => ally != null ? ally.IsAlive : player != null && player.Health > 0;

        private void Awake()
        {
            player = GetComponent<PuppetMotor>();
            ally = GetComponent<PracticeAlly>();
        }

        internal void Apply()
        {
            if (!isActiveAndEnabled || !Alive) return;
            PulsesLeft = TotalPulses;
            nextPulse = Time.time + 1f;
        }

        internal bool Clear()
        {
            bool wasPoisoned = PulsesLeft > 0;
            PulsesLeft = 0;
            return wasPoisoned;
        }

        private void Update()
        {
            while (PulsesLeft > 0 && Time.time >= nextPulse)
            {
                if (!Alive) { Clear(); return; }
                PulsesLeft--;
                nextPulse += 1f;
                if (ally != null) ally.TakePoisonDamage(PulseDamage);
                else player.TakePoisonDamage(PulseDamage);
            }
        }

        private void OnDisable() { Clear(); }
    }
}
