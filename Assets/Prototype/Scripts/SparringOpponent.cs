using UnityEngine;

namespace MMO.Prototype
{
    public sealed class SparringOpponent : MonoBehaviour
    {
        public bool autoAttack = true;
        public float windup = 0.85f;
        public int damage = 18;
        public PracticeAlly practiceAlly;
        public ArenaAttackSector sweepSector;
        public bool groupForAnyClass;
        private const float SweepWindup = 1.38f;
        private const int SweepDamage = 28;
        private TrainingTarget fighter;
        private PuppetRig rig;
        private KayKitCharacterVisual character;
        private ArenaFootwork footwork;
        private PuppetMotor player;
        private PracticeAlly attackAlly;
        private float attackTime = -1f, nextAttack;
        private bool hitApplied, flanking, sweeping;
        private int swings;
        private Vector3 flankGoal;
        private float ActiveWindup => sweeping ? SweepWindup : windup;
        public bool WindingUp => attackTime >= 0f && attackTime < ActiveWindup;
        internal PuppetMotor ActivePlayer => player;
        internal bool AllyDuelActive => isActiveAndEnabled && autoAttack && player != null && player.Health > 0 &&
            (groupForAnyClass || player.Mage?.isHealer == true);
        public string StateLabel => !autoAttack ? "PAUSE · T" : fighter.IsStunned ? "OUVERTURE" :
            WindingUp ? (sweeping ? "BALAYAGE" : "ATTAQUE...") : fighter.IsGuarding ? "GARDE" : "REPRISE";

        private void Awake()
        {
            fighter = GetComponent<TrainingTarget>();
            rig = GetComponentInChildren<PuppetRig>();
            character = GetComponentInChildren<KayKitCharacterVisual>();
            footwork = GetComponent<ArenaFootwork>();
            player = FindFirstObjectByType<PuppetMotor>();
            if (practiceAlly != null) practiceAlly.BindOpponent(this, fighter);
            ResetCycle();
        }
        public void ResetCycle()
        {
            attackTime = -1f;
            nextAttack = Time.time + 1.2f;
            hitApplied = flanking = sweeping = false;
            attackAlly = null;
            swings = 0;
            footwork?.Stop();
            sweepSector?.Hide();
        }
        public void SetPlayer(PuppetMotor activePlayer)
        {
            player = activePlayer;
            if ((attackAlly != null || sweeping) && !groupForAnyClass && player.Mage?.isHealer != true) ResetCycle();
        }
        public static void ToggleAll()
        {
            var tower = FindFirstObjectByType<TowerFloor>();
            if (tower != null) { tower.TogglePause(); return; }
            var opponents = FindObjectsByType<SparringOpponent>(FindObjectsSortMode.None);
            if (opponents.Length == 0) return;
            bool enable = !opponents[0].autoAttack;
            foreach (var opponent in opponents) { opponent.autoAttack = enable; opponent.ResetCycle(); }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (player == null || fighter.Health <= 0)
            {
                if (attackTime >= 0f) ResetCycle();
                footwork?.Stop();
                if (fighter.Health <= 0) Present(dt);
                return;
            }
            bool mobile = footwork != null && AllyDuelActive;
            Vector3 toPlayer = Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up);
            var ally = attackTime >= 0f ? attackAlly : ChooseAlly(toPlayer.sqrMagnitude);
            Vector3 victimPosition = ally != null ? ally.transform.position : player.transform.position;
            Vector3 delta = Vector3.ProjectOnPlane(victimPosition - transform.position, Vector3.up);
            if (!autoAttack || fighter.IsStunned || player.Health <= 0 || (!sweeping && ally != null && !ally.IsAlive))
            {
                attackTime = -1f;
                attackAlly = null;
                flanking = sweeping = false;
                sweepSector?.Hide();
                footwork?.Stop();
                fighter.SetGuard(false);
                nextAttack = Mathf.Max(nextAttack, Time.time + 0.7f);
            }
            else if (attackTime < 0f)
            {
                if (delta.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(delta), 1f - Mathf.Exp(-(mobile ? 8f : 6f) * dt));
                bool close = delta.sqrMagnitude <= (mobile ? 1.85f * 1.85f : 2.4f * 2.4f);
                if (mobile && close) close = HasStrikeLine(victimPosition);
                if (Time.time >= nextAttack) flanking = false;
                if (close && !flanking && Time.time >= nextAttack)
                {
                    attackTime = 0f; attackAlly = ally; hitApplied = false;
                    sweeping = mobile && sweepSector != null && (swings % 2) == 1;
                    if (sweeping)
                    {
                        Vector3 heading = delta.sqrMagnitude > 0.01f ? delta.normalized : transform.forward;
                        transform.rotation = Quaternion.LookRotation(heading);
                        sweepSector.Announce(transform.position, heading);
                    }
                    footwork?.Stop();
                    fighter.SetGuard(false);
                }
                else
                {
                    if (mobile && delta.sqrMagnitude <= 100f)
                    {
                        Vector3 goal = flanking ? flankGoal : close ? transform.position :
                            victimPosition - delta.normalized * 1.48f;
                        if (!flanking && !close && delta.sqrMagnitude < 1.7f * 1.7f)
                            goal += Vector3.Cross(Vector3.up, transform.forward) * 1.35f;
                        footwork.MoveTo(goal, dt);
                    }
                    else footwork?.Stop();
                    fighter.SetGuard(footwork == null || !footwork.IsMoving);
                }
            }
            else
            {
                fighter.SetGuard(false);
                attackTime += dt;
                if (sweeping && !hitApplied) sweepSector.SetProgress(attackTime / ActiveWindup);
                if (!hitApplied && attackTime >= ActiveWindup)
                {
                    hitApplied = true;
                    if (sweeping) ResolveSweep();
                    else if (delta.magnitude <= 2.1f && Vector3.Angle(transform.forward, delta) <= 55f && HasStrikeLine(victimPosition))
                    {
                        var hit = new CombatHit(damage, transform.position, source: fighter, bladeTip: character?.BladeTip);
                        var result = ally != null ? ally.ReceiveHit(hit) : player.ReceiveHit(hit);
                        character?.MarkMeleeContact(WarriorMove.Heavy, ActiveWindup, hit, result);
                    }
                }
                if (attackTime >= ActiveWindup + 0.55f)
                {
                    attackTime = -1f; attackAlly = null;
                    sweeping = false;
                    nextAttack = Time.time + (mobile ? 1.57f : 1.1f);
                    if (mobile && delta.sqrMagnitude > 0.01f)
                    {
                        float side = (swings++ / 2) % 2 == 0 ? 1f : -1f;
                        flankGoal = transform.position + Vector3.Cross(Vector3.up, delta.normalized) * (3.75f * side);
                        flanking = true;
                    }
                }
            }
            Present(dt);
        }

        private void Present(float dt)
        {
            if (character != null)
                character.Present(footwork?.PoseSpeed ?? 0f, true, 0f, attackTime >= 0f ? (sweeping ? WarriorMove.Sweep : WarriorMove.Heavy) : WarriorMove.None,
                    Mathf.Max(0f, attackTime), ActiveWindup, ActiveWindup + 0.55f, -1f, fighter.IsGuarding, -1f, fighter.IsStunned, fighter.Health <= 0, dt,
                    localLockMovement: footwork?.LocalVelocity ?? Vector3.zero);
            else if (rig != null && fighter.Health > 0)
                rig.PoseWarrior(0f, true, 0f, attackTime >= 0f ? WarriorMove.Heavy : WarriorMove.None,
                    Mathf.Max(0f, attackTime), windup, windup + 0.55f, -1f, fighter.IsGuarding, -1f, fighter.IsStunned, dt);
        }

        private void ResolveSweep()
        {
            var hit = new CombatHit(SweepDamage, sweepSector.Origin, parryable: false, source: fighter, bladeTip: character?.BladeTip);
            if (practiceAlly != null && practiceAlly.IsAlive && sweepSector.CanHit(practiceAlly.transform.position))
            {
                var result = practiceAlly.ReceiveHit(hit);
                character?.MarkMeleeContact(WarriorMove.Sweep, ActiveWindup, hit, result);
            }
            if (player.Health > 0 && sweepSector.CanHit(player.transform.position))
            {
                var result = player.ReceiveHit(hit);
                character?.MarkMeleeContact(WarriorMove.Sweep, ActiveWindup, hit, result);
            }
            sweepSector.FlashImpact();
        }

        private void OnDisable() { sweepSector?.Hide(); }

        private PracticeAlly ChooseAlly(float playerDistanceSquared)
        {
            if (!AllyDuelActive || practiceAlly == null || !practiceAlly.IsAlive) return null;
            Vector3 offset = Vector3.ProjectOnPlane(practiceAlly.transform.position - transform.position, Vector3.up);
            float reach = footwork != null ? 10f : 2.4f;
            return offset.sqrMagnitude <= reach * reach && offset.sqrMagnitude < playerDistanceSquared ? practiceAlly : null;
        }

        private bool HasStrikeLine(Vector3 victimPosition) =>
            (footwork == null || Mathf.Abs(victimPosition.y - transform.position.y) < 0.9f) &&
            !Physics.Linecast(transform.position + Vector3.up, victimPosition + Vector3.up,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }
}
