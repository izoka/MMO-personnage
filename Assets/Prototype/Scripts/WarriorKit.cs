using System;
using System.Collections.Generic;
using UnityEngine;

namespace MMO.Prototype
{
    public enum WarriorMove { None, Light1, Light2, Light3, Heavy, Thrust, Lunge, Sweep, GuardBreaker, Retreat, Riposte, Plunge }
    public enum HitResult { Miss, Hit, Blocked, Parried, GuardBroken }

    public struct CombatHit
    {
        public int damage;
        public Vector3 origin;
        public bool guardBreaker;
        public bool parryable;
        public bool arcane;
        public TrainingTarget source;
        public Vector3? contact;
        public Transform bladeTip;
        public CombatHit(int damage, Vector3 origin, bool guardBreaker = false, bool parryable = true, TrainingTarget source = null,
            bool arcane = false, Vector3? contact = null, Transform bladeTip = null)
        {
            this.damage = damage; this.origin = origin; this.guardBreaker = guardBreaker; this.parryable = parryable;
            this.source = source; this.arcane = arcane; this.contact = contact; this.bladeTip = bladeTip;
        }
    }

    public struct WarriorAttack
    {
        public WarriorMove move;
        public string name;
        public float impact, duration, damage, cost, cooldown, reach, angle, width, travel, travelDuration;
        public bool IsThrust => move == WarriorMove.Thrust || move == WarriorMove.Riposte || move == WarriorMove.Lunge;
        public bool IsLight => move >= WarriorMove.Light1 && move <= WarriorMove.Light3;

        public static float SweepYaw(float secondsFromImpact, float angle = 160f)
        {
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.1f, 0.1f, secondsFromImpact));
            return Mathf.Lerp(angle * 0.5f, -angle * 0.5f, progress);
        }
    }

    // Field names deliberately match the approved JSON; the build reads these values directly.
    public sealed class WarriorKit
    {
        [Serializable] public class Shape { public float reach_m, angle_degrees, width_m; }
        [Serializable] public class LightHit { public float damage_multiplier, impact_s, duration_s, stamina; }
        [Serializable] public class Skill
        {
            public string id, name;
            public LightHit[] hits;
            public Shape shape;
            public float damage_multiplier, damage_multiplier_min, damage_multiplier_max, charge_max_s;
            public float windup_s, windup_after_release_s, recovery_s, recovery_after_landing_s;
            public float stamina, stamina_min, stamina_max, cooldown_s, travel_m, travel_s, retreat_m, retreat_s;
            public float damage_reduction, frontal_angle_degrees, stamina_per_block_base, stamina_per_block_incoming_damage_multiplier;
            public float movement_speed_multiplier, window_s, rearm_delay_s, stamina_per_success, duration_s, speed_m_s;
        }
        [Serializable] public class Stamina { public float maximum, regeneration_per_second, regeneration_delay_after_spending_s; }
        [Serializable] public class Balance { public float damage_reference; public Stamina stamina; }
        [Serializable] public class Rules { public float input_buffer_s, combo_continue_window_after_recovery_s; }
        [Serializable] public class Data { public Balance balance; public Rules global_rules; public Skill[] skills; }

        private static WarriorKit instance;
        public static WarriorKit Instance => instance ?? (instance = new WarriorKit());
        public readonly Data data;
        private readonly Dictionary<string, Skill> skills = new Dictionary<string, Skill>();
        private WarriorKit()
        {
            var asset = Resources.Load<TextAsset>("WarriorKit");
            if (asset == null) throw new InvalidOperationException("WarriorKit.json is missing from Resources.");
            data = JsonUtility.FromJson<Data>(asset.text);
            foreach (var skill in data.skills) skills.Add(skill.id, skill);
            if (skills.Count != 12 || data.balance.stamina.maximum <= 0f) throw new InvalidOperationException("Invalid warrior kit.");
        }
        public Skill Get(string id) => skills[id];

        public WarriorAttack Attack(WarriorMove move, float charge = 0f)
        {
            string id;
            switch (move)
            {
                case WarriorMove.Light1: case WarriorMove.Light2: case WarriorMove.Light3: id = "light_combo"; break;
                case WarriorMove.Heavy: id = "charged_strike"; break;
                case WarriorMove.Thrust: id = "thrust"; break;
                case WarriorMove.Lunge: id = "advancing_lunge"; break;
                case WarriorMove.Sweep: id = "sweep"; break;
                case WarriorMove.GuardBreaker: id = "guard_breaker"; break;
                case WarriorMove.Retreat: id = "retreating_cut"; break;
                case WarriorMove.Riposte: id = "riposte"; break;
                case WarriorMove.Plunge: id = "descending_strike"; break;
                default: throw new ArgumentOutOfRangeException(nameof(move));
            }
            Skill s = Get(id);
            var attack = new WarriorAttack { move = move, name = s.name, damage = s.damage_multiplier * data.balance.damage_reference,
                cost = s.stamina, cooldown = s.cooldown_s, impact = s.windup_s, duration = s.windup_s + s.recovery_s,
                reach = s.shape.reach_m, angle = s.shape.angle_degrees, width = s.shape.width_m };
            if (attack.IsLight)
            {
                int stage = (int)move - (int)WarriorMove.Light1;
                var hit = s.hits[stage];
                attack.name = "Combo " + (stage + 1) + "/3";
                attack.damage = hit.damage_multiplier * data.balance.damage_reference;
                attack.cost = hit.stamina; attack.impact = hit.impact_s; attack.duration = hit.duration_s;
            }
            if (move == WarriorMove.Heavy)
            {
                attack.damage = Mathf.Lerp(s.damage_multiplier_min, s.damage_multiplier_max, charge) * data.balance.damage_reference;
                attack.cost = Mathf.Lerp(s.stamina_min, s.stamina_max, charge);
                attack.impact = s.windup_after_release_s; attack.duration = attack.impact + s.recovery_s;
            }
            if (move == WarriorMove.Lunge)
            { attack.impact = 0.1f; attack.duration = s.travel_s + s.recovery_s; attack.travel = s.travel_m; attack.travelDuration = s.travel_s; }
            if (move == WarriorMove.Retreat)
            { attack.duration = s.windup_s + s.retreat_s + s.recovery_s; attack.travel = -s.retreat_m; attack.travelDuration = s.retreat_s; }
            if (move == WarriorMove.Plunge)
            { attack.impact = 0f; attack.duration = s.recovery_after_landing_s; }
            return attack;
        }
    }
}
