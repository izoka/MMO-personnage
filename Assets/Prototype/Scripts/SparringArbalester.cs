using UnityEngine;

namespace MMO.Prototype
{
    [RequireComponent(typeof(TrainingTarget))]
    public sealed class SparringArbalester : MonoBehaviour
    {
        public SparringOpponent melee;
        public KayKitCharacterVisual character;
        public AnimationClip shootClip, reloadClip;
        public Transform muzzle;
        public Material boltMaterial;
        public bool poisonBolts;

        private const float Windup = 0.95f;
        private const float Recovery = 0.62f;
        private const float ReloadDelay = 1.55f;
        private const float Range = 15f;
        private const int Damage = 12;
        private static readonly Color BoltTint = new Color(1f, 0.43f, 0.25f);
        private static readonly Color PoisonTint = new Color(0.78f, 0.35f, 1f);

        private TrainingTarget fighter, meleeFighter;
        private Collider body;
        private ArenaFootwork footwork;
        private PracticeAlly ally;
        private PuppetMotor Player => melee.ActivePlayer;
        private Transform victim;
        private RangerBolt flight;
        private float attackTime = -1f, nextShot;
        private int hitsAtStart, shotsFired;
        private Vector3 repositionGoal;
        private bool repositioning;

        public bool WindingUp => attackTime >= 0f && attackTime < Windup;
        private bool PoisonedShot => poisonBolts && (shotsFired + 1) % 2 == 0;
        public bool EncounterCleared => fighter != null && fighter.Health <= 0 && meleeFighter.Health <= 0;
        public string StateLabel => !melee.autoAttack ? "PAUSE · T" : !melee.AllyDuelActive ? "EN ATTENTE" :
            fighter.IsStunned ? "TIR INTERROMPU" : WindingUp ? AimLabel :
            attackTime >= Windup ? "RECHARGE" : footwork != null && footwork.IsMoving ? "SE REPLACE" : "OBSERVE";
        private string AimLabel => PoisonedShot ? (victim == ally.transform ? "TOXIQUE · ALLIÉ" : "TOXIQUE · TOI") :
            victim == ally.transform ? "VISE L’ALLIÉ" : "TE VISE";

        private void Awake()
        {
            fighter = GetComponent<TrainingTarget>();
            meleeFighter = melee.GetComponent<TrainingTarget>();
            body = GetComponent<Collider>();
            footwork = GetComponent<ArenaFootwork>();
            ally = melee.practiceAlly;
            ResetCycle();
        }

        public void ResetCycle()
        {
            CancelShot(0.85f);
            shotsFired = 0;
            repositioning = false;
            footwork?.Stop();
            if (flight == null) return;
            flight.gameObject.SetActive(false);
            Destroy(flight.gameObject);
            flight = null;
        }

        private void CancelShot(float delay)
        {
            victim = null;
            attackTime = -1f;
            nextShot = Time.time + delay;
        }

        private void Update()
        {
            if (!melee.AllyDuelActive || fighter.Health <= 0)
            {
                ResetCycle();
                Present();
                return;
            }

            if (fighter.IsStunned || (WindingUp && fighter.TotalHits != hitsAtStart))
            {
                if (!fighter.IsStunned) fighter.Stagger(0.60f);
                CancelShot(0.85f);
                repositioning = false;
                footwork?.Stop();
            }
            else if (footwork != null && WindingUp && !CanAimAt(victim))
            {
                CancelShot(0.30f);
            }
            else if (attackTime < 0f)
            {
                if (footwork != null) PrepareMobileShot(Time.deltaTime);
                else if (Time.time >= nextShot)
                {
                    var target = ChooseVictim();
                    if (target != null) BeginShot(target);
                }
            }
            else
            {
                float previous = attackTime;
                attackTime += Time.deltaTime;
                if (previous < Windup && attackTime >= Windup && CanAimAt(victim)) Shoot();
                if (attackTime >= Windup + Recovery)
                {
                    if (footwork != null && victim != null)
                    {
                        Vector3 toward = Vector3.ProjectOnPlane(victim.position - transform.position, Vector3.up).normalized;
                        float side = (shotsFired / 2) % 2 == 0 ? 1f : -1f;
                        repositionGoal = transform.position + Vector3.Cross(Vector3.up, toward) * (2.8f * side);
                        repositioning = true;
                    }
                    CancelShot(ReloadDelay);
                }
            }

            if (WindingUp && victim != null)
            {
                Vector3 direction = Vector3.ProjectOnPlane(victim.position - transform.position, Vector3.up);
                if (direction.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 1f - Mathf.Exp(-9f * Time.deltaTime));
            }
            Present();
        }

        private void BeginShot(Transform target)
        {
            victim = target;
            attackTime = 0f;
            hitsAtStart = fighter.TotalHits;
            repositioning = false;
            footwork?.Stop();
        }

        private void PrepareMobileShot(float dt)
        {
            Transform target = (shotsFired + 1) % 3 == 0 || !ally.IsAlive ? Player.transform : ally.transform;
            if ((target.position - transform.position).sqrMagnitude > Range * Range && ally.IsAlive)
                target = ally.transform;

            Vector3 offset = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up);
            Vector3 toward = offset.sqrMagnitude > 0.01f ? offset.normalized : transform.forward;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toward), 1f - Mathf.Exp(-8f * dt));
            float distance = offset.magnitude;
            bool clear = CanAimAt(target);
            if (Time.time >= nextShot && clear && distance <= 8f &&
                (distance >= 2.85f || (!footwork.IsMoving && Time.time >= nextShot + 0.8f)))
            {
                BeginShot(target);
                return;
            }

            Vector3 goal;
            if (distance < 3.25f) goal = target.position - toward * 4.45f;
            else if (distance > 7.15f) goal = target.position - toward * 6.2f;
            else if (!clear)
            {
                float side = (shotsFired / 2) % 2 == 0 ? 1f : -1f;
                goal = transform.position + Vector3.Cross(Vector3.up, toward) * (2.25f * side) + toward * 0.35f;
            }
            else goal = repositioning ? repositionGoal : transform.position;
            footwork.MoveTo(goal, dt);
        }

        private Transform ChooseVictim()
        {
            if ((shotsFired + 1) % 3 == 0 && CanAimAt(Player.transform)) return Player.transform;
            if (CanAimAt(ally.transform)) return ally.transform;
            return CanAimAt(Player.transform) ? Player.transform : null;
        }

        private bool CanAimAt(Transform target)
        {
            if (target == null || (target == ally.transform ? !ally.IsAlive : target != Player.transform || Player.Health <= 0)) return false;
            Vector3 point = target.position + Vector3.up;
            if ((point - fighter.AimPoint).sqrMagnitude > Range * Range) return false;
            return !Physics.Linecast(fighter.AimPoint, point, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                hit.collider == body;
        }

        private void Shoot()
        {
            bool poisoned = PoisonedShot;
            shotsFired++;
            Vector3 origin = fighter.AimPoint;
            Vector3 heading = (victim.position + Vector3.up - origin).normalized;
            flight = new GameObject(poisoned ? "Carreau toxique" : "Carreau ennemi").AddComponent<RangerBolt>();
            flight.Launch(origin, muzzle.position, heading, 18f, Range, poisoned ? PoisonTint : BoltTint, boltMaterial,
                Physics.AllLayers, body, contact => Hit(contact, poisoned));
        }

        private void Hit(RaycastHit contact, bool poisoned)
        {
            if (!melee.AllyDuelActive) return;
            if (contact.collider.transform.IsChildOf(ally.transform))
            {
                int health = ally.Health;
                ally.ReceiveHit(new CombatHit(Damage, transform.position, source: fighter, contact: contact.point));
                if (poisoned && ally.Health < health) ally.Poison?.Apply();
            }
            else if (contact.collider.transform.IsChildOf(Player.transform))
            {
                var player = Player;
                int health = player.Health;
                player.ReceiveHit(new CombatHit(Damage, transform.position, source: fighter, contact: contact.point));
                if (poisoned && player.Health < health) player.Poison?.Apply();
            }
        }

        private void Present()
        {
            bool shooting = attackTime >= 0f;
            AnimationClip clip = shooting ? (WindingUp ? shootClip : reloadClip) : null;
            float phase = WindingUp ? Mathf.Lerp(0.04f, 0.48f, attackTime / Windup) :
                Mathf.InverseLerp(Windup, Windup + Recovery, attackTime);
            character.Present(footwork?.PoseSpeed ?? 0f, true, 0f, WarriorMove.None, 0f, 0f, 0f, -1f, false, -1f, fighter.IsStunned,
                fighter.Health <= 0, Time.deltaTime, clip, phase, WindingUp ? 8 : 9, footwork?.LocalVelocity ?? Vector3.zero);
        }
    }
}
