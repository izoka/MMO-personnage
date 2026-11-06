using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MMO.Prototype
{
    [DefaultExecutionOrder(-30)]
    public sealed class TargetLock : MonoBehaviour
    {
        public Camera view;
        public OrbitCamera orbit;
        public Material markerMaterial;

        private const float AcquireRange = 16f;
        private const float LoseRange = 18f;
        private const float GazeRadius = 0.14f;
        private const float AcquireDwell = 0.18f;
        private static readonly Vector2 GazeCenter = new Vector2(0.5f, 0.64f);
        private static readonly Color AimColor = new Color(0.91f, 0.95f, 1f, 0.65f);
        private static readonly Color LockColor = new Color(1f, 0.76f, 0.28f);
        private readonly List<Candidate> candidates = new List<Candidate>();
        private TrainingTarget locked, gaze;
        private LineRenderer ring;
        private float blockedFor, refreshAt, gazeSince;

        public TrainingTarget Locked => IsAlive(locked) ? locked : null;
        private Transform Player => orbit != null ? orbit.target : null;

        private struct Candidate
        {
            public TrainingTarget Target;
            public Vector3 Viewport;
            public float Score;
        }

        private void Awake()
        {
            ring = new GameObject("Target Ring").AddComponent<LineRenderer>();
            ring.transform.SetParent(transform, false);
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 48;
            ring.sharedMaterial = markerMaterial;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.enabled = false;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.68f);
            }
        }

        private void Update()
        {
            if (Player == null) return;
            if (locked != null)
            {
                if (!IsAlive(locked) || (locked.transform.position - Player.position).sqrMagnitude > LoseRange * LoseRange)
                    Clear();
                else
                {
                    blockedFor = ClearLine(Player.position + Vector3.up * 1.05f, locked) ? 0f : blockedFor + Time.deltaTime;
                    if (blockedFor >= 0.3f) Clear();
                }
            }
            if (Input.GetKeyDown(KeyCode.V))
            {
                CycleTarget();
                return;
            }
            if (Locked != null || Time.time < refreshAt) return;

            var seen = Gather();
            refreshAt = Time.time + 0.075f;
            if (seen != gaze)
            {
                gaze = seen;
                gazeSince = Time.time;
            }
            if (seen != null && Time.time - gazeSince >= AcquireDwell) Select(seen);
        }

        public bool CycleTarget()
        {
            var seen = Gather();
            if (candidates.Count == 0) return false;
            var current = Locked;
            if (current == null)
            {
                if (seen != null) return Select(seen);
                candidates.Sort((a, b) => a.Score.CompareTo(b.Score));
                return Select(candidates[0].Target);
            }
            candidates.Sort((a, b) =>
            {
                int order = a.Viewport.x.CompareTo(b.Viewport.x);
                return order != 0 ? order : a.Viewport.z.CompareTo(b.Viewport.z);
            });
            int index = candidates.FindIndex(entry => entry.Target == current);
            return Select(candidates[(index + 1) % candidates.Count].Target);
        }

        public TrainingTarget ForPlayer(Transform player) => Player == player ? Locked : null;

        public void Clear()
        {
            locked = null;
            gaze = null;
            blockedFor = 0f;
            refreshAt = 0f;
        }

        private bool Select(TrainingTarget target)
        {
            if (!IsAlive(target)) return false;
            locked = target;
            gaze = null;
            blockedFor = 0f;
            return true;
        }

        private TrainingTarget Gather()
        {
            candidates.Clear();
            if (view == null || Player == null) return null;
            TrainingTarget nearestToGaze = null;
            float best = float.PositiveInfinity;
            foreach (var target in FindObjectsByType<TrainingTarget>(FindObjectsSortMode.None))
            {
                if (!IsAlive(target)) continue;
                float distance = Vector3.Distance(Player.position, target.transform.position);
                if (distance > AcquireRange) continue;
                Vector3 point = view.WorldToViewportPoint(target.AimPoint);
                if (point.z <= view.nearClipPlane || point.x < 0f || point.x > 1f || point.y < 0f || point.y > 1f) continue;
                if (!ClearLine(Player.position + Vector3.up * 1.05f, target) || !ClearLine(view.transform.position, target)) continue;

                Vector2 fromCenter = new Vector2((point.x - GazeCenter.x) * view.aspect, point.y - GazeCenter.y);
                float score = fromCenter.sqrMagnitude + distance * 0.00065f;
                candidates.Add(new Candidate { Target = target, Viewport = point, Score = score });
                if (fromCenter.sqrMagnitude <= GazeRadius * GazeRadius && score < best)
                {
                    best = score;
                    nearestToGaze = target;
                }
            }
            return nearestToGaze;
        }

        private static bool IsAlive(TrainingTarget target) => target != null && target.isActiveAndEnabled && target.Health > 0;

        private bool ClearLine(Vector3 from, TrainingTarget target)
        {
            Vector3 offset = target.AimPoint - from;
            if (!Physics.Raycast(from, offset.normalized, out RaycastHit first, offset.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return true;
            if (first.collider.GetComponentInParent<TrainingTarget>() == target) return true;
            if (!first.collider.transform.IsChildOf(Player)) return false;
            foreach (var hit in Physics.RaycastAll(from, offset.normalized, offset.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(Player) && hit.collider.GetComponentInParent<TrainingTarget>() != target)
                    return false;
            return true;
        }

        private void LateUpdate()
        {
            var target = Locked;
            bool confirmed = target != null;
            if (!confirmed) target = gaze;
            ring.enabled = IsAlive(target);
            if (!ring.enabled) return;
            ring.transform.SetPositionAndRotation(target.transform.position + Vector3.up * 0.035f, Quaternion.identity);
            ring.widthMultiplier = confirmed ? 0.055f : 0.025f;
            ring.startColor = ring.endColor = confirmed ? LockColor : AimColor;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || Player == null || view == null) return;
            Vector2 point = GazeCenter;
            var target = Locked;
            if (target != null)
            {
                Vector3 lockedPoint = view.WorldToViewportPoint(target.AimPoint + Vector3.up * 0.55f);
                if (lockedPoint.z <= view.nearClipPlane || lockedPoint.x < 0f || lockedPoint.x > 1f || lockedPoint.y < 0f || lockedPoint.y > 1f)
                    return;
                point = lockedPoint;
            }
            Rect area = view.pixelRect;
            Vector2 center = new Vector2(area.x + point.x * area.width, Screen.height - area.y - point.y * area.height);
            Color old = GUI.color;
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            GUI.color = target != null ? LockColor : AimColor;
            float gap = target != null ? 11f : 3f;
            GUI.DrawTexture(new Rect(center.x - gap - 4f, center.y - 1f, 4f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(center.x + gap, center.y - 1f, 4f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(center.x - 1f, center.y - gap - 4f, 2f, 4f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(center.x - 1f, center.y + gap, 2f, 4f), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.matrix = oldMatrix;
        }
    }
}
