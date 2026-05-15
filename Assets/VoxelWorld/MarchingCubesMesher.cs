using UnityEngine;

namespace VoxelWorld
{
    public static class MarchingCubesMesher
    {
        public static VoxelMeshData Build(VoxelChunkData chunk, DensitySettings settings, float isoLevel)
        {
            VoxelMeshData mesh = new VoxelMeshData();

            for (int z = 0; z < chunk.Resolution; z++)
            {
                for (int y = 0; y < chunk.Resolution; y++)
                {
                    for (int x = 0; x < chunk.Resolution; x++)
                    {
                        PolygonizeCell(chunk, settings, mesh, x, y, z, isoLevel);
                    }
                }
            }

            return mesh;
        }

        private static void PolygonizeCell(
            VoxelChunkData chunk,
            DensitySettings settings,
            VoxelMeshData mesh,
            int x,
            int y,
            int z,
            float isoLevel)
        {
            Vector3[] positions = new Vector3[8];
            float[] densities = new float[8];
            int cubeIndex = 0;

            for (int i = 0; i < 8; i++)
            {
                Vector3Int offset = MarchingCubesTables.CornerOffsets[i];
                int px = x + offset.x;
                int py = y + offset.y;
                int pz = z + offset.z;

                positions[i] = chunk.GetPosition(px, py, pz);
                densities[i] = chunk.GetDensity(px, py, pz);

                if (densities[i] <= isoLevel)
                {
                    cubeIndex |= 1 << i;
                }
            }

            int edgeMask = MarchingCubesTables.EdgeTable[cubeIndex];
            if (edgeMask == 0)
            {
                return;
            }

            Vector3[] edgeVertices = new Vector3[12];
            float normalEpsilon = chunk.Step * 0.5f;

            for (int edge = 0; edge < 12; edge++)
            {
                if ((edgeMask & (1 << edge)) == 0)
                {
                    continue;
                }

                int cornerA = MarchingCubesTables.EdgeCorners[edge, 0];
                int cornerB = MarchingCubesTables.EdgeCorners[edge, 1];
                edgeVertices[edge] = VertexInterp(
                    isoLevel,
                    positions[cornerA],
                    positions[cornerB],
                    densities[cornerA],
                    densities[cornerB]);
            }

            for (int tri = 0; tri < 12; tri += 3)
            {
                int edgeA = MarchingCubesTables.TriTable[cubeIndex, tri];
                if (edgeA < 0)
                {
                    break;
                }

                int edgeB = MarchingCubesTables.TriTable[cubeIndex, tri + 1];
                int edgeC = MarchingCubesTables.TriTable[cubeIndex, tri + 2];

                Vector3 a = edgeVertices[edgeA];
                Vector3 b = edgeVertices[edgeB];
                Vector3 c = edgeVertices[edgeC];

                mesh.AddTriangle(
                    a,
                    b,
                    c,
                    DensitySampler.EstimateNormal(a, settings, normalEpsilon),
                    DensitySampler.EstimateNormal(b, settings, normalEpsilon),
                    DensitySampler.EstimateNormal(c, settings, normalEpsilon));
            }
        }

        private static Vector3 VertexInterp(float isoLevel, Vector3 p1, Vector3 p2, float d1, float d2)
        {
            if (Mathf.Abs(isoLevel - d1) < 0.00001f)
            {
                return p1;
            }

            if (Mathf.Abs(isoLevel - d2) < 0.00001f)
            {
                return p2;
            }

            if (Mathf.Abs(d1 - d2) < 0.00001f)
            {
                return p1;
            }

            float t = (isoLevel - d1) / (d2 - d1);
            return Vector3.LerpUnclamped(p1, p2, t);
        }
    }
}
