using UnityEngine;

namespace MMO.Prototype
{
    public sealed class TrainingTarget : MonoBehaviour
    {
        public Transform visual;
        public TextMesh healthText, damageText;
        public int maxHealth = 100;
        public bool respawnAutomatically = true;
        public string displayName = "CIBLE";
        public int Health { get; private set; }
        internal bool CanTakeDamage { get; set; } = true;
        public int TotalHits { get; private set; }
        public bool IsGuarding { get; private set; }
        public bool IsStunned => Time.time < stunnedUntil;
        private float lastHitTime = -10f, respawnTime, stunnedUntil;
        private float fallAngle, fallVelocity;
        private Vector3 fallAxis = Vector3.right;
        private ImpactRecoil recoil;
        private KayKitCharacterVisual character;
        private Renderer[] bodyRenderers;
        private Collider targetCollider;
        public Vector3 AimPoint => targetCollider != null && targetCollider.enabled
            ? targetCollider.bounds.center : transform.position + Vector3.up;
        private MaterialPropertyBlock block;
        private SparringOpponent opponent;
        private SparringArbalester arbalester;
        private ArenaFootwork footwork;
        private bool initialized;

        private void Awake() { Initialize(); ResetTarget(); }
        private void Initialize()
        {
            if (initialized) return;
            bodyRenderers = visual.GetComponentsInChildren<Renderer>();
            targetCollider = GetComponent<Collider>();
            block = new MaterialPropertyBlock();
            opponent = GetComponent<SparringOpponent>();
            arbalester = GetComponent<SparringArbalester>();
            if (opponent != null || arbalester != null) character = visual.GetComponentInChildren<KayKitCharacterVisual>();
            footwork = GetComponent<ArenaFootwork>();
            initialized = true;
        }
        public void SetGuard(bool held) { IsGuarding = held && !IsStunned && Health > 0; }
        public void Stagger(float duration) { stunnedUntil = Time.time + duration; IsGuarding = false; }
        public bool ReceiveHit(int damage) => ReceiveHit(new CombatHit(damage, transform.position - transform.forward)) != HitResult.Miss;

        public HitResult ReceiveHit(CombatHit hit)
        {
            if (!CanTakeDamage || Health <= 0 || hit.damage <= 0) return HitResult.Miss;
            HitResult result = HitResult.Hit;
            Vector3 direction = Vector3.ProjectOnPlane(hit.origin - transform.position, Vector3.up);
            bool frontal = direction.sqrMagnitude < 0.001f || Vector3.Angle(transform.forward, direction) <= 60f;
            int damage = hit.damage;
            if (IsGuarding && frontal && hit.parryable)
            {
                if (hit.guardBreaker) { Stagger(0.8f); result = HitResult.GuardBroken; }
                else { damage = Mathf.CeilToInt(damage * 0.3f); result = HitResult.Blocked; }
            }
            else if (hit.guardBreaker) Stagger(0.25f);
            Health = Mathf.Max(0, Health - damage);
            TotalHits++;
            lastHitTime = Time.time;
            Vector3 away = ImpactRecoil.AwayFrom(transform, hit.origin);
            if (character != null) character.ReactToImpact(transform, hit, result);
            else recoil.Kick(away, ImpactRecoil.Strength(hit, result));
            damageText.text = result == HitResult.GuardBroken ? "GARDE BRISÉE" : result == HitResult.Blocked ? "BLOC −" + damage : "−" + damage;
            CombatImpactVfx.Present(targetCollider, AimPoint, hit, result);
            if (Health == 0)
            {
                fallAxis = Vector3.Cross(Vector3.up, transform.InverseTransformDirection(away));
                respawnTime = Time.time + 1.5f;
                targetCollider.enabled = false;
                IsGuarding = false;
            }
            return result;
        }

        public void ResetTarget()
        {
            Initialize();
            Health = maxHealth; TotalHits = 0; lastHitTime = -10f; stunnedUntil = 0f; IsGuarding = false;
            fallAngle = fallVelocity = 0f;
            recoil = default;
            character?.ResetImpact();
            footwork?.ResetPosition();
            targetCollider.enabled = true;
            visual.localRotation = Quaternion.identity; visual.localScale = Vector3.one;
            damageText.text = "";
            if (opponent != null) opponent.ResetCycle();
            if (arbalester != null) arbalester.ResetCycle();
        }

        private void Update()
        {
            if (Health == 0 && respawnAutomatically && Time.time >= respawnTime)
            {
                float previousAngle = fallAngle;
                ResetTarget();
                fallAngle = previousAngle;
            }
            float age = Time.time - lastHitTime;
            if (character == null)
            {
                Vector3 tilt = Vector3.Cross(Vector3.up, transform.InverseTransformDirection(recoil.Advance(Time.deltaTime)));
                fallAngle = Mathf.SmoothDampAngle(fallAngle, Health > 0 ? 0f : 79f, ref fallVelocity, 0.14f);
                visual.localRotation = Quaternion.AngleAxis(fallAngle, fallAxis) *
                    Quaternion.AngleAxis(tilt.magnitude * 27f, tilt.normalized);
            }
            foreach (var renderer in bodyRenderers)
            {
                if (renderer.sharedMaterial == null || renderer is TrailRenderer) continue;
                renderer.GetPropertyBlock(block);
                float flash = 0.55f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.16f)));
                block.SetColor("_Color", Color.Lerp(renderer.sharedMaterial.color, Color.white, flash));
                renderer.SetPropertyBlock(block);
            }
            string state = opponent != null ? "\n" + opponent.StateLabel :
                arbalester != null ? "\n" + arbalester.StateLabel : "";
            healthText.text = Health > 0 ? displayName + "\n" + Health + " / " + maxHealth + state : "AU SOL";
            damageText.gameObject.SetActive(age < 0.65f);
            damageText.transform.localPosition = new Vector3(0f, 2.15f + Mathf.Clamp01(age / 0.65f) * 0.5f, 0f);
        }
        private void LateUpdate()
        {
            if (Camera.main == null) return;
            healthText.transform.rotation = Camera.main.transform.rotation;
            damageText.transform.rotation = Camera.main.transform.rotation;
        }
    }
}

