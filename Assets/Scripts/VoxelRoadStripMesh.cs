using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    /// <summary>A closed, static road strip sharing its sampled ends with adjacent strips/chunks.</summary>
    [ExecuteAlways]
    public sealed class VoxelRoadStripMesh : MonoBehaviour
    {
        [SerializeField, HideInInspector] private Mesh ownedMesh;
        [SerializeField, HideInInspector] private int meshOwnerId;

        public static GameObject Create(string name, Transform parent, VoxelTrackPose[] frames,
            float startDistance, float sliceLength, float left, float right, float bottom, float top, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            go.transform.SetParent(parent, false);
            Vector3 origin = frames[0].position;
            go.transform.SetPositionAndRotation(origin, Quaternion.identity);
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int slice = 0; slice < frames.Length - 1; slice++)
            {
                float a = startDistance + slice * sliceLength, b = a + sliceLength;
                // Upward top, downward bottom and outward side walls, with flat voxel-style normals.
                Quad(slice,left,top,a, slice+1,left,top,b, slice+1,right,top,b, slice,right,top,a);
                Quad(slice,left,bottom,a, slice,right,bottom,a, slice+1,right,bottom,b, slice+1,left,bottom,b);
                Quad(slice,left,bottom,a, slice+1,left,bottom,b, slice+1,left,top,b, slice,left,top,a);
                Quad(slice,right,bottom,a, slice,right,top,a, slice+1,right,top,b, slice+1,right,bottom,b);
            }
            int last = frames.Length - 1; float endDistance = startDistance + last * sliceLength;
            Quad(0,left,bottom,startDistance, 0,left,top,startDistance, 0,right,top,startDistance, 0,right,bottom,startDistance);
            Quad(last,left,bottom,endDistance, last,right,bottom,endDistance, last,right,top,endDistance, last,left,top,endDistance);
            var mesh = new Mesh { name = name + " Joined Strip", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var owner = go.AddComponent<VoxelRoadStripMesh>();
            owner.ownedMesh = mesh; owner.meshOwnerId = owner.GetInstanceID();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.GetComponent<MeshCollider>().sharedMesh = mesh;
            return go;

            void Vertex(int frame,float offset,float height,float distance)
            {
                vertices.Add(go.transform.InverseTransformPoint(frames[frame].position + frames[frame].right * offset + Vector3.up * height));
                uv.Add(new Vector2(offset,distance));
            }
            void Quad(int f0,float x0,float y0,float d0, int f1,float x1,float y1,float d1,
                int f2,float x2,float y2,float d2, int f3,float x3,float y3,float d3)
            {
                int first=vertices.Count;
                Vertex(f0,x0,y0,d0);Vertex(f1,x1,y1,d1);Vertex(f2,x2,y2,d2);Vertex(f3,x3,y3,d3);
                triangles.Add(first);triangles.Add(first+1);triangles.Add(first+2);
                triangles.Add(first);triangles.Add(first+2);triangles.Add(first+3);
            }
        }

        private void OnDestroy()
        {
            // A preview clone shares the source mesh but must not release the source's resource.
            if (ownedMesh == null || meshOwnerId != GetInstanceID()) return;
            if (Application.isPlaying) Destroy(ownedMesh); else DestroyImmediate(ownedMesh);
        }
    }
}
