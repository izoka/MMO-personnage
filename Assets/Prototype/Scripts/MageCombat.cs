using System.Collections.Generic;
using UnityEngine;

namespace MMO.Prototype
{
    public enum MageSpell { None, Bolt, FireOrb, Frost, Nova, Barrier, Light, Mend, Renew, Aegis, Sanctuary, Purify, Revive }

    public sealed class MageCombat : MonoBehaviour
    {
        public const float MaxMana = 100f;
        public AnimationClip[] castClips;
        public Material effectMaterial;
        public Material trailMaterial;
        public Transform castFocus;
        public bool isHealer;
        public PracticeAlly practiceAlly;

        private const int RenewPulses = 5;
        private const float SupportRange = 14f;
        private const float SanctuaryRadius = 2.4f;
        private const float SanctuaryDuration = 5f;

        private readonly struct Spell
        {
            public readonly string Name;
            public readonly float Cost, Windup, Recovery, Cooldown, Speed, Range, Radius;
            public readonly int Power;
            public readonly Color Color;

            public Spell(string name, float cost, float windup, float recovery, float cooldown, int power,
                float speed, float range, float radius, Color color)
            {
                Name = name; Cost = cost; Windup = windup; Recovery = recovery; Cooldown = cooldown;
                Power = power; Speed = speed; Range = range; Radius = radius; Color = color;
            }
        }

        private static readonly Spell[] Spells =
        {
            default,
            new Spell("Trait d’arcane", 0f, 0.19f, 0.23f, 0.42f, 18, 21f, 15f, 0f, new Color(0.76f, 0.35f, 1f)),
            new Spell("Orbe de feu", 24f, 0.48f, 0.32f, 5f, 38, 12f, 14f, 2.1f, new Color(1f, 0.43f, 0.12f)),
            new Spell("Éclat de givre", 18f, 0.32f, 0.26f, 6f, 22, 18f, 14f, 0f, new Color(0.28f, 0.86f, 1f)),
            new Spell("Onde arcanique", 25f, 0.43f, 0.34f, 8f, 30, 0f, 0f, 3f, new Color(0.73f, 0.39f, 1f)),
            new Spell("Barrière", 26f, 0.35f, 0.24f, 12f, 0, 0f, 0f, 0f, new Color(0.25f, 0.71f, 1f)),
            new Spell("Trait lumineux", 0f, 0.24f, 0.24f, 0.5f, 12, 22f, 15f, 0f, new Color(1f, 0.86f, 0.53f)),
            new Spell("Soin direct", 24f, 0.52f, 0.26f, 4f, 36, 0f, 0f, 0f, new Color(0.43f, 1f, 0.55f)),
            new Spell("Régénération", 22f, 0.38f, 0.24f, 8f, 8, 0f, 0f, 0f, new Color(0.50f, 1f, 0.76f)),
            new Spell("Égide", 25f, 0.38f, 0.25f, 10f, 42, 0f, 0f, 0f, new Color(1f, 0.72f, 0.32f)),
            new Spell("Sanctuaire", 28f, 0.55f, 0.32f, 12f, 0, 0f, 0f, SanctuaryRadius, new Color(1f, 0.83f, 0.46f)),
            new Spell("Purification", 14f, 0.26f, 0.22f, 3.5f, 0, 0f, 0f, 0f, new Color(0.65f, 0.91f, 1f)),
            new Spell("Relèvement", 32f, 1.45f, 0.35f, 12f, 45, 0f, 0f, 0f, new Color(1f, 0.86f, 0.48f))
        };

        private PuppetMotor player;
        private MageVfx vfx;
        private TrainingTarget castTarget;
        private PracticeAlly castAlly, renewAlly, barrierAlly;
        private readonly float[] readyAt = new float[Spells.Length];
        private readonly HashSet<MageProjectile> projectiles = new HashSet<MageProjectile>();
        private float age, lastSpend = -10f, barrierUntil, messageUntil;
        private int barrierPoints;
        private int renewRemaining;
        private float nextRenew, sanctuaryUntil;
        private bool released, allySelected;
        private Vector3 direction;
        private GameObject barrier;
        private GameObject sanctuary;
        private string message;

        public float Mana { get; private set; } = MaxMana;
        public MageSpell CurrentSpell { get; private set; }
        public bool IsCasting => CurrentSpell != MageSpell.None;
        internal float ActionTimeRemaining => IsCasting
            ? Spells[(int)CurrentSpell].Windup + Spells[(int)CurrentSpell].Recovery - age : 0f;
        public bool CanPurify => isHealer && player != null && player.Poison != null && HasCastClip(MageSpell.Purify);
        public bool HasRevive => isHealer && practiceAlly != null && practiceAlly.requiresRevive && HasCastClip(MageSpell.Revive);
        public bool HasCastAim => castTarget != null || castAlly != null;
        public Vector3 CastFacing => direction;
        public PracticeAlly SelectedAlly => allySelected && practiceAlly != null && (practiceAlly.IsAlive || practiceAlly.CanBeRevived)
            ? practiceAlly : null;
        private int ActiveBarrierPoints => barrier != null && Time.time < barrierUntil ? barrierPoints : 0;
        public int BarrierPoints => ShieldPointsFor(null);
        public bool InSanctuary => SanctuaryAt(transform.position);
        public AnimationClip CurrentClip => IsCasting ? castClips[(int)CurrentSpell] : null;
        public string Status => Time.time < messageUntil ? message :
            IsCasting ? Spells[(int)CurrentSpell].Name.ToUpperInvariant() :
            BarrierPoints > 0 ? (isHealer ? "ÉGIDE · " : "BARRIÈRE · ") + BarrierPoints :
            InSanctuary ? "SANCTUAIRE · DÉGÂTS RÉDUITS" : renewRemaining > 0 ? "RÉGÉNÉRATION" :
            SelectedAlly != null ? (SelectedAlly.CanBeRevived ? "ALLIÉ AU SOL · 6 POUR RELEVER" : "CIBLE DE SOUTIEN · ALLIÉ") : "";
        public float AnimationProgress
        {
            get
            {
                if (!IsCasting) return 0f;
                Spell spell = Spells[(int)CurrentSpell];
                float contact = CurrentSpell == MageSpell.FireOrb ? 0.62f : 0.54f;
                return KayKitCharacterVisual.SampleThroughContact(age, spell.Windup, spell.Windup + spell.Recovery, contact);
            }
        }

        private void Awake()
        {
            player = GetComponent<PuppetMotor>();
            vfx = GetComponent<MageVfx>();
        }

        public static string SpellName(MageSpell spell) => Spells[(int)spell].Name;
        public float Cooldown(MageSpell spell) => Mathf.Max(0f, readyAt[(int)spell] - Time.time);
        private MageSpell PrimarySpell => isHealer ? MageSpell.Light : MageSpell.Bolt;
        internal float PrimaryCooldown => Cooldown(PrimarySpell);
        public bool RequestPrimary() => RequestCast(PrimarySpell);

        private bool HasCastClip(MageSpell spell) =>
            castClips != null && castClips.Length > (int)spell && castClips[(int)spell] != null;

        public void ReadInput()
        {
            if (isHealer && practiceAlly != null && Input.GetKeyDown(KeyCode.X)) ToggleAllyTarget();
            if (Input.GetMouseButtonDown(0)) player.RequestAttack();
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) RequestCast(isHealer ? MageSpell.Mend : MageSpell.FireOrb);
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) RequestCast(isHealer ? MageSpell.Renew : MageSpell.Frost);
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) RequestCast(isHealer ? MageSpell.Aegis : MageSpell.Nova);
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) RequestCast(isHealer ? MageSpell.Sanctuary : MageSpell.Barrier);
            if (CanPurify && (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))) RequestCast(MageSpell.Purify);
            if (HasRevive && (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6))) RequestCast(MageSpell.Revive);
        }

        public bool ToggleAllyTarget()
        {
            if (!isActiveAndEnabled || !isHealer || practiceAlly == null || player.Health <= 0) return false;
            if (allySelected)
            {
                allySelected = false;
                Tell("CIBLE DE SOUTIEN · SOI-MÊME");
                return false;
            }
            string problem = AllyProblem(practiceAlly, HasRevive);
            if (problem != null) { Tell(problem); return false; }
            allySelected = true;
            Tell("CIBLE DE SOUTIEN · ALLIÉ");
            return true;
        }

        private string AllyProblem(PracticeAlly ally, bool allowDowned = false)
        {
            if (ally == null || (!ally.IsAlive && !(allowDowned && ally.CanBeRevived))) return "ALLIÉ AU SOL";
            Vector3 origin = transform.position + Vector3.up;
            if ((ally.AimPoint - origin).sqrMagnitude > SupportRange * SupportRange) return "ALLIÉ HORS DE PORTÉE";
            if (Physics.Linecast(origin, ally.AimPoint, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return "ALLIÉ MASQUÉ";
            return null;
        }

        public bool RequestCast(MageSpell spell)
        {
            if (spell <= MageSpell.None || (int)spell >= Spells.Length || (spell >= MageSpell.Light) != isHealer || !isActiveAndEnabled || IsCasting ||
                player.Health <= 0 || player.IsStunned || player.IsDodging || !player.Grounded) return false;
            if (spell == MageSpell.Purify && !CanPurify) return false;
            if (spell == MageSpell.Revive)
            {
                if (!HasRevive) return false;
                if (!allySelected) { Tell("X · SÉLECTIONNE L’ALLIÉ"); return false; }
                if (!practiceAlly.CanBeRevived) { Tell("L’ALLIÉ EST DEBOUT"); return false; }
            }
            Spell definition = Spells[(int)spell];
            if (Cooldown(spell) > 0f) { Tell("SORT EN RECHARGE"); return false; }
            if (Mana < definition.Cost) { Tell("MANA INSUFFISANT"); return false; }
            bool targetsAlly = spell >= MageSpell.Mend && allySelected;
            if (targetsAlly)
            {
                string problem = AllyProblem(practiceAlly, spell == MageSpell.Revive);
                if (problem != null) { Tell(problem); return false; }
            }
            if (spell == MageSpell.Purify)
            {
                var poison = targetsAlly ? practiceAlly.Poison : player.Poison;
                if (poison == null || poison.PulsesLeft == 0) { Tell("AUCUN POISON SUR CETTE CIBLE"); return false; }
            }

            Mana -= definition.Cost;
            if (definition.Cost > 0f) lastSpend = Time.time;
            readyAt[(int)spell] = Time.time + definition.Cooldown;
            direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            castTarget = definition.Speed > 0f ? player.LockedTarget : null;
            castAlly = targetsAlly ? practiceAlly : null;
            if (HasCastAim)
            {
                Vector3 aimPoint = castAlly != null ? castAlly.AimPoint : castTarget.AimPoint;
                Vector3 aim = Vector3.ProjectOnPlane(aimPoint - transform.position, Vector3.up);
                if (aim.sqrMagnitude > 0.01f) direction = aim.normalized;
            }
            CurrentSpell = spell;
            age = 0f;
            released = false;
            messageUntil = 0f;
            vfx?.Cast(castFocus, spell, definition.Color, definition.Windup);
            return true;
        }

        public void Tick(float dt)
        {
            if (barrier != null && (ActiveBarrierPoints <= 0 || (barrierAlly != null && !barrierAlly.IsAlive))) RemoveBarrier();
            if (isHealer)
            {
                if (player.Health <= 0) { ClearEffects(); return; }
                if (allySelected && SelectedAlly == null) { allySelected = false; Tell("ALLIÉ AU SOL"); }
                if (sanctuary != null && Time.time >= sanctuaryUntil) RemoveSanctuary();
                if (renewAlly is not null && (renewAlly == null || !renewAlly.IsAlive)) { renewRemaining = 0; renewAlly = null; }
                while (renewRemaining > 0 && Time.time >= nextRenew)
                {
                    renewRemaining--;
                    nextRenew += 1f;
                    Heal(renewAlly, Spells[(int)MageSpell.Renew], true);
                    if (renewRemaining == 0) renewAlly = null;
                }
            }
            if (!IsCasting)
            {
                if (Time.time - lastSpend >= 1.1f && player.Health > 0)
                    Mana = Mathf.Min(MaxMana, Mana + 12f * dt);
                return;
            }
            Spell spell = Spells[(int)CurrentSpell];
            age += dt;
            if (!released && age >= spell.Windup)
            {
                released = true;
                Release(CurrentSpell, spell);
            }
            if (age >= spell.Windup + spell.Recovery) Interrupt();
        }

        private void Release(MageSpell kind, Spell spell)
        {
            if (castAlly is not null)
            {
                string problem = AllyProblem(castAlly, kind == MageSpell.Revive);
                if (problem != null)
                {
                    Refund(kind, spell, problem);
                    return;
                }
            }

            Vector3 origin = transform.position + Vector3.up * 1.05f;
            Transform recipient = castAlly != null ? castAlly.transform : transform;
            if (kind == MageSpell.Revive)
            {
                if (castAlly == null || !castAlly.Revive(spell.Power))
                    Refund(kind, spell, "RELÈVEMENT ANNULÉ");
                else
                {
                    vfx?.Revive(recipient, spell.Color);
                    Tell("ALLIÉ RELEVÉ · " + castAlly.Health + " PV");
                }
                return;
            }
            if (kind == MageSpell.Purify)
            {
                var poison = castAlly != null ? castAlly.Poison : player.Poison;
                if (poison == null || !poison.Clear())
                    Refund(kind, spell, "LE POISON S’EST DISSIPÉ");
                else
                {
                    vfx?.Purify(recipient, spell.Color);
                    Tell(castAlly != null ? "ALLIÉ PURIFIÉ" : "POISON DISSIPÉ");
                }
                return;
            }
            if (kind == MageSpell.Barrier || kind == MageSpell.Aegis)
            {
                RemoveBarrier();
                float duration = kind == MageSpell.Aegis ? 6f : 5f;
                barrierAlly = castAlly;
                barrierPoints = kind == MageSpell.Aegis ? spell.Power : 40;
                barrierUntil = Time.time + duration;
                barrier = new GameObject("Arcane Barrier");
                barrier.transform.SetParent(recipient, false);
                barrier.transform.localPosition = Vector3.up * 0.85f;
                if (vfx != null) vfx.Shield(barrier.transform, trailMaterial, spell.Color, duration);
                else
                {
                    MageEffects.Ring(barrier.transform, 0.92f, Quaternion.identity, trailMaterial, spell.Color);
                    MageEffects.Ring(barrier.transform, 0.92f, Quaternion.Euler(90f, 0f, 0f), trailMaterial, spell.Color);
                    MageEffects.Ring(barrier.transform, 0.92f, Quaternion.Euler(0f, 0f, 90f), trailMaterial, spell.Color);
                }
                return;
            }
            if (kind == MageSpell.Mend) { Heal(castAlly, spell, false); return; }
            if (kind == MageSpell.Renew)
            {
                renewRemaining = RenewPulses;
                nextRenew = Time.time + 1f;
                renewAlly = castAlly;
                vfx?.Healing(recipient, spell.Color, true);
                return;
            }
            if (kind == MageSpell.Sanctuary)
            {
                RemoveSanctuary();
                sanctuary = new GameObject("Sanctuaire");
                sanctuary.transform.position = recipient.position + Vector3.up * 0.04f;
                sanctuaryUntil = Time.time + SanctuaryDuration;
                vfx?.Sanctuary(sanctuary.transform, spell.Radius, spell.Color, SanctuaryDuration, trailMaterial);
                return;
            }
            if (kind == MageSpell.Nova)
            {
                AreaHit(origin, spell, 0.35f);
                Vector3 center = transform.position + Vector3.up * 0.12f;
                if (vfx != null) vfx.Nova(center, spell.Radius, spell.Color);
                else MageEffects.Burst(center, spell.Radius, trailMaterial, spell.Color);
                return;
            }
            var missile = new GameObject(spell.Name).AddComponent<MageProjectile>();
            projectiles.Add(missile);
            Vector3 aimOffset = castTarget != null && castTarget.Health > 0 ? castTarget.AimPoint - origin : Vector3.zero;
            Vector3 heading = aimOffset.sqrMagnitude > 0.01f ? aimOffset.normalized : direction;
            missile.Launch(this, kind, origin, castFocus != null ? castFocus.position : origin, heading, spell.Speed, spell.Range,
                kind == MageSpell.FireOrb ? 0.22f : 0.14f, spell.Color, effectMaterial, trailMaterial, vfx);
        }

        internal void Impact(MageSpell kind, Vector3 position, TrainingTarget target, Vector3 origin)
        {
            Spell spell = Spells[(int)kind];
            if (kind == MageSpell.FireOrb)
                AreaHit(position, spell, 0f);
            else if (target != null)
                HitTarget(target, spell.Power, origin, kind == MageSpell.Frost ? 1.05f : 0f);
            float radius = kind == MageSpell.FireOrb ? spell.Radius : 0.5f;
            if (vfx != null) vfx.Impact(kind, position, radius, spell.Color);
            else MageEffects.Burst(position, radius, trailMaterial, spell.Color);
        }

        private void HitTarget(TrainingTarget target, int damage, Vector3 origin, float stagger)
        {
            var hit = new CombatHit(damage, origin, parryable: false, arcane: true);
            var result = target.ReceiveHit(hit);
            player.PresentLandedImpact(hit, result, target.transform.position);
            if (result != HitResult.Miss && stagger > 0f && target.Health > 0) target.Stagger(stagger);
        }

        private void AreaHit(Vector3 center, Spell spell, float stagger)
        {
            var affected = new HashSet<TrainingTarget>();
            foreach (var collider in Physics.OverlapSphere(center, spell.Radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var target = collider.GetComponentInParent<TrainingTarget>();
                if (target == null || target.Health <= 0 || affected.Contains(target)) continue;
                Vector3 toTarget = target.transform.position + Vector3.up * 0.9f - center;
                bool blocked = false;
                foreach (var obstruction in Physics.RaycastAll(center, toTarget.normalized, toTarget.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    if (obstruction.collider.GetComponentInParent<TrainingTarget>() == null) { blocked = true; break; }
                if (blocked) continue;
                affected.Add(target);
                HitTarget(target, spell.Power, center, stagger);
            }
        }

        public int Absorb(int damage) => AbsorbFor(damage, null);

        internal bool SanctuaryAt(Vector3 position)
        {
            if (sanctuary == null || Time.time >= sanctuaryUntil) return false;
            Vector3 offset = position - sanctuary.transform.position;
            return Mathf.Abs(offset.y) <= 1.2f && offset.x * offset.x + offset.z * offset.z <= SanctuaryRadius * SanctuaryRadius;
        }

        internal int ShieldPointsFor(PracticeAlly ally) => barrierAlly == ally ? ActiveBarrierPoints : 0;

        internal int AbsorbFor(int damage, PracticeAlly ally)
        {
            if (!isActiveAndEnabled) return damage;
            if (SanctuaryAt(ally != null ? ally.transform.position : transform.position) && damage > 1)
            {
                damage = (damage + 1) / 2;
                Tell(ally != null ? "ALLIÉ · COUP ATTÉNUÉ" : "SANCTUAIRE · COUP ATTÉNUÉ");
            }
            int absorbed = Mathf.Min(damage, ShieldPointsFor(ally));
            if (absorbed <= 0) return damage;
            barrierPoints -= absorbed;
            Tell((ally != null ? "ALLIÉ · " : isHealer ? "ÉGIDE · " : "BARRIÈRE · ") + absorbed + " ABSORBÉS");
            if (barrierPoints <= 0) RemoveBarrier();
            return damage - absorbed;
        }

        public void Interrupt()
        {
            if (CurrentSpell == MageSpell.Revive && !released)
                Refund(MageSpell.Revive, Spells[(int)MageSpell.Revive], "RELÈVEMENT INTERROMPU");
            CurrentSpell = MageSpell.None;
            castTarget = null;
            castAlly = null;
            age = 0f;
        }
        private void Tell(string text) { message = text; messageUntil = Time.time + 0.85f; }
        private void Refund(MageSpell kind, Spell spell, string reason)
        {
            Mana = Mathf.Min(MaxMana, Mana + spell.Cost);
            readyAt[(int)kind] = 0f;
            Tell(reason);
        }
        private void RemoveBarrier()
        {
            barrierPoints = 0;
            barrierAlly = null;
            if (barrier != null) Destroy(barrier);
            barrier = null;
        }

        private void Heal(PracticeAlly ally, Spell spell, bool periodic)
        {
            int restored = ally != null ? ally.Heal(spell.Power) : player.Heal(spell.Power);
            vfx?.Healing(ally != null ? ally.transform : transform, spell.Color, periodic);
            if (restored > 0) Tell("+" + restored + " VIE" + (ally != null ? " · ALLIÉ" : ""));
        }

        private void RemoveSanctuary()
        {
            if (sanctuary != null)
            {
                sanctuary.SetActive(false);
                Destroy(sanctuary);
            }
            sanctuary = null;
        }

        public void ResetSpells()
        {
            ClearEffects();
            Mana = MaxMana;
            lastSpend = -10f;
            messageUntil = 0f;
            System.Array.Clear(readyAt, 0, readyAt.Length);
        }

        private void OnDisable() { ClearEffects(); }
        internal void Forget(MageProjectile projectile) { projectiles.Remove(projectile); }
        private void ClearEffects()
        {
            Interrupt();
            RemoveBarrier();
            RemoveSanctuary();
            allySelected = false;
            renewRemaining = 0;
            renewAlly = null;
            foreach (var projectile in projectiles)
            {
                if (projectile == null) continue;
                projectile.gameObject.SetActive(false);
                Destroy(projectile.gameObject);
            }
            projectiles.Clear();
        }
    }
}
