using System.Collections.Generic;
using UnityEngine;

namespace VoxelWorld
{
    public sealed class VoxelMeshData
    {
        public readonly List<Vector3> Vertices = new List<Vector3>(1024);
        public readonly List<int> Triangles = new List<int>(2048);
        public readonly List<Vector3> Normals = new List<Vector3>(1024);

        public bool IsEmpty => Triangles.Count == 0;

        public int AddVertex(Vector3 position, Vector3 normal)
        {
            int index = Vertices.Count;
            Vertices.Add(position);
            Normals.Add(normal);
            return index;
        }

        public void AddIndexedTriangle(int a, int b, int c)
        {
            Triangles.Add(a);
            Triangles.Add(b);
            Triangles.Add(c);
        }

        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc)
        {
            int ia = AddVertex(a, na);
            int ib = AddVertex(b, nb);
            int ic = AddVertex(c, nc);
            AddIndexedTriangle(ia, ib, ic);
        }

        public Mesh ToMesh(string meshName)
        {
            Mesh mesh = new Mesh
            {
                name = meshName,
                indexFormat = Vertices.Count > 65535 ?
                    UnityEngine.Rendering.IndexFormat.UInt32 :
                    UnityEngine.Rendering.IndexFormat.UInt16
            };
            mesh.SetVertices(Vertices);
            mesh.SetTriangles(Triangles, 0, true);
            mesh.SetNormals(Normals);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
