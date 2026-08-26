using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelRacer
{
    /// <summary>Keeps a chunky two-colour sunset disc on the visible horizon.</summary>
    [ExecuteAlways]
    public sealed class VoxelHorizonSun : MonoBehaviour
    {
        private const string DiscMeshName = "Voxel Horizon Sun Disc";

        public Transform target;
        [Min(10f)] public float distanceAhead = 220f;
        public float horizontalOffset = 12f;
        public float horizonHeight = -80f;

        public void Build()
        {
            // Preserve the original large sunset presentation used by the showcase scene.
            transform.localScale = Vector3.one * 10f;

            if (HasCurrentDiscGeometry())
                return;

            for (int index = transform.childCount - 1; index >= 0; index--)
            {
                GameObject oldDisc = transform.GetChild(index).gameObject;
                oldDisc.SetActive(false);
                if (Application.isPlaying)
                    Destroy(oldDisc);
                else
                    DestroyImmediate(oldDisc);
            }

            var rim = CreateDisc("Sun Outer Glow", 28f, new Color(1f, 0.18f, 0.12f));
            rim.transform.SetParent(transform, false);

            var core = CreateDisc("Sun Core", 21f, new Color(1f, 0.68f, 0.12f));
            core.transform.SetParent(transform, false);
            core.transform.localPosition = Vector3.back * 0.4f;
        }

        private void Update()
        {
            if (target == null)
                return;

            Vector3 planarForward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
            Vector3 planarRight = Vector3.Cross(Vector3.up, planarForward).normalized;
            transform.position = target.position + planarForward * distanceAhead + planarRight * horizontalOffset + Vector3.up * horizonHeight;
            transform.rotation = Quaternion.LookRotation(planarForward, Vector3.up);
        }

        private static GameObject CreateDisc(string objectName, float size, Color colour)
        {
            var disc = new GameObject(objectName);
            var meshFilter = disc.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = CreateDiscMesh();
            var renderer = disc.AddComponent<MeshRenderer>();
            disc.transform.localScale = new Vector3(size, size, 0.8f);

            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = colour };
            material.SetColor("_BaseColor", colour);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            return disc;
        }

        private bool HasCurrentDiscGeometry()
        {
            if (transform.childCount != 2)
                return false;

            for (int index = 0; index < transform.childCount; index++)
            {
                MeshFilter filter = transform.GetChild(index).GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || filter.sharedMesh.name != DiscMeshName)
                    return false;
            }
            return true;
        }

        private static Mesh CreateDiscMesh()
        {
            const int segments = 64;
            var vertices = new Vector3[segments + 1];
            var uv = new Vector2[segments + 1];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            uv[0] = new Vector2(0.5f, 0.5f);

            for (int index = 0; index < segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(angle) * 0.5f;
                float y = Mathf.Sin(angle) * 0.5f;
                vertices[index + 1] = new Vector3(x, y, 0f);
                uv[index + 1] = new Vector2(x + 0.5f, y + 0.5f);

                int next = (index + 1) % segments;
                int triangle = index * 3;
                // Reverse winding so the disc faces the race camera on the -Z side.
                triangles[triangle] = 0;
                triangles[triangle + 1] = next + 1;
                triangles[triangle + 2] = index + 1;
            }

            var mesh = new Mesh { name = DiscMeshName };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
