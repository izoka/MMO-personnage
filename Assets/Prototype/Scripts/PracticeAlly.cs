using UnityEngine;

namespace MMO.Prototype
{
    public sealed class PracticeAlly : MonoBehaviour
    {
        public int maxHealth = 100;
        public bool requiresRevive;
        public MageCombat support;
        public KayKitCharacterVisual visual;
        public TextMesh healthText, changeText;
        public Material markerMaterial;
        public TrainingTarget secondaryEnemy;

        private const float StrikeRange = 1.95f;
        private const float StrikeWindup = 0.38f;
        private const float StrikeDuration = 0.83f;
        private const float BetweenStrikes = 0.82f;
        private const int StrikeDamage = 18;
        private static readonly Color Friendly = new Color(0.34f, 0.96f, 1f);
        private float lastDamage = -10f, changeUntil, recoverAt;
        private float attackTime = -1f, nextAttack;
        private int comboStep;
        private LineRenderer selectionRing;
        private Collider body;
        private SparringOpponent opponent;
        private TrainingTarget primaryEnemy, enemy;
        private ArenaFootwork footwork;

        public int Health { get; private set; }
        public PoisonStatus Poison { get; private set; }
        public bool IsAlive => isActiveAndEnabled && Health > 0;
        public bool CanBeRevived => isActiveAndEnabled && requiresRevive && Health == 0;
        public int ShieldPoints => support != null ? support.ShieldPointsFor(this) : 0;
        public bool IsProtected => support != null && support.SanctuaryAt(transform.position);
        public Vector3 AimPoint => transform.position + Vector3.up;

        private void Awake()
        {
            body = GetComponent<Collider>();
            Poison = GetComponent<PoisonStatus>();
            footwork = GetComponent<ArenaFootwork>();
            selectionRing = MageEffects.Ring(transform, 0.69f, Quaternion.identity, markerMaterial, Friendly);
            selectionRing.transform.localPosition = Vector3.up * 0.045f;
            selectionRing.enabled = false;
            ResetAlly();
        }

        internal void BindOpponent(SparringOpponent sparring, TrainingTarget target)
        {
            opponent = sparring;
            primaryEnemy = enemy = target;
        }

        public void ResetAlly()
        {
            Health = maxHealth;
            Poison?.Clear();
            lastDamage = -10f;
            visual.ResetImpact();
            changeUntil = 0f;
            footwork?.ResetPosition();
            PrepareToFight(0.3f);
        }

        internal bool Revive(int health)
        {
            if (!CanBeRevived || health <= 0) return false;
            Health = Mathf.Min(maxHealth, health);
            Poison?.Clear();
            lastDamage = -10f;
            visual.ResetImpact();
            PrepareToFight(0.65f);
            ShowChange("RELEVÉ", new Color(1f, 0.88f, 0.53f));
            return true;
        }

        private void PrepareToFight(float delay)
        {
            body.enabled = true;
            attackTime = -1f;
            nextAttack = Time.time + delay;
            comboStep = 0;
        }

        public int Heal(int amount)
        {
            if (!IsAlive || amount <= 0) return 0;
            int restored = Mathf.Max(0, Mathf.Min(amount, maxHealth - Health));
            if (restored == 0) return 0;
            Health += restored;
            ShowChange("+" + restored, new Color(0.55f, 1f, 0.61f));
            return restored;
        }

        public void ReceiveHit(int damage) => ReceiveHit(new CombatHit(damage, transform.position + transform.forward));

        public HitResult ReceiveHit(CombatHit hit)
        {
            if (!IsAlive || hit.damage <= 0) return HitResult.Miss;
            int received = support != null ? support.AbsorbFor(hit.damage, this) : hit.damage;
            if (received <= 0)
            {
                visual.ReactToImpact(transform, hit, HitResult.Blocked);
                CombatImpactVfx.Present(body, AimPoint, hit, HitResult.Blocked, shielded: true);
                ShowChange("ABSORBÉ", new Color(1f, 0.82f, 0.48f));
                return HitResult.Blocked;
            }
            hit.damage = received;
            visual.ReactToImpact(transform, hit, HitResult.Hit);
            CombatImpactVfx.Present(body, AimPoint, hit, HitResult.Hit);
            Health = Mathf.Max(0, Health - received);
            lastDamage = Time.time;
            if (attackTime >= 0f && attackTime < StrikeWindup)
            {
                attackTime = -1f;
                nextAttack = Time.time + 0.4f;
                comboStep = 0;
            }
            ShowChange("−" + received, new Color(1f, 0.59f, 0.42f));
            if (Health == 0) Fall();
            return HitResult.Hit;
        }

        internal void TakePoisonDamage(int amount)
        {
            if (!IsAlive) return;
            Health = Mathf.Max(0, Health - amount);
            ShowChange("−" + amount + " · POISON", new Color(0.83f, 0.56f, 1f));
            if (Health == 0) Fall();
        }

        private void Fall()
        {
            Poison?.Clear();
            body.enabled = false;
            recoverAt = Time.time + 3f;
        }

        private void TickAttack(float dt)
        {
            var currentEnemy = primaryEnemy != null && primaryEnemy.Health > 0 ? primaryEnemy : secondaryEnemy;
            if (enemy != currentEnemy)
            {
                enemy = currentEnemy;
                attackTime = -1f;
                comboStep = 0;
            }

            if (!IsAlive || opponent == null || !opponent.AllyDuelActive ||
                enemy == null || !enemy.isActiveAndEnabled || enemy.Health <= 0)
            {
                footwork?.Stop();
                attackTime = -1f;
                nextAttack = Time.time + 0.25f;
                comboStep = 0;
                return;
            }

            Vector3 toward = Vector3.ProjectOnPlane(enemy.AimPoint - AimPoint, Vector3.up);
            if (attackTime < StrikeWindup && toward.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toward), 1f - Mathf.Exp(-10f * dt));

            if (attackTime < 0f)
            {
                if (Time.time - lastDamage < 0.2f) { footwork?.Stop(); return; }
                bool canStrike = CanStrike(toward);
                bool inPosition = footwork == null || toward.sqrMagnitude <= 1.50f * 1.50f;
                if (Time.time >= nextAttack && canStrike && inPosition)
                {
                    footwork?.Stop();
                    attackTime = 0f;
                }
                else if (footwork != null)
                {
                    bool hold = canStrike && inPosition;
                    Vector3 goal = hold ? transform.position : enemy.transform.position - toward.normalized * 1.30f;
                    if (!hold && toward.sqrMagnitude <= 1.75f * 1.75f && !HasStrikeLine())
                        goal += Vector3.Cross(Vector3.up, toward.normalized) * 1.25f;
                    footwork.MoveTo(goal, dt);
                }
                return;
            }

            float previous = attackTime;
            attackTime += dt;
            if (previous < StrikeWindup && attackTime >= StrikeWindup && CanStrike(toward))
            {
                var hit = new CombatHit(StrikeDamage, transform.position, bladeTip: visual.BladeTip);
                var result = enemy.ReceiveHit(hit);
                visual.MarkMeleeContact((WarriorMove)((int)WarriorMove.Light1 + comboStep), StrikeWindup, hit, result);
            }

            if (attackTime < StrikeDuration) return;
            attackTime = -1f;
            nextAttack = Time.time + BetweenStrikes;
            comboStep = (comboStep + 1) % 3;
        }

        private bool CanStrike(Vector3 toward)
        {
            if (toward.sqrMagnitude > StrikeRange * StrikeRange || Mathf.Abs(enemy.AimPoint.y - AimPoint.y) > 0.9f ||
                Vector3.Angle(transform.forward, toward) > 55f) return false;
            return HasStrikeLine();
        }

        private bool HasStrikeLine()
        {
            return !Physics.Linecast(AimPoint, enemy.AimPoint, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                || hit.collider.GetComponentInParent<TrainingTarget>() == enemy;
        }

        private void ShowChange(string value, Color color)
        {
            changeText.text = value;
            changeText.color = color;
            changeUntil = Time.time + 0.8f;
        }

        private void LateUpdate()
        {
            if (Health <= 0 && !requiresRevive && Time.time >= recoverAt) ResetAlly();
            float dt = Time.deltaTime;
            TickAttack(dt);
            var move = attackTime >= 0f ? (WarriorMove)((int)WarriorMove.Light1 + comboStep) : WarriorMove.None;
            visual.Present(footwork?.PoseSpeed ?? 0f, true, 0f, move, Mathf.Max(0f, attackTime), StrikeWindup, StrikeDuration, -1f, false, -1f,
                Time.time - lastDamage < 0.20f, Health <= 0, dt, localLockMovement: footwork?.LocalVelocity ?? Vector3.zero);
            selectionRing.enabled = support != null && support.SelectedAlly == this;
            string protection = ShieldPoints > 0 ? "\nÉGIDE " + ShieldPoints : IsProtected ? "\nSANCTUAIRE" : "";
            string poison = Poison != null && Poison.PulsesLeft > 0 ? "\nPOISON · " + Poison.PulsesLeft : "";
            healthText.text = Health > 0 ? "ALLIÉ\n" + Health + " / " + maxHealth + protection + poison :
                CanBeRevived && selectionRing.enabled ? "ALLIÉ AU SOL\n6 · RELÈVEMENT" : "ALLIÉ AU SOL";
            bool showChange = Time.time < changeUntil;
            changeText.gameObject.SetActive(showChange);
            if (showChange)
                changeText.transform.localPosition = Vector3.up * (2.9f + (1f - (changeUntil - Time.time) / 0.8f) * 0.38f);
            var camera = Camera.main;
            if (camera == null) return;
            healthText.transform.rotation = camera.transform.rotation;
            changeText.transform.rotation = camera.transform.rotation;
        }
    }
}
