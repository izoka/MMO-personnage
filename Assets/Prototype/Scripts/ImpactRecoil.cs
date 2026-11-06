using UnityEngine;

namespace MMO.Prototype
{
    // A visual spring in world space so a turn during recovery doesn't redirect a hit.
    internal struct ImpactRecoil
    {
        private const float Response = 19f;
        private Vector3 offset, velocity;

        public static float Strength(CombatHit hit, HitResult result)
        {
            if (result == HitResult.Parried) return 0.20f;
            if (result == HitResult.Blocked) return 0.30f;
            if (result == HitResult.GuardBroken) return 1f;
            return Mathf.Lerp(0.36f, 1f, Mathf.InverseLerp(10f, 60f, hit.damage));
        }

        public static Vector3 AwayFrom(Transform body, Vector3 origin)
        {
            Vector3 away = Vector3.ProjectOnPlane(body.position - origin, Vector3.up);
            return away.sqrMagnitude > 0.001f ? away.normalized : -body.forward;
        }

        public void Kick(Vector3 away, float strength)
        {
            velocity = Vector3.ClampMagnitude(velocity + away * (52f * strength), 52f);
        }

        public Vector3 Advance(float dt)
        {
            if (offset.sqrMagnitude < 0.000001f && velocity.sqrMagnitude < 0.0001f)
            {
                this = default;
                return Vector3.zero;
            }

            // Exact critically damped step. It keeps the same response at different frame rates.
            Vector3 decayTerm = velocity + Response * offset;
            float decay = Mathf.Exp(-Response * dt);
            offset = (offset + decayTerm * dt) * decay;
            velocity = (velocity - Response * decayTerm * dt) * decay;
            return Vector3.ClampMagnitude(offset, 1f);
        }
    }
}
