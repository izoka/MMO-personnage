using UnityEngine;
using UnityEngine.Rendering;

namespace MMO.Prototype
{
    // The imported textures drive the particles; gameplay hit volumes live in MageCombat.
    public sealed class MageVfx : MonoBehaviour
    {
        public Material glow, flame, smoke, sigil, sparks, star, ring, swirl;
        private static readonly Color Ember = new Color(1f, 0.53f, 0.14f);

        public void Cast(Transform focus, MageSpell spell, Color tint, float windup)
        {
            if (focus == null) return;
            var effect = Container("Cast sparks", focus.position, focus);
            var flash = Layer(effect, spell == MageSpell.FireOrb ? flame : glow, tint, 0.42f, windup + 0.14f, 0f, false);
            Emit(flash, 1);
            Emit(Layer(effect, star, tint, 0.20f, 0.32f, 0.68f, false), 6);
            Destroy(effect.gameObject, windup + 0.23f);
        }

        public void Projectile(Transform visual, MageSpell spell, Color tint, float size)
        {
            bool fire = spell == MageSpell.FireOrb;
            bool frost = spell == MageSpell.Frost;
            Material core = fire ? flame : frost || spell == MageSpell.Light ? star : sigil;
            var body = Layer(visual, core, tint, size * (fire ? 4.9f : 4.0f), 4f, 0f, false, false);
            Emit(body, 1);
            Emit(Layer(visual, glow, Color.Lerp(tint, Color.white, 0.4f), size * 2.9f, 4f, 0f, false, false), 1);
            var tail = Layer(visual, fire ? flame : star, fire ? Ember : tint, fire ? 0.40f : 0.22f, 0.31f, 1.15f, true);
            Stream(tail, fire ? 32f : 26f);
            if (frost) Stream(Layer(visual, sparks, tint, 0.29f, 0.23f, 0.6f, true), 13f);
        }

        public void Impact(MageSpell spell, Vector3 position, float radius, Color tint)
        {
            var effect = Container("Spell impact", position);
            switch (spell)
            {
                case MageSpell.FireOrb:
                    Emit(Layer(effect, glow, new Color(1f, 0.66f, 0.24f), 1.75f, 0.21f, 0f, false), 1);
                    Emit(Layer(effect, flame, tint, 0.86f, 0.5f, 3.6f, true), 17);
                    Emit(Layer(effect, star, Ember, 0.25f, 0.66f, 4.4f, true), 22);
                    Emit(Layer(effect, smoke, new Color(0.40f, 0.30f, 0.32f, 0.5f), 1.1f, 0.77f, 1.35f, true), 10);
                    GroundBurst(position, radius, tint, ring, 0.48f);
                    break;
                case MageSpell.Frost:
                    Emit(Layer(effect, glow, tint, 0.95f, 0.2f, 0f, false), 1);
                    Emit(Layer(effect, star, tint, 0.44f, 0.54f, 3.4f, true), 16);
                    Emit(Layer(effect, sparks, new Color(0.62f, 0.92f, 1f), 0.53f, 0.38f, 1.5f, true), 9);
                    break;
                case MageSpell.Light:
                    Emit(Layer(effect, glow, tint, 0.78f, 0.23f, 0f, false), 1);
                    Emit(Layer(effect, star, tint, 0.22f, 0.38f, 2.3f, true), 10);
                    break;
                default:
                    Emit(Layer(effect, sigil, tint, 0.92f, 0.31f, 0f, false), 1);
                    Emit(Layer(effect, star, tint, 0.27f, 0.43f, 2.3f, true), 12);
                    break;
            }
            Destroy(effect.gameObject, 0.9f);
        }

        public void Nova(Vector3 position, float radius, Color tint)
        {
            GroundBurst(position, radius, tint, sigil, 0.5f);
            GroundBurst(position, radius, Color.Lerp(tint, Color.white, 0.35f), swirl, 0.62f);
            var burst = Container("Arcane sparks", position + Vector3.up * 0.32f);
            Emit(Layer(burst, star, tint, 0.30f, 0.52f, 4.2f, true), 28);
            Destroy(burst.gameObject, 0.65f);
        }

        public void Shield(Transform shield, Material outlineMaterial, Color tint, float duration)
        {
            MageEffects.Ring(shield, 0.91f, Quaternion.Euler(72f, 0f, 27f), outlineMaterial, new Color(tint.r, tint.g, tint.b, 0.6f));
            MageEffects.Ring(shield, 0.91f, Quaternion.Euler(0f, 0f, -18f), outlineMaterial, new Color(tint.r, tint.g, tint.b, 0.45f));
            var motes = Layer(shield, star, tint, 0.30f, 0.88f, 0.23f, false);
            var shape = motes.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.8f;
            shape.radiusThickness = 0.12f;
            Stream(motes, 20f);
            var disk = Layer(shield, ring, new Color(tint.r, tint.g, tint.b, 0.72f), 2.30f, duration + 0.1f, 0f, false, false);
            disk.transform.localPosition = Vector3.down * 0.78f;
            disk.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            Emit(disk, 1);
        }

        public void Healing(Transform caster, Color tint, bool periodic)
        {
            var effect = Container("Healing motes", caster.position + Vector3.up * 0.45f, caster);
            var motes = Layer(effect, star, tint, periodic ? 0.24f : 0.35f, 0.68f, 0.38f, false);
            var shape = motes.shape;
            shape.radius = 0.63f;
            shape.radiusThickness = 0.35f;
            var lift = motes.velocityOverLifetime;
            lift.enabled = true;
            lift.y = 1.5f;
            Emit(motes, periodic ? 9 : 24);
            if (!periodic)
            {
                Emit(Layer(effect, glow, tint, 1.3f, 0.38f, 0f, false), 1);
                GroundBurst(caster.position + Vector3.up * 0.09f, 1.25f, tint, ring, 0.52f);
            }
            Destroy(effect.gameObject, 0.78f);
        }

        public void Purify(Transform recipient, Color tint)
        {
            var effect = Container("Purification", recipient.position + Vector3.up, recipient);
            Emit(Layer(effect, glow, tint, 1.32f, 0.36f, 0f, false), 1);
            Emit(Layer(effect, star, tint, 0.23f, 0.42f, 2.15f, false), 20);
            GroundBurst(recipient.position + Vector3.up * 0.08f, 1.06f, tint, ring, 0.43f);
            Destroy(effect.gameObject, 0.52f);
        }

        public void Revive(Transform recipient, Color tint)
        {
            var effect = Container("Relèvement", recipient.position + Vector3.up * 0.53f, recipient);
            Emit(Layer(effect, glow, tint, 1.68f, 0.55f, 0f, false), 1);
            var motes = Layer(effect, star, tint, 0.34f, 0.82f, 0.56f, false);
            var lift = motes.velocityOverLifetime;
            lift.enabled = true;
            lift.y = 2.1f;
            Emit(motes, 25);
            GroundBurst(recipient.position + Vector3.up * 0.08f, 1.32f, tint, sigil, 0.74f);
            Destroy(effect.gameObject, 0.88f);
        }

        public void Sanctuary(Transform zone, float radius, Color tint, float duration, Material outline)
        {
            MageEffects.Ring(zone, radius, Quaternion.identity, outline, new Color(tint.r, tint.g, tint.b, 0.82f));
            var disk = Layer(zone, sigil, new Color(tint.r, tint.g, tint.b, 0.39f), radius * 2.35f, duration, 0f, false, false);
            var main = disk.main;
            main.startRotation = 0f;
            disk.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            Emit(disk, 1);

            var motes = Layer(zone, star, tint, 0.24f, 0.93f, 0f, false);
            var shape = motes.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.rotation = new Vector3(90f, 0f, 0f);
            shape.radius = radius;
            shape.radiusThickness = 0.06f;
            var lift = motes.velocityOverLifetime;
            lift.enabled = true;
            lift.y = 0.78f;
            Stream(motes, 19f);
        }

        private void GroundBurst(Vector3 position, float radius, Color tint, Material material, float lifetime)
        {
            if (Physics.Raycast(position + Vector3.up * 0.2f, Vector3.down, out RaycastHit ground, 3f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && ground.collider.GetComponentInParent<TrainingTarget>() == null)
                position.y = ground.point.y + 0.045f;
            var effect = Container("Magic wave", position);
            var disk = Layer(effect, material, tint, radius * 2.65f, lifetime, 0f, false);
            var renderer = disk.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            var size = disk.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.22f, 1f, 1f));
            Emit(disk, 1);
            Destroy(effect.gameObject, lifetime + 0.07f);
        }

        private static Transform Container(string name, Vector3 position, Transform parent = null)
        {
            var effect = new GameObject(name).transform;
            if (parent != null) effect.SetParent(parent, false);
            effect.position = position;
            return effect;
        }

        internal static ParticleSystem Layer(Transform parent, Material material, Color tint, float size, float life, float speed,
            bool world, bool fade = true)
        {
            var layer = new GameObject(material.name).AddComponent<ParticleSystem>();
            layer.transform.SetParent(parent, false);
            layer.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = layer.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.maxParticles = 96;
            main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.startColor = tint;
            main.startSize = size;
            main.startLifetime = life;
            main.startSpeed = speed > 0f ? new ParticleSystem.MinMaxCurve(speed * 0.45f, speed) : new ParticleSystem.MinMaxCurve(0f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            var emission = layer.emission;
            emission.enabled = false;
            var shape = layer.shape;
            shape.enabled = speed > 0f;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.075f;
            if (fade)
            {
                var color = layer.colorOverLifetime;
                color.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.72f, 0.35f), new GradientAlphaKey(0f, 1f) });
                color.color = gradient;
            }
            var renderer = layer.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return layer;
        }

        private static void Emit(ParticleSystem system, int count)
        {
            system.Play();
            system.Emit(count);
        }

        private static void Stream(ParticleSystem system, float rate)
        {
            var main = system.main;
            main.loop = true;
            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;
            system.Play();
        }
    }
}
