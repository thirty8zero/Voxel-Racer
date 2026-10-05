using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>A font-independent chevron with no overlapping translucent geometry.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VoxelSteeringChevron : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            AddVertex(mesh, rect, -0.5f, 0.35f);
            AddVertex(mesh, rect, -0.2f, 0.5f);
            AddVertex(mesh, rect, 0.5f, 0f);
            AddVertex(mesh, rect, -0.2f, -0.5f);
            AddVertex(mesh, rect, -0.5f, -0.35f);
            AddVertex(mesh, rect, 0f, 0f);
            mesh.AddTriangle(0, 1, 2);
            mesh.AddTriangle(0, 2, 5);
            mesh.AddTriangle(5, 2, 3);
            mesh.AddTriangle(5, 3, 4);
        }

        private void AddVertex(VertexHelper mesh, Rect rect, float x, float y)
        {
            mesh.AddVert(new Vector3(rect.center.x + x * rect.width, rect.center.y + y * rect.height, 0f),
                color, Vector2.zero);
        }
    }
}
