using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MMO.Prototype
{
    public sealed class RangerBolt : MonoBehaviour
    {
        private RangerCombat owner;
        private Action<RaycastHit> onHit;
        private Collider ignoredCollider;
        private int hitLayers;
        private Vector3 direction, muzzleOffset;
        private Transform visual;
        private LineRenderer shaft;
        private TrailRenderer trail;
        private float speed, remaining, age;

        internal void Launch(RangerCombat shooter, Vector3 origin, Vector3 muzzle, Vector3 heading, float velocity,
            float range, Color tint, Material material, Action<TrainingTarget, Vector3> impact)
        {
            owner = shooter;
            Launch(origin, muzzle, heading, velocity, range, tint, material, Physics.DefaultRaycastLayers, null,
                hit => impact(hit.collider.GetComponentInParent<TrainingTarget>(), hit.point));
        }

        internal void Launch(Vector3 origin, Vector3 muzzle, Vector3 heading, float velocity, float range,
            Color tint, Material material, int layers, Collider ignored, Action<RaycastHit> impact)
        {
            onHit = impact;
            ignoredCollider = ignored;
            hitLayers = layers;
            direction = heading;
            speed = velocity;
            remaining = range;
            muzzleOffset = muzzle - origin;
            gameObject.layer = 2;
            transform.SetPositionAndRotation(origin, Quaternion.LookRotation(heading));

            visual = new GameObject("Carreau visuel").transform;
            visual.SetParent(transform, false);
            visual.position = muzzle;
            shaft = visual.gameObject.AddComponent<LineRenderer>();
            shaft.useWorldSpace = false;
            shaft.positionCount = 2;
            shaft.SetPosition(0, Vector3.zero);
            shaft.SetPosition(1, Vector3.back * 0.40f);
            shaft.startWidth = 0.032f;
            shaft.endWidth = 0.018f;
            shaft.startColor = shaft.endColor = tint;
            shaft.sharedMaterial = material;
            shaft.shadowCastingMode = ShadowCastingMode.Off;

            var trailObject = new GameObject("Traînée");
            trailObject.transform.SetParent(visual, false);
            trail = trailObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = 0.11f;
            trail.minVertexDistance = 0.055f;
            trail.startWidth = 0.043f;
            trail.endWidth = 0f;
            trail.startColor = tint;
            trail.endColor = new Color(tint.r, tint.g, tint.b, 0f);
            trail.shadowCastingMode = ShadowCastingMode.Off;
        }

        private void Update()
        {
            float distance = Mathf.Min(remaining, speed * Time.deltaTime);
            if (FindContact(distance, out RaycastHit hit))
            {
                transform.position += direction * hit.distance;
                visual.localPosition = Vector3.zero;
                onHit(hit);
                Finish();
                return;
            }

            transform.position += direction * distance;
            remaining -= distance;
            age += Time.deltaTime;
            visual.position = transform.position + Vector3.Lerp(muzzleOffset, Vector3.zero, Mathf.Clamp01(age / 0.075f));
            if (remaining <= 0.001f) Finish();
        }

        private bool FindContact(float distance, out RaycastHit hit)
        {
            if (!Physics.SphereCast(transform.position, 0.075f, direction, out hit, distance, hitLayers, QueryTriggerInteraction.Ignore))
                return false;
            if (hit.collider != ignoredCollider) return true;

            float nearest = float.PositiveInfinity;
            foreach (var contact in Physics.SphereCastAll(transform.position, 0.075f, direction, distance, hitLayers, QueryTriggerInteraction.Ignore))
            {
                if (contact.collider == ignoredCollider || contact.distance >= nearest) continue;
                nearest = contact.distance;
                hit = contact;
            }
            return nearest < float.PositiveInfinity;
        }

        private void Finish()
        {
            shaft.enabled = false;
            trail.emitting = false;
            enabled = false;
            Destroy(gameObject, trail.time);
        }

        private void OnDestroy() { if (owner != null) owner.Forget(this); }
    }
}
