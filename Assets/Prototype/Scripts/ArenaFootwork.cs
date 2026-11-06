using UnityEngine;

namespace MMO.Prototype
{
    [DefaultExecutionOrder(-60)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class ArenaFootwork : MonoBehaviour
    {
        public float speed = 2.45f;
        public Vector2 xLimits = new Vector2(-9.2f, 9.2f);
        public Vector2 zLimits = new Vector2(-6.2f, 14.2f);

        private CharacterController body;
        private Vector3 home, velocity;
        private Quaternion homeRotation;
        private Vector3 motion;
        private float fallSpeed;

        public bool IsMoving => motion.sqrMagnitude > 0.04f;
        public float PoseSpeed => motion.magnitude / 6.2f;
        public Vector3 LocalVelocity => transform.InverseTransformDirection(motion);

        private void Awake()
        {
            body = GetComponent<CharacterController>();
            home = transform.position;
            homeRotation = transform.rotation;
        }

        public void MoveTo(Vector3 destination, float dt)
        {
            if (!body.enabled || dt <= 0f) return;
            destination.x = Mathf.Clamp(destination.x, xLimits.x, xLimits.y);
            destination.z = Mathf.Clamp(destination.z, zLimits.x, zLimits.y);
            Vector3 from = transform.position;
            Vector3 offset = Vector3.ProjectOnPlane(destination - from, Vector3.up);
            float distance = offset.magnitude;
            Vector3 desired = distance > 0.08f
                ? Steer(offset / distance) * Mathf.Min(speed, distance * 5f)
                : Vector3.zero;
            velocity = Vector3.MoveTowards(velocity, desired, 14f * dt);
            fallSpeed = body.isGrounded ? -1.4f : Mathf.Max(-12f, fallSpeed - 22f * dt);
            body.Move((velocity + Vector3.up * fallSpeed) * dt);
            motion = Vector3.ProjectOnPlane(transform.position - from, Vector3.up) / dt;
        }

        private Vector3 Steer(Vector3 direction)
        {
            const float probe = 0.85f;
            if (Clearance(direction, probe) >= probe) return direction;
            Vector3 left = Quaternion.AngleAxis(-62f, Vector3.up) * direction;
            Vector3 right = Quaternion.AngleAxis(62f, Vector3.up) * direction;
            float roomLeft = Clearance(left, probe);
            float roomRight = Clearance(right, probe);
            if (Mathf.Max(roomLeft, roomRight) < 0.15f) return Vector3.zero;
            return roomLeft >= roomRight ? left : right;
        }

        private float Clearance(Vector3 direction, float distance)
        {
            return Physics.SphereCast(transform.position + Vector3.up * 0.64f, body.radius * 0.78f, direction,
                out RaycastHit hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.distance : distance;
        }

        public void Stop()
        {
            velocity = motion = Vector3.zero;
            fallSpeed = 0f;
        }

        public void ResetPosition()
        {
            bool enabledBeforeReset = body.enabled;
            body.enabled = false;
            transform.SetPositionAndRotation(home, homeRotation);
            body.enabled = enabledBeforeReset;
            Stop();
        }
    }
}
