using UnityEngine;

namespace MMO.Prototype
{
    public sealed class OrbitCamera : MonoBehaviour
    {
        private const float DefaultDistance = 5.2f;
        public Transform target;
        public TargetLock targeting;
        public float distance = DefaultDistance;
        public float sensitivity = 2.6f;
        [SerializeField, Range(0f, 0.09f)] private float impactAmplitude;
        private float yaw;
        private float pitch = 23f;
        private float shownYaw, shownPitch, shownDistance;
        private Vector3 pivot;
        private ImpactRecoil impact;
        private Transform impactOwner;
        private float nextImpact;

        private void Start() { ResetView(); }

        public void ResetView()
        {
            impact = default;
            nextImpact = 0f;
            yaw = target != null ? target.eulerAngles.y : 0f;
            pitch = 23f;
            distance = DefaultDistance;
            shownYaw = yaw;
            shownPitch = pitch;
            shownDistance = distance;
            if (target != null) pivot = target.position + Vector3.up * 1.2f;
        }

        internal void ReactToImpact(Transform owner, Vector3 direction, CombatHit hit, HitResult result)
        {
            if (impactAmplitude <= 0f || target != owner || !isActiveAndEnabled) return;
            bool parry = result == HitResult.Parried;
            float weight = parry ? 0.48f : ImpactRecoil.Strength(hit, result);
            if (!parry && ((result != HitResult.Hit && result != HitResult.GuardBroken) || weight < 0.70f ||
                (impactOwner == owner && Time.time < nextImpact))) return;

            if (impactOwner != owner) impact = default;
            impactOwner = owner;
            nextImpact = Time.time + 0.22f;
            Vector3 planar = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            Vector3 impulse = planar * 0.78f + Vector3.up * (parry ? 0.63f : -0.63f);
            impact.Kick(impulse.normalized, weight);
        }

        private void LateUpdate()
        {
            if (target == null) return;
            bool orbiting = Input.GetMouseButton(1) && Application.isFocused;
            Cursor.lockState = orbiting ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !orbiting;
            if (orbiting)
            {
                yaw += Input.GetAxisRaw("Mouse X") * sensitivity;
                pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * sensitivity, 8f, 65f);
            }
            Vector3 desiredPivot = target.position + Vector3.up * 1.2f;
            var locked = targeting != null ? targeting.ForPlayer(target) : null;
            if (!orbiting && locked != null)
            {
                Vector3 toward = Vector3.ProjectOnPlane(locked.AimPoint - target.position, Vector3.up);
                if (toward.sqrMagnitude > 0.04f)
                    yaw = Mathf.LerpAngle(yaw, Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg, 1f - Mathf.Exp(-8f * Time.deltaTime));
                desiredPivot += Vector3.ClampMagnitude(toward, 2.4f) * 0.25f;
            }
            distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y * 0.55f, 2.5f, 8f);
            pivot = Vector3.Lerp(pivot, desiredPivot, 1f - Mathf.Exp(-15f * Time.deltaTime));
            float orbitBlend = 1f - Mathf.Exp(-25f * Time.deltaTime);
            shownYaw = Mathf.LerpAngle(shownYaw, yaw, orbitBlend);
            shownPitch = Mathf.Lerp(shownPitch, pitch, orbitBlend);
            Quaternion rotation = Quaternion.Euler(shownPitch, shownYaw, 0f);
            Vector3 backward = rotation * Vector3.back;
            float length = distance;
            if (Physics.SphereCast(pivot, 0.2f, backward, out RaycastHit hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                length = Mathf.Max(0.35f, hit.distance - 0.1f);
            shownDistance = Mathf.Lerp(shownDistance, length, 1f - Mathf.Exp(-14f * Time.deltaTime));
            if (length < distance) shownDistance = Mathf.Min(shownDistance, length);
            if (impactOwner != target) impact = default;
            // A retracted camera has less room around it and magnifies even small motion.
            float clearance = Mathf.InverseLerp(0.35f, 1.4f, shownDistance) * Mathf.Min(1f, shownDistance / DefaultDistance);
            Vector3 offset = impact.Advance(Time.deltaTime) * (impactAmplitude * clearance);
            transform.SetPositionAndRotation(pivot + backward * shownDistance + offset, rotation);
        }

        private void OnDisable()
        {
            impact = default;
            nextImpact = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        private void OnApplicationFocus(bool focused) { if (!focused) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } }
    }
}
