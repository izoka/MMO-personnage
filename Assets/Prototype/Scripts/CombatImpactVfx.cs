using UnityEngine;

namespace MMO.Prototype
{
    public sealed class CombatImpactVfx : MonoBehaviour
    {
        public Material glowMaterial, sparkMaterial, starMaterial, ringMaterial;

        private static CombatImpactVfx instance;
        private ParticleSystem glow, sparks, star, ring;
        private uint sequence;

        private void Awake()
        {
            glow = CreateLayer(glowMaterial);
            sparks = CreateLayer(sparkMaterial);
            star = CreateLayer(starMaterial);
            ring = CreateLayer(ringMaterial);
            if (sparks != null)
            {
                var main = sparks.main;
                main.gravityModifier = 0.38f;
                var renderer = sparks.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 1.25f;
                renderer.velocityScale = 0.10f;
            }
            if (ring != null)
            {
                var size = ring.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.45f, 1f, 1f));
            }
        }

        private void OnEnable() { instance = this; }
        private void OnDisable() { if (instance == this) instance = null; }

        private ParticleSystem CreateLayer(Material material)
        {
            if (material == null) return null;
            return MageVfx.Layer(transform, material, Color.white, 1f, 0.2f, 0f, true);
        }

        internal static void Present(Collider surface, Vector3 center, CombatHit hit, HitResult result, bool shielded = false)
        {
            if (result == HitResult.Miss || !Application.isPlaying) return;
            Vector3 outward = Vector3.ProjectOnPlane(hit.origin - center, Vector3.up).normalized;
            if (outward == Vector3.zero) outward = surface != null ? surface.transform.forward : Vector3.forward;
            Vector3 point = hit.contact ?? SurfaceContact(surface, center, outward, hit.bladeTip);
            ImpactAudio.Play(point, hit, result);
            // Spells already carry their own contact effect.
            if (instance != null && (!hit.arcane || shielded))
                instance.Burst(point + outward * 0.055f, outward, ImpactRecoil.Strength(hit, result), result, shielded);
        }

        private static Vector3 SurfaceContact(Collider surface, Vector3 center, Vector3 outward, Transform bladeTip)
        {
            if (surface == null || !surface.enabled) return center + outward * 0.35f;
            if (bladeTip != null && bladeTip.parent != null)
            {
                Vector3 hilt = bladeTip.parent.position;
                Vector3 blade = bladeTip.position - hilt;
                if (blade.sqrMagnitude > 0.0001f)
                {
                    Vector3 nearSide = surface.ClosestPoint(center + outward * 2f);
                    float alongBlade = Mathf.Clamp01(Vector3.Dot(nearSide - hilt, blade) / blade.sqrMagnitude);
                    Bounds bounds = surface.bounds;
                    center.y = Mathf.Clamp(hilt.y + blade.y * alongBlade,
                        bounds.min.y + bounds.size.y * 0.18f, bounds.max.y - bounds.size.y * 0.12f);
                }
            }
            return surface.ClosestPoint(center + outward * 2f);
        }

        private void Burst(Vector3 point, Vector3 outward, float strength, HitResult result, bool shielded)
        {
            bool parried = result == HitResult.Parried;
            bool blocked = result == HitResult.Blocked;
            bool broken = result == HitResult.GuardBroken;
            Color tint = shielded ? new Color(0.33f, 0.68f, 1f) :
                parried ? new Color(0.48f, 0.88f, 1f) :
                broken ? new Color(1f, 0.43f, 0.14f) :
                blocked ? new Color(1f, 0.71f, 0.27f) : new Color(1f, 0.85f, 0.53f);
            float scale = parried ? 1.12f : shielded ? 0.84f : Mathf.Lerp(0.58f, 1.16f, strength);
            Emit(glow, point, Vector3.zero, tint, 0.46f * scale, 0.12f, 0f);
            Emit(star, point, Vector3.zero, Color.Lerp(tint, Color.white, 0.52f), 0.40f * scale, 0.085f, 16f);
            if (shielded || parried || broken)
                Emit(ring, point + outward * 0.12f, Vector3.zero, tint, (parried ? 1.14f : 0.87f) * scale, 0.19f, 0f);

            Vector3 tangent = Vector3.Cross(Vector3.up, outward);
            int count = shielded ? 4 : parried ? 12 : blocked ? 7 : Mathf.RoundToInt(Mathf.Lerp(4f, 13f, strength));
            for (int i = 0; i < count; i++)
            {
                // Evenly space rays in the tangent plane, with variation from one impact to the next.
                float phase = (i * 2.39996f + sequence * 0.37f) % (2f * Mathf.PI);
                float spread = 0.55f + (i % 3) * 0.19f;
                Vector3 velocity = (outward * 0.55f + (tangent * Mathf.Cos(phase) + Vector3.up * Mathf.Sin(phase)) * spread)
                    * Mathf.Lerp(2.2f, 4.0f, scale - 0.4f);
                Emit(sparks, point, velocity, tint, (0.065f + (i % 3) * 0.015f) * scale, 0.14f + (i % 4) * 0.018f, 0f);
            }
            sequence = (sequence + 1) % 64;
        }

        private static void Emit(ParticleSystem layer, Vector3 position, Vector3 velocity, Color tint, float size, float life, float rotation)
        {
            if (layer == null) return;
            if (!layer.isPlaying) layer.Play();
            layer.Emit(new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startColor = tint,
                startSize = size,
                startLifetime = life,
                rotation = rotation
            }, 1);
        }
    }
}
