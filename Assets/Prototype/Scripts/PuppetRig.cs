using UnityEngine;

namespace MMO.Prototype
{
    public sealed class PuppetRig : MonoBehaviour
    {
        public Transform torso, leftArm, rightArm, leftElbow, rightElbow;
        public Transform leftLeg, rightLeg, leftKnee, rightKnee, weapon;
        public TrailRenderer swordTrail;
        private static readonly Vector3 ReadyBlade = new Vector3(0.15f, -0.56f, 0.72f);
        private float gait, gaitWeight, groundWeight;
        private float landingAge = 1f, landingDepth;
        private bool wasGrounded = true;
        private Transform[] joints;
        private readonly Quaternion[] fromRotations = new Quaternion[9];
        private readonly Quaternion[] targetRotations = new Quaternion[9];
        private Quaternion fromBlade;
        private Vector3 fromOffset;
        private float blendAge, blendDuration;
        private int poseState = -1;
        private PuppetFootwork footwork;

        private void Awake() { BindWeapon(); }
        public void BindWeapon()
        {
            if (weapon == null && rightElbow != null) weapon = rightElbow.Find("Right Hand/Sword");
        }
        public void Pose(float speed, bool grounded, float vertical, float attack, float dodge, float dt)
        {
            PoseWarrior(speed, grounded, vertical, attack >= 0f ? WarriorMove.Light1 : WarriorMove.None, Mathf.Max(0f, attack) * 0.62f,
                0.26f, 0.62f, -1f, false, dodge, false, dt);
        }

        public void PoseWarrior(float speed, bool grounded, float vertical, WarriorMove move, float time, float impact, float duration,
            float charge, bool guarding, float dodge, bool reacting, float dt, Vector3 groundVelocity = default)
        {
            if (joints == null)
            {
                BindWeapon();
                joints = new[] { torso, leftArm, rightArm, leftElbow, rightElbow, leftLeg, rightLeg, leftKnee, rightKnee };
                footwork = new PuppetFootwork(transform.parent, leftLeg, rightLeg, leftKnee, rightKnee);
            }
            float run = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 1f, speed));
            float cycleLength = Mathf.Lerp(1.45f, 2.55f, run);
            float support = Mathf.Lerp(0.64f, 0.37f, run);
            float travelSpeed = Vector3.ProjectOnPlane(groundVelocity, Vector3.up).magnitude;
            if (grounded) gait = Mathf.Repeat(gait + travelSpeed * dt / cycleLength, 1f);
            gaitWeight = Mathf.Lerp(gaitWeight, grounded ? Mathf.Clamp01(travelSpeed / 2f) : 0f, 1f - Mathf.Exp(-12f * dt));
            float phase = gait * 2f * Mathf.PI;
            float stride = Mathf.Sin(phase) * gaitWeight;
            Vector3 armL = new Vector3(stride * 33f, 0f, -8f), armR = new Vector3(-stride * 33f - 8f, 0f, 8f);
            Vector3 torsoRotation = new Vector3(speed * 9f + Mathf.Sin(Time.time * 2.8f) * 1.2f, -stride * 6f, stride * 3.5f);
            Vector3 legL = new Vector3(-stride * 37f, 0f, -2f), legR = new Vector3(stride * 37f, 0f, 2f);
            float kneeL = Mathf.Max(0f, -stride) * 55f, kneeR = Mathf.Max(0f, stride) * 55f;
            float elbowL = -17f - Mathf.Max(0f, stride) * 22f, elbowR = -23f + stride * 10f;
            float drop = -Mathf.Lerp(0.025f, 0.155f, gaitWeight);
            float weightShift = -Mathf.Cos(phase) * gaitWeight * 0.035f;
            float forwardShift = speed * 0.035f;
            float stance = 0f;
            Vector3 blade = ReadyBlade;

            if (grounded && !wasGrounded)
            {
                landingAge = 0f;
                landingDepth = Mathf.Clamp(-vertical * 0.017f, 0.035f, 0.14f);
            }
            landingAge += dt;
            wasGrounded = grounded;
            if (grounded && landingAge < 0.24f)
                drop -= Mathf.Sin(landingAge / 0.24f * Mathf.PI) * landingDepth;

            if (!grounded)
            {
                legL.x = -22f; legR.x = 18f; kneeL = 50f; kneeR = 30f;
                armL.x = -30f; armR.x = -25f; torsoRotation.x = Mathf.Clamp(vertical * -1.2f, -9f, 12f);
            }
            if (guarding)
            {
                armR = new Vector3(-65f, -20f, -35f); armL = new Vector3(-52f, 15f, 30f);
                elbowR = -85f; elbowL = -70f; torsoRotation = new Vector3(3f, -12f, 0f);
                legL.x = -12f; legR.x = 12f; blade = new Vector3(-0.7f, 0.9f, 0.25f);
                stance = 0.19f;
                drop -= 0.055f;
            }
            if (charge >= 0f)
            {
                armR = new Vector3(-155f, 0f, 15f); elbowR = -40f;
                armL = new Vector3(-110f, 10f, -25f); elbowL = -75f;
                torsoRotation = new Vector3(-8f - charge * 5f, -15f, 0f);
                kneeL = kneeR = 20f; blade = new Vector3(0f, 0.8f, -0.55f);
                stance = 0.19f;
                forwardShift -= 0.065f;
                drop -= 0.04f + charge * 0.035f;
            }
            bool activeTrail = false;
            if (move != WarriorMove.None)
            {
                float halfSwing = Mathf.Clamp(impact, 0.055f, 0.12f);
                float strike = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(impact - halfSwing, impact + halfSwing, time));
                float recovery = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(impact + 0.12f, Mathf.Max(impact + 0.15f, duration), time));
                Vector3 begin = new Vector3(-55f, -60f, -45f), end = new Vector3(-65f, 65f, 45f);
                Vector3 bladeStart = new Vector3(0.9f, 0.35f, 0.2f), bladeEnd = new Vector3(-0.9f, 0.15f, 0.3f);
                float bodyStart = -24f, bodyEnd = 28f, lean = 6f;
                switch (move)
                {
                    case WarriorMove.Light2:
                        begin = new Vector3(-65f, 60f, 45f); end = new Vector3(-50f, -60f, -40f);
                        bladeStart = new Vector3(-1f, 0.2f, 0.2f); bladeEnd = new Vector3(1f, 0.4f, 0.3f);
                        bodyStart = 25f; bodyEnd = -25f; break;
                    case WarriorMove.Light3:
                    case WarriorMove.Heavy:
                        begin = new Vector3(-155f, -12f, 10f); end = new Vector3(-55f, 15f, -12f);
                        bladeStart = new Vector3(0.1f, 1f, -0.3f); bladeEnd = new Vector3(-0.25f, -0.75f, 0.8f);
                        bodyStart = -12f; bodyEnd = 14f; lean = 15f;
                        armL = new Vector3(-75f, 10f, -20f); elbowL = -65f;
                        if (move == WarriorMove.Heavy) { kneeL = 30f; kneeR = 25f; drop -= 0.08f * strike; lean = 23f; }
                        break;
                    case WarriorMove.Thrust:
                    case WarriorMove.Riposte:
                    case WarriorMove.Lunge:
                        begin = new Vector3(-25f, -20f, 10f); end = new Vector3(-95f, 0f, -5f);
                        bladeStart = bladeEnd = Vector3.forward;
                        bodyStart = -25f; bodyEnd = 15f; lean = move == WarriorMove.Lunge ? 26f : 12f;
                        elbowR = Mathf.Lerp(-95f, -5f, strike);
                        armL = new Vector3(20f, 0f, -20f);
                        if (move == WarriorMove.Lunge) { legL.x = -45f; legR.x = 35f; kneeL = 30f; kneeR = 55f; }
                        break;
                    case WarriorMove.Sweep:
                        strike = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(impact - 0.1f, impact + 0.1f, time));
                        begin = new Vector3(-85f, -80f, -50f); end = new Vector3(-85f, 85f, 55f);
                        bladeStart = bladeEnd = Quaternion.Euler(0f, WarriorAttack.SweepYaw(time - impact), 0f) * Vector3.forward;
                        bodyStart = -45f; bodyEnd = 45f; lean = 3f;
                        legL.z = -10f; legR.z = 10f; break;
                    case WarriorMove.GuardBreaker:
                        begin = new Vector3(-35f, 0f, 5f); end = new Vector3(-85f, 0f, 5f);
                        bladeStart = bladeEnd = Vector3.up;
                        elbowR = Mathf.Lerp(-100f, -45f, strike); bodyStart = -12f; bodyEnd = 12f; lean = 14f; break;
                    case WarriorMove.Retreat:
                        begin = new Vector3(-60f, -35f, -25f); end = new Vector3(-70f, 40f, 25f);
                        bladeStart = new Vector3(0.8f, 0.2f, 0.4f); bladeEnd = new Vector3(-0.8f, 0.3f, 0.4f);
                        bodyStart = -12f; bodyEnd = 15f; lean = -12f;
                        legL.x = 25f; legR.x = -30f; break;
                    case WarriorMove.Plunge:
                        strike = grounded ? Mathf.Lerp(0.86f, 1f, Mathf.Clamp01(time / 0.06f)) :
                            0.86f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-1f, -6.5f, vertical));
                        recovery = grounded ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, duration, time)) : 0f;
                        begin = new Vector3(-160f, 0f, 5f); end = new Vector3(-60f, 0f, -5f);
                        bladeStart = Vector3.up; bladeEnd = new Vector3(0, -1f, 0.25f);
                        bodyStart = bodyEnd = 0f; lean = 24f; kneeL = kneeR = 45f;
                        if (grounded) drop -= 0.14f * (1f - recovery);
                        break;
                }
                armR = Vector3.Lerp(Vector3.Lerp(begin, end, strike), new Vector3(-8f, 0f, 8f), recovery);
                if (move != WarriorMove.Thrust && move != WarriorMove.Lunge && move != WarriorMove.Riposte && move != WarriorMove.GuardBreaker)
                    elbowR = Mathf.Lerp(-48f, -12f, strike);
                blade = Vector3.Slerp(BladeArc(bladeStart, bladeEnd, strike), ReadyBlade, recovery);
                torsoRotation = new Vector3(Mathf.Lerp(-lean * 0.28f, lean, strike), Mathf.Lerp(bodyStart, bodyEnd, strike), 0f) * (1f - recovery);
                float followThrough = 1f - recovery;
                float push = Mathf.Lerp(-0.05f, move == WarriorMove.Retreat ? -0.10f : 0.10f, strike);
                forwardShift += push * followThrough;
                weightShift += Mathf.Lerp(0.04f, -0.045f, strike) * followThrough;
                drop -= 0.045f * followThrough;
                stance = (move == WarriorMove.Light2 ? -0.20f : 0.24f) * followThrough;
                if (move != WarriorMove.Lunge && move != WarriorMove.Retreat && move != WarriorMove.Plunge)
                { legL.x = -10f; legR.x = 10f; kneeL = kneeR = 12f; }
                activeTrail = time >= Mathf.Max(0f, impact - 0.1f) && time <= impact + 0.12f;
                if (move == WarriorMove.Plunge) activeTrail &= grounded;
            }
            if (dodge >= 0f)
            {
                float push = Mathf.Sin(dodge * Mathf.PI);
                drop -= 0.11f * push;
                torsoRotation = new Vector3(32f * push, 0f, -8f * push);
                armL = new Vector3(35f * push, 0f, -15f); armR = new Vector3(22f * push, 0f, 15f);
                legL = new Vector3(-32f * push, 0f, -4f); legR = new Vector3(28f * push, 0f, 4f);
                kneeL = 46f * push; kneeR = 57f * push;
                forwardShift += 0.10f * push;
            }
            if (reacting) { torsoRotation.x -= 12f; armL.z -= 12f; }
            int state = (int)move | (grounded ? 16 : 0) | (guarding ? 32 : 0) |
                (charge >= 0f ? 64 : 0) | (dodge >= 0f ? 128 : 0) | (reacting ? 256 : 0);
            if (state != poseState)
            {
                poseState = state;
                for (int i = 0; i < joints.Length; i++) fromRotations[i] = joints[i].localRotation;
                fromOffset = transform.localPosition;
                if (weapon != null) fromBlade = Quaternion.Inverse(transform.parent.rotation) * weapon.rotation;
                blendAge = 0f;
                blendDuration = move != WarriorMove.None && impact > 0f ? Mathf.Clamp(impact * 0.58f, 0.055f, 0.30f) : 0.16f;
                if (dodge >= 0f) blendDuration = 0.085f;
            }
            blendAge += dt;
            float blend = dt <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(blendAge / blendDuration));
            float bob = grounded ? (1f - Mathf.Cos(phase * 2f)) * 0.015f * gaitWeight : 0f;
            transform.localPosition = Vector3.Lerp(fromOffset, new Vector3(weightShift, drop + bob, forwardShift), blend);
            targetRotations[0] = Quaternion.Euler(torsoRotation);
            targetRotations[1] = Quaternion.Euler(armL);
            targetRotations[2] = Quaternion.Euler(armR);
            targetRotations[3] = Quaternion.Euler(elbowL, 0f, 0f);
            targetRotations[4] = Quaternion.Euler(elbowR, 0f, 0f);
            targetRotations[5] = Quaternion.Euler(legL);
            targetRotations[6] = Quaternion.Euler(legR);
            targetRotations[7] = Quaternion.Euler(kneeL, 0f, 0f);
            targetRotations[8] = Quaternion.Euler(kneeR, 0f, 0f);
            for (int i = 0; i < joints.Length; i++) joints[i].localRotation = Quaternion.Slerp(fromRotations[i], targetRotations[i], blend);
            if (weapon != null && blade.sqrMagnitude > 0.001f)
            {
                Quaternion targetBlade = Quaternion.FromToRotation(Vector3.forward, blade.normalized);
                weapon.rotation = transform.parent.rotation * Quaternion.Slerp(fromBlade, targetBlade, blend);
            }
            groundWeight = Mathf.MoveTowards(groundWeight, grounded ? 1f : 0f, dt * 11f);
            footwork.Apply(gait, gaitWeight, cycleLength, support, run, groundVelocity, stance, groundWeight, dt);
            if (swordTrail != null) swordTrail.emitting = activeTrail;
        }

        private static Vector3 BladeArc(Vector3 from, Vector3 to, float progress)
        {
            if (Vector3.Dot(from.normalized, to.normalized) > 0.92f)
                return Vector3.Slerp(from, to, progress);
            // Pass in front of the body, including when the two endpoints are opposite.
            return progress < 0.5f
                ? Vector3.Slerp(from, Vector3.forward, progress * 2f)
                : Vector3.Slerp(Vector3.forward, to, (progress - 0.5f) * 2f);
        }
    }
}

