using UnityEngine;

namespace MMO.Prototype
{
    [DefaultExecutionOrder(-50)]
    public sealed class ClassSwitcher : MonoBehaviour
    {
        public PuppetMotor warrior, mage, ranger, healer;
        public OrbitCamera orbit;
        public PrototypeHelp help;
        public SparringOpponent opponent;

        private float blockedUntil;
        public PuppetMotor ActivePlayer { get; private set; }
        public PuppetMotor NextPlayer
        {
            get
            {
                if (ActivePlayer == warrior) return mage;
                if (ActivePlayer == mage && ranger != null) return ranger;
                if (ActivePlayer == ranger && healer != null) return healer;
                return warrior;
            }
        }
        public string NextClassLabel
        {
            get
            {
                var next = NextPlayer;
                if (next == mage) return "le mage";
                if (next == ranger) return "le rôdeur";
                if (next == healer) return "le soigneur";
                return "le guerrier";
            }
        }
        public bool ShowBlockedMessage => Time.time < blockedUntil;

        private void Start() { SetActivePlayer(warrior); }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab)) TrySwitch();
        }

        public bool TrySwitch()
        {
            var current = ActivePlayer;
            if (current == null || !current.CanSwitchClass)
            {
                blockedUntil = Time.time + 1.2f;
                return false;
            }

            var next = NextPlayer;
            Vector3 position = current.transform.position;
            Quaternion facing = current.transform.rotation;
            current.gameObject.SetActive(false);
            next.gameObject.SetActive(true);
            next.MoveForClassSwitch(position, facing);
            SetActivePlayer(next);
            blockedUntil = 0f;
            return true;
        }

        private void SetActivePlayer(PuppetMotor player)
        {
            ActivePlayer = player;
            orbit.target = player.transform;
            help.player = player;
            if (opponent != null) opponent.SetPlayer(player);
        }
    }
}
