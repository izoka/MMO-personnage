using System.Collections.Generic;
using UnityEngine;

namespace MMO.Prototype
{
    public enum RangerShot { None, Quick, Precision, Pinning, Volley, Sunder }

    [RequireComponent(typeof(PuppetMotor))]
    public sealed class RangerCombat : MonoBehaviour
    {
        public AnimationClip shootClip, reloadClip;
        public Transform muzzle;
        public Material boltMaterial;

        private const float FollowThrough = 0.12f;
        private const float ReleaseFrame = 0.48f;
        private static readonly Color Gold = new Color(1f, 0.82f, 0.49f);

        private readonly struct Shot
        {
            public readonly string Name;
            public readonly int Damage;
            public readonly float Cost, Windup, Recovery, Cooldown, Range, Speed;
            public readonly Color Tint;

            public Shot(string name, int damage, float cost, float windup, float recovery, float cooldown,
                float range, float speed, Color tint)
            {
                Name = name;
                Damage = damage;
                Cost = cost;
                Windup = windup;
                Recovery = recovery;
                Cooldown = cooldown;
                Range = range;
                Speed = speed;
                Tint = tint;
            }
        }

        private static readonly Shot[] Shots =
        {
            default,
            new Shot("Carreau rapide", 14, 0f, 0.28f, 0.60f, 0f, 16f, 27f, Gold),
            new Shot("Tir de précision", 32, 16f, 0.64f, 0.68f, 4f, 23f, 35f, Color.white),
            new Shot("Carreau entravant", 18, 15f, 0.35f, 0.64f, 6f, 16f, 26f, new Color(0.48f, 1f, 0.66f)),
            new Shot("Volée en éventail", 19, 24f, 0.46f, 0.71f, 7f, 16f, 27f, Gold),
            new Shot("Carreau brise-garde", 25, 22f, 0.43f, 0.68f, 8f, 16f, 30f, new Color(1f, 0.55f, 0.32f))
        };

        private readonly float[] readyAt = new float[Shots.Length];
        private readonly HashSet<RangerBolt> bolts = new HashSet<RangerBolt>();
        private PuppetMotor player;
        private TrainingTarget aimTarget;
        private Vector3 facing;
        private float age;
        private bool released;

        public RangerShot CurrentShot { get; private set; }
        public bool IsShooting => CurrentShot != RangerShot.None;
        internal float ActionTimeRemaining => IsShooting
            ? Shots[(int)CurrentShot].Windup + Shots[(int)CurrentShot].Recovery - age : 0f;
        public bool HasShotAim => IsShooting && aimTarget != null;
        public Vector3 ShotFacing => facing;
        public float MovementScale => released ? 0.72f : 0.38f;
        private bool Reloading => IsShooting && age >= Shots[(int)CurrentShot].Windup + FollowThrough;
        public AnimationClip CurrentClip => !IsShooting ? null : Reloading ? reloadClip : shootClip;
        public int AnimationKey => Reloading ? 9 : 8;
        public string Status => !IsShooting ? "" : Reloading ? "RECHARGEMENT" : Shots[(int)CurrentShot].Name.ToUpperInvariant();

        public float AnimationProgress
        {
            get
            {
                if (!IsShooting) return 0f;
                Shot shot = Shots[(int)CurrentShot];
                if (Reloading)
                    return Mathf.Lerp(0.08f, 0.93f, Mathf.InverseLerp(shot.Windup + FollowThrough, shot.Windup + shot.Recovery, age));
                float contact = Mathf.InverseLerp(0.04f, 0.72f, ReleaseFrame);
                float phase = KayKitCharacterVisual.SampleThroughContact(age, shot.Windup, shot.Windup + FollowThrough, contact);
                return Mathf.Lerp(0.04f, 0.72f, phase);
            }
        }

        private void Awake() { player = GetComponent<PuppetMotor>(); }

        public float Cooldown(RangerShot shot) => Mathf.Max(0f, readyAt[(int)shot] - Time.time);

        public void ReadInput()
        {
            if (Input.GetMouseButtonDown(0)) player.RequestAttack();
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) RequestShot(RangerShot.Precision);
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) RequestShot(RangerShot.Pinning);
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) RequestShot(RangerShot.Volley);
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) RequestShot(RangerShot.Sunder);
        }

        public bool RequestShot(RangerShot kind)
        {
            if (kind <= RangerShot.None || (int)kind >= Shots.Length || !isActiveAndEnabled || IsShooting ||
                !player.Grounded || player.Health <= 0 || player.IsStunned || player.IsDodging) return false;
            if (Cooldown(kind) > 0f) { player.Notify("CARREAU EN RECHARGE"); return false; }

            Shot shot = Shots[(int)kind];
            if (!player.Spend(shot.Cost)) return false;
            aimTarget = player.LockedTarget;
            Vector3 aim = aimTarget != null ? Vector3.ProjectOnPlane(aimTarget.AimPoint - transform.position, Vector3.up) : Vector3.zero;
            facing = aim.sqrMagnitude > 0.01f ? aim.normalized : transform.forward;
            readyAt[(int)kind] = Time.time + shot.Cooldown;
            CurrentShot = kind;
            age = 0f;
            released = false;
            return true;
        }

        public void Tick(float dt)
        {
            if (!IsShooting) return;
            Shot shot = Shots[(int)CurrentShot];
            age += dt;
            if (!released && age >= shot.Windup)
            {
                released = true;
                Release(CurrentShot, shot);
            }
            if (age >= shot.Windup + shot.Recovery) Interrupt();
        }

        private void Release(RangerShot kind, Shot shot)
        {
            Vector3 origin = transform.position + Vector3.up * 1.02f;
            Vector3 focus = muzzle != null ? muzzle.position : origin;
            Vector3 aim = aimTarget != null && aimTarget.Health > 0 ? aimTarget.AimPoint - origin : Vector3.zero;
            Vector3 heading = aim.sqrMagnitude > 0.01f ? aim.normalized : facing;
            var struck = kind == RangerShot.Volley ? new HashSet<TrainingTarget>() : null;

            void Hit(TrainingTarget target, Vector3 point)
            {
                if (target == null || (struck != null && !struck.Add(target))) return;
                var hit = new CombatHit(shot.Damage, origin, guardBreaker: kind == RangerShot.Sunder, contact: point);
                var result = target.ReceiveHit(hit);
                player.PresentLandedImpact(hit, result, point);
                if (result == HitResult.Hit && kind == RangerShot.Pinning && target.Health > 0)
                    target.Stagger(1.2f);
            }

            void Fire(Vector3 direction)
            {
                var bolt = new GameObject("Carreau").AddComponent<RangerBolt>();
                bolts.Add(bolt);
                bolt.Launch(this, origin, focus, direction, shot.Speed, shot.Range, shot.Tint, boltMaterial, Hit);
            }

            Fire(heading);
            if (kind == RangerShot.Volley)
            {
                Fire(Quaternion.AngleAxis(-5.5f, Vector3.up) * heading);
                Fire(Quaternion.AngleAxis(5.5f, Vector3.up) * heading);
            }
        }

        public void Interrupt()
        {
            CurrentShot = RangerShot.None;
            aimTarget = null;
        }

        public void ResetShots()
        {
            ClearBolts();
            System.Array.Clear(readyAt, 0, readyAt.Length);
        }

        private void OnDisable() { ClearBolts(); }

        internal void Forget(RangerBolt bolt) => bolts.Remove(bolt);

        private void ClearBolts()
        {
            Interrupt();
            foreach (var bolt in bolts)
            {
                if (bolt == null) continue;
                bolt.gameObject.SetActive(false);
                Destroy(bolt.gameObject);
            }
            bolts.Clear();
        }
    }
}
