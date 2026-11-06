using UnityEngine;

namespace MMO.Prototype
{
    // Distance-driven steps keep the contact phase tied to movement. Two-bone IK
    // bends each leg towards its foot target and keeps the sole on the surface.
    internal sealed class PuppetFootwork
    {
        private readonly Transform actor;
        private readonly Leg left;
        private readonly Leg right;

        public PuppetFootwork(Transform actor, Transform leftHip, Transform rightHip, Transform leftKnee, Transform rightKnee)
        {
            this.actor = actor;
            left = new Leg(leftHip, leftKnee);
            right = new Leg(rightHip, rightKnee);
        }

        public void Apply(float cycle, float gaitWeight, float cycleLength, float support, float run,
            Vector3 velocity, float stance, float contactWeight, float dt)
        {
            Vector3 direction = Vector3.ProjectOnPlane(velocity, Vector3.up);
            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : actor.forward;
            Step(left, -1f, cycle, direction, gaitWeight, cycleLength, support, run, stance, contactWeight, dt);
            Step(right, 1f, Mathf.Repeat(cycle + 0.5f, 1f), direction, gaitWeight, cycleLength, support, run, stance, contactWeight, dt);
        }

        private void Step(Leg leg, float side, float phase, Vector3 direction, float gaitWeight, float cycleLength,
            float support, float run, float stance, float contactWeight, float dt)
        {
            float halfStep = cycleLength * support * 0.5f;
            float swing = Mathf.InverseLerp(support, 1f, phase);
            float travel = phase <= support
                ? Mathf.Lerp(halfStep, -halfStep, phase / support)
                : Mathf.SmoothStep(-halfStep, halfStep, swing);
            float lift = Mathf.Sin(swing * Mathf.PI) * Mathf.Lerp(0.11f, 0.24f, run) * gaitWeight;
            float lead = leg.EaseStance(Mathf.Max(0f, -side * stance) * (1f - gaitWeight), dt);
            lift += Mathf.Min(0.045f, Mathf.Abs(leg.StanceVelocity) * 0.024f);
            Vector3 displacement = direction * travel * gaitWeight;
            float lateral = side * 0.17f + Vector3.Dot(displacement, actor.right);
            lateral = side * Mathf.Max(0.075f, lateral * side);
            Vector3 spot = actor.position + actor.right * lateral
                + actor.forward * (lead + Vector3.Dot(displacement, actor.forward));
            leg.Place(actor, spot, lift, -12f * Mathf.Sin(swing * Mathf.PI) * gaitWeight, contactWeight, dt);
        }

        private sealed class Leg
        {
            private readonly Transform hip, knee, foot;
            private readonly Vector3 footOffset;
            private readonly Quaternion footRotation;
            private readonly float thighLength, shinLength;
            private float groundY;
            private bool hasGroundSample;
            private float stagger, staggerVelocity;
            public float StanceVelocity => staggerVelocity;

            public Leg(Transform hip, Transform knee)
            {
                this.hip = hip;
                this.knee = knee;
                foot = knee.Find("Foot");
                if (foot == null) return;
                footOffset = foot.localPosition;
                footRotation = foot.localRotation;
                thighLength = knee.localPosition.magnitude;
                shinLength = -footOffset.y;
            }

            public float EaseStance(float goal, float dt)
            {
                if (dt > 0f) stagger = Mathf.SmoothDamp(stagger, goal, ref staggerVelocity, 0.08f, 3f, dt);
                return stagger;
            }

            public void Place(Transform actor, Vector3 spot, float lift, float toePitch, float weight, float dt)
            {
                if (foot == null) return;
                foot.SetLocalPositionAndRotation(footOffset, footRotation);
                if (weight <= 0f)
                {
                    hasGroundSample = false;
                    return;
                }

                Vector3 normal = Vector3.up;
                float surfaceY = spot.y;
                if (Physics.Raycast(spot + Vector3.up * 0.38f, Vector3.down, out RaycastHit ground, 0.72f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && ground.normal.y > 0.65f &&
                    !ground.transform.IsChildOf(actor) && ground.collider.GetComponentInParent<TrainingTarget>() == null)
                {
                    surfaceY = ground.point.y;
                    normal = ground.normal;
                }
                if (!hasGroundSample || Mathf.Abs(groundY - surfaceY) > 0.4f) groundY = surfaceY;
                groundY = Mathf.Lerp(groundY, surfaceY, 1f - Mathf.Exp(-28f * dt));
                hasGroundSample = true;

                Vector3 ankle = new Vector3(spot.x, groundY + 0.065f + lift, spot.z);
                Vector3 offset = ankle - hip.position;
                Vector3 axis = offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector3.down;
                float distance = Mathf.Clamp(offset.magnitude, 0.04f, thighLength + shinLength - 0.004f);
                float along = (thighLength * thighLength - shinLength * shinLength + distance * distance) / (2f * distance);
                float bend = Mathf.Sqrt(Mathf.Max(0f, thighLength * thighLength - along * along));
                Vector3 kneeDirection = Vector3.ProjectOnPlane(actor.forward, axis).normalized;
                Vector3 upper = axis * along + kneeDirection * bend;
                Vector3 lower = axis * distance - upper;
                hip.rotation = Quaternion.Slerp(hip.rotation, BoneRotation(upper, actor), weight);
                knee.rotation = Quaternion.Slerp(knee.rotation, BoneRotation(lower, actor), weight);

                Quaternion sole = Quaternion.LookRotation(Vector3.ProjectOnPlane(actor.forward, normal), normal) * Quaternion.Euler(toePitch, 0f, 0f);
                foot.rotation = Quaternion.Slerp(foot.rotation, sole, weight);
                Vector3 footCenter = knee.TransformPoint(new Vector3(0f, footOffset.y, 0f)) + foot.forward * footOffset.z;
                foot.position = Vector3.Lerp(foot.position, footCenter, weight);
            }

            private static Quaternion BoneRotation(Vector3 towardFoot, Transform actor)
            {
                Vector3 up = -towardFoot.normalized;
                Vector3 forward = Vector3.ProjectOnPlane(actor.forward, up);
                if (forward.sqrMagnitude < 0.001f) forward = Vector3.ProjectOnPlane(actor.right, up);
                return Quaternion.LookRotation(forward, up);
            }
        }
    }
}
