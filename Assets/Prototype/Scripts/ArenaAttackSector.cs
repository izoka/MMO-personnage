using UnityEngine;
using UnityEngine.Rendering;

namespace MMO.Prototype
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ArenaAttackSector : MonoBehaviour
    {
        public Material material;
        public const float Radius = 3.4f;
        private const float HalfAngle = 54f;
        private const int ArcSegments = 36;
        private static readonly float MinDot = Mathf.Cos(HalfAngle * Mathf.Deg2Rad);
        private static readonly int Tint = Shader.PropertyToID("_Color");
        private static readonly Color EdgeColor = new Color(1f, 0.34f, 0.24f, 0.96f);
        private Mesh mesh;
        private MeshRenderer fill;
        private LineRenderer edge;
        private MaterialPropertyBlock properties;
        private Vector3 forward;
        private float flashUntil;

        public Vector3 Origin { get; private set; }
        public bool Warning { get; private set; }

        private void Awake()
        {
            fill = GetComponent<MeshRenderer>();
            fill.sharedMaterial = material;
            fill.shadowCastingMode = ShadowCastingMode.Off;
            fill.receiveShadows = false;
            properties = new MaterialPropertyBlock();
            var outline = new GameObject("Contour du balayage");
            outline.transform.SetParent(transform, false);
            edge = outline.AddComponent<LineRenderer>();
            edge.useWorldSpace = false;
            edge.loop = true;
            edge.sharedMaterial = material;
            edge.widthMultiplier = 0.065f;
            edge.startColor = edge.endColor = EdgeColor;
            edge.shadowCastingMode = ShadowCastingMode.Off;

            var points = new Vector3[ArcSegments + 2];
            var colors = new Color32[points.Length];
            var triangles = new int[ArcSegments * 3];
            colors[0] = Color.white;
            for (int i = 0; i <= ArcSegments; i++)
            {
                float radians = Mathf.Lerp(-HalfAngle, HalfAngle, i / (float)ArcSegments) * Mathf.Deg2Rad;
                points[i + 1] = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians)) * Radius;
                colors[i + 1] = Color.white;
                if (i == ArcSegments) continue;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
            mesh = new Mesh { name = "Balayage · secteur" };
            mesh.vertices = points;
            mesh.colors32 = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            edge.positionCount = points.Length;
            edge.SetPositions(points);
            Hide();
        }

        public void Announce(Vector3 origin, Vector3 direction)
        {
            Origin = origin;
            forward = direction;
            transform.SetPositionAndRotation(origin + Vector3.up * 0.043f, Quaternion.LookRotation(direction));
            flashUntil = 0f;
            Warning = true;
            fill.enabled = edge.enabled = true;
            SetTint(new Color(0.96f, 0.23f, 0.13f, 0.37f));
        }

        public void SetProgress(float progress)
        {
            SetTint(new Color(0.96f, 0.23f, 0.13f, Mathf.Lerp(0.35f, 0.56f, Mathf.Clamp01(progress))));
        }

        public bool CanHit(Vector3 feet)
        {
            Vector3 offset = feet - Origin;
            if (Mathf.Abs(offset.y) > 0.9f) return false;
            offset.y = 0f;
            if (offset.sqrMagnitude > Radius * Radius) return false;
            if (offset.sqrMagnitude > 0.001f && Vector3.Dot(forward, offset.normalized) < MinDot) return false;
            return !Physics.Linecast(Origin + Vector3.up, feet + Vector3.up, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        public void FlashImpact()
        {
            Warning = false;
            flashUntil = Time.time + 0.16f;
            SetTint(new Color(1f, 0.44f, 0.17f, 0.69f));
        }

        public void Hide()
        {
            Warning = false;
            flashUntil = 0f;
            if (fill != null) fill.enabled = edge.enabled = false;
        }

        private void SetTint(Color color)
        {
            properties.SetColor(Tint, color);
            fill.SetPropertyBlock(properties);
        }

        private void LateUpdate()
        {
            if (flashUntil > 0f && Time.time >= flashUntil) Hide();
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
