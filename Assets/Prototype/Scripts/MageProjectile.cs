using UnityEngine;
using UnityEngine.Rendering;

namespace MMO.Prototype
{
    public sealed class MageProjectile : MonoBehaviour
    {
        private MageCombat owner;
        private MageSpell kind;
        private Vector3 direction, origin;
        private Vector3 focusOffset;
        private Transform visual;
        private float visualAge;
        private float speed, remaining, radius;

        public void Launch(MageCombat caster, MageSpell spell, Vector3 start, Vector3 focus, Vector3 heading, float velocity, float range,
            float size, Color color, Material material, Material trailMaterial, MageVfx vfx = null)
        {
            owner = caster; kind = spell; origin = start; direction = heading; speed = velocity; remaining = range; radius = size;
            transform.position = start;
            gameObject.layer = 2;
            visual = new GameObject("Projectile Visual").transform;
            visual.SetParent(transform, false);
            focusOffset = focus - start;
            visual.localPosition = focusOffset;
            if (vfx != null)
            {
                vfx.Projectile(visual, spell, color, size);
                return;
            }
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.layer = 2;
            var collider = sphere.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            sphere.transform.SetParent(visual, false);
            sphere.transform.localScale = Vector3.one * (size * 2f);
            var renderer = sphere.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_Color", color);
            renderer.SetPropertyBlock(tint);

            var trail = visual.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailMaterial;
            trail.time = 0.17f;
            trail.startWidth = size * 1.8f;
            trail.endWidth = 0f;
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            trail.shadowCastingMode = ShadowCastingMode.Off;
        }

        private void Update()
        {
            float distance = Mathf.Min(remaining, speed * Time.deltaTime);
            if (distance <= 0f) return;
            if (Physics.SphereCast(transform.position, radius, direction, out RaycastHit hit, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                Vector3 contact = transform.position + direction * Mathf.Max(0f, hit.distance - 0.035f);
                Resolve(contact, hit.collider.GetComponentInParent<TrainingTarget>());
                return;
            }
            transform.position += direction * distance;
            // Start at the animated staff, then meet the collision ray in front of the caster.
            visualAge += Time.deltaTime;
            visual.localPosition = Vector3.Lerp(focusOffset, Vector3.zero, Mathf.Clamp01(visualAge / 0.09f));
            remaining -= distance;
            if (remaining <= 0.001f) Resolve(transform.position, null);
        }

        private void Resolve(Vector3 position, TrainingTarget target)
        {
            enabled = false;
            if (owner != null && owner.isActiveAndEnabled) owner.Impact(kind, position, target, origin);
            Destroy(gameObject);
        }

        private void OnDestroy() { if (owner != null) owner.Forget(this); }
    }

    internal static class MageEffects
    {
        public static LineRenderer Ring(Transform parent, float radius, Quaternion rotation, Material material, Color color)
        {
            var ring = new GameObject("Magic Ring").AddComponent<LineRenderer>();
            ring.transform.SetParent(parent, false);
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 48;
            ring.widthMultiplier = 0.055f;
            ring.sharedMaterial = material;
            ring.startColor = ring.endColor = color;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * (2f * Mathf.PI / ring.positionCount);
                ring.SetPosition(i, rotation * new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
            return ring;
        }

        public static void Burst(Vector3 position, float radius, Material material, Color color)
        {
            var effect = new GameObject("Magic Impact");
            effect.transform.position = position;
            var pulse = effect.AddComponent<MagePulse>();
            pulse.line = Ring(effect.transform, radius, Quaternion.identity, material, color);
            pulse.color = color;
            effect.transform.localScale = Vector3.one * 0.12f;
        }
    }

    public sealed class MagePulse : MonoBehaviour
    {
        internal LineRenderer line;
        internal Color color;
        private float age;

        private void Update()
        {
            age += Time.deltaTime;
            float phase = Mathf.Clamp01(age / 0.42f);
            transform.localScale = Vector3.one * Mathf.Lerp(0.12f, 1f, Mathf.Sqrt(phase));
            color.a = 1f - phase;
            line.startColor = line.endColor = color;
            if (phase >= 1f) Destroy(gameObject);
        }
    }
}
