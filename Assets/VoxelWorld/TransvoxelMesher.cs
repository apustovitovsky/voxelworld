using UnityEngine;

namespace VoxelWorld
{
    public enum ChunkDirection
    {
        North,
        South,
        East,
        West
    }

    public static class TransvoxelMesher
    {
        private const float EndpointEpsilon = 0.0001f;

        private static int[] CreateEmptyCell(int size)
        {
            int[] cell = new int[size];
            ResetCell(cell);
            return cell;
        }

        private static void ResetCell(int[] cell)
        {
            for (int i = 0; i < cell.Length; i++)
            {
                cell[i] = -1;
            }
        }

        private sealed class TransitionCache
        {
            private readonly int[][][] _rows;

            public TransitionCache(int width)
            {
                _rows = new int[2][][];
                for (int row = 0; row < 2; row++)
                {
                    _rows[row] = new int[width][];
                    for (int x = 0; x < width; x++)
                    {
                        _rows[row][x] = CreateEmptyCell(12);
                    }
                }
            }

            public void ClearRow(int parity)
            {
                int[][] row = _rows[parity & 1];
                for (int x = 0; x < row.Length; x++)
                {
                    ResetCell(row[x]);
                }
            }

            public int[] GetCell(int fx, int fy)
            {
                return _rows[fy & 1][fx];
            }
        }

        private sealed class RegularCache
        {
            private readonly int[][][] _decks;
            private readonly int _width;

            public RegularCache(int width, int height)
            {
                _width = width;
                _decks = new int[2][][];
                for (int deck = 0; deck < 2; deck++)
                {
                    _decks[deck] = new int[width * height][];
                    for (int i = 0; i < _decks[deck].Length; i++)
                    {
                        _decks[deck][i] = CreateEmptyCell(4);
                    }
                }
            }

            public void ClearDeck(int z)
            {
                int[][] deck = _decks[z & 1];
                for (int i = 0; i < deck.Length; i++)
                {
                    ResetCell(deck[i]);
                }
            }

            public int[] GetCell(int x, int y, int z)
            {
                return _decks[z & 1][x + y * _width];
            }
        }

        public static VoxelMeshData BuildRegular(
            VoxelChunkData chunk,
            DensitySettings settings,
            float isoLevel)
        {
            VoxelMeshData mesh = new VoxelMeshData();
            RegularCache cache = new RegularCache(chunk.Resolution, chunk.Resolution);
            Vector3[] positions = new Vector3[8];
            float[] samples = new float[8];
            int[] cellVertexIndices = new int[12];

            for (int z = 0; z < chunk.Resolution; z++)
            {
                cache.ClearDeck(z);

                for (int y = 0; y < chunk.Resolution; y++)
                {
                    for (int x = 0; x < chunk.Resolution; x++)
                    {
                        int caseCode = 0;
                        for (int i = 0; i < 8; i++)
                        {
                            Vector3Int offset = TransvoxelTables.RegularCornerOffsets[i];
                            int px = x + offset.x;
                            int py = y + offset.y;
                            int pz = z + offset.z;

                            positions[i] = chunk.GetPosition(px, py, pz);
                            samples[i] = chunk.GetDensity(px, py, pz);

                            if (samples[i] < isoLevel)
                            {
                                caseCode |= 1 << i;
                            }
                        }

                        if (caseCode == 0 || caseCode == 255)
                        {
                            continue;
                        }

                        TransvoxelCellData cellData = TransvoxelTables.RegularCellData[
                            TransvoxelTables.RegularCellClass[caseCode]];

                        for (int i = 0; i < cellVertexIndices.Length; i++)
                        {
                            cellVertexIndices[i] = -1;
                        }

                        int[] currentReuseCell = cache.GetCell(x, y, z);
                        int directionValidityMask =
                            (x > 0 ? 1 : 0) |
                            ((y > 0 ? 1 : 0) << 1) |
                            ((z > 0 ? 1 : 0) << 2);

                        for (int vertexIndex = 0; vertexIndex < cellData.VertexCount; vertexIndex++)
                        {
                            ushort edgeCode = TransvoxelTables.RegularVertexData[caseCode][vertexIndex];
                            int edgeCodeLow = edgeCode & 0xFF;
                            int edgeCodeHigh = (edgeCode >> 8) & 0xFF;
                            int v0 = (edgeCodeLow >> 4) & 0xF;
                            int v1 = edgeCodeLow & 0xF;

                            float sample0 = samples[v0];
                            float sample1 = samples[v1];
                            float denominator = sample1 - sample0;
                            float t = Mathf.Abs(denominator) < EndpointEpsilon ?
                                0.5f :
                                Mathf.Clamp01((isoLevel - sample0) / denominator);

                            Vector3 p0 = positions[v0];
                            Vector3 p1 = positions[v1];

                            if (t > EndpointEpsilon && t < 1f - EndpointEpsilon)
                            {
                                int reuseDirection = (edgeCodeHigh >> 4) & 0xF;
                                int reuseVertexIndex = edgeCodeHigh & 0xF;
                                bool present = (reuseDirection & directionValidityMask) == reuseDirection;

                                if (present)
                                {
                                    int[] previousCell = cache.GetCell(
                                        x - (reuseDirection & 1),
                                        y - ((reuseDirection >> 1) & 1),
                                        z - ((reuseDirection >> 2) & 1));
                                    cellVertexIndices[vertexIndex] = previousCell[reuseVertexIndex];
                                }

                                if (cellVertexIndices[vertexIndex] == -1)
                                {
                                    Vector3 position = Vector3.LerpUnclamped(p0, p1, t);
                                    Vector3 normal = DensitySampler.EstimateNormal(position, settings, chunk.Step);
                                    int createdVertex = mesh.AddVertex(position, normal);
                                    cellVertexIndices[vertexIndex] = createdVertex;

                                    if ((reuseDirection & 0x8) != 0)
                                    {
                                        currentReuseCell[reuseVertexIndex] = createdVertex;
                                    }
                                }
                            }
                            else if (t <= EndpointEpsilon && v1 == 7)
                            {
                                Vector3 position = p1;
                                Vector3 normal = DensitySampler.EstimateNormal(position, settings, chunk.Step);
                                int createdVertex = mesh.AddVertex(position, normal);
                                cellVertexIndices[vertexIndex] = createdVertex;
                                currentReuseCell[0] = createdVertex;
                            }
                            else
                            {
                                Vector3 position = t <= EndpointEpsilon ? p0 : p1;
                                Vector3 normal = DensitySampler.EstimateNormal(position, settings, chunk.Step);
                                cellVertexIndices[vertexIndex] = mesh.AddVertex(position, normal);
                            }
                        }

                        for (int tri = 0; tri < cellData.TriangleCount; tri++)
                        {
                            int t0 = tri * 3;
                            int i0 = cellVertexIndices[cellData.VertexIndex[t0]];
                            int i1 = cellVertexIndices[cellData.VertexIndex[t0 + 1]];
                            int i2 = cellVertexIndices[cellData.VertexIndex[t0 + 2]];
                            mesh.AddIndexedTriangle(i0, i1, i2);
                        }
                    }
                }
            }

            return mesh;
        }

        public static VoxelMeshData BuildTransition(
            VoxelChunkData chunk,
            DensitySettings settings,
            float isoLevel,
            ChunkDirection direction)
        {
            int resolution = chunk.Resolution;
            if ((resolution & 1) != 0 || resolution < 2)
            {
                return new VoxelMeshData();
            }

            VoxelMeshData mesh = new VoxelMeshData();
            TransitionCache cache = new TransitionCache(resolution + 1);
            Vector3[] positions = new Vector3[13];
            float[] samples = new float[13];
            int[] cellVertexIndices = new int[12];

            for (int fy = 0; fy < resolution; fy += 2)
            {
                cache.ClearRow(fy & 1);

                for (int fx = 0; fx < resolution; fx += 2)
                {
                    FillCellSamples(chunk, settings, direction, fx, fy, positions, samples);

                    bool firstSign = samples[0] > isoLevel;
                    bool allSame = true;
                    for (int i = 1; i < 9; i++)
                    {
                        if ((samples[i] > isoLevel) != firstSign)
                        {
                            allSame = false;
                            break;
                        }
                    }

                    if (allSame)
                    {
                        continue;
                    }

                    int caseCode = GetCaseCode(samples, isoLevel);
                    if (caseCode == 0 || caseCode == 511)
                    {
                        continue;
                    }

                    for (int i = 0; i < cellVertexIndices.Length; i++)
                    {
                        cellVertexIndices[i] = -1;
                    }

                    byte cellClass = TransvoxelTables.TransitionCellClass[caseCode];
                    bool flipTriangles = (cellClass & 0x80) != 0;
                    TransvoxelCellData cellData = TransvoxelTables.TransitionCellData[cellClass & 0x7F];
                    int[] currentReuseCell = cache.GetCell(fx, fy);
                    int directionValidityMask = (fx > 0 ? 1 : 0) | ((fy > 0 ? 1 : 0) << 1);

                    for (int vertexIndex = 0; vertexIndex < cellData.VertexCount; vertexIndex++)
                    {
                        ushort edgeCode = TransvoxelTables.TransitionVertexData[caseCode][vertexIndex];
                        int indexA = (edgeCode >> 4) & 0xF;
                        int indexB = edgeCode & 0xF;

                        float sampleA = samples[indexA];
                        float sampleB = samples[indexB];
                        float denominator = sampleB - sampleA;
                        float t = Mathf.Abs(denominator) < EndpointEpsilon ?
                            0.5f :
                            Mathf.Clamp01((isoLevel - sampleA) / denominator);

                        if (t > EndpointEpsilon && t < 1f - EndpointEpsilon)
                        {
                            int reuseSlot = (edgeCode >> 8) & 0xF;
                            int reuseDirection = (edgeCode >> 12) & 0xF;
                            bool present = (reuseDirection & directionValidityMask) == reuseDirection;

                            if (present)
                            {
                                int[] previousCell = cache.GetCell(
                                    fx - (reuseDirection & 1),
                                    fy - ((reuseDirection >> 1) & 1));
                                cellVertexIndices[vertexIndex] = previousCell[reuseSlot];
                            }

                            if (cellVertexIndices[vertexIndex] == -1)
                            {
                                Vector3 position = Vector3.LerpUnclamped(positions[indexA], positions[indexB], t);
                                Vector3 normal = DensitySampler.EstimateNormal(position, settings, chunk.Step);
                                int createdVertex = mesh.AddVertex(position, normal);
                                cellVertexIndices[vertexIndex] = createdVertex;

                                if ((reuseDirection & 0x8) != 0)
                                {
                                    currentReuseCell[reuseSlot] = createdVertex;
                                }
                            }
                        }
                        else
                        {
                            int cornerIndex = t <= EndpointEpsilon ? indexA : indexB;
                            byte cornerData = TransvoxelTables.TransitionCornerData[cornerIndex];
                            int reuseSlot = cornerData & 0xF;
                            int reuseDirection = (cornerData >> 4) & 0xF;
                            bool present = (reuseDirection & directionValidityMask) == reuseDirection;

                            if (present)
                            {
                                int[] previousCell = cache.GetCell(
                                    fx - (reuseDirection & 1),
                                    fy - ((reuseDirection >> 1) & 1));
                                cellVertexIndices[vertexIndex] = previousCell[reuseSlot];
                            }

                            if (cellVertexIndices[vertexIndex] == -1)
                            {
                                Vector3 position = positions[cornerIndex];
                                Vector3 normal = DensitySampler.EstimateNormal(position, settings, chunk.Step);
                                int createdVertex = mesh.AddVertex(position, normal);
                                cellVertexIndices[vertexIndex] = createdVertex;
                                currentReuseCell[reuseSlot] = createdVertex;
                            }
                        }
                    }

                    for (int triangleIndex = 0; triangleIndex < cellData.TriangleCount; triangleIndex++)
                    {
                        int i0 = cellVertexIndices[cellData.VertexIndex[triangleIndex * 3]];
                        int i1 = cellVertexIndices[cellData.VertexIndex[triangleIndex * 3 + 1]];
                        int i2 = cellVertexIndices[cellData.VertexIndex[triangleIndex * 3 + 2]];

                        if (flipTriangles)
                        {
                            mesh.AddIndexedTriangle(i0, i1, i2);
                        }
                        else
                        {
                            mesh.AddIndexedTriangle(i2, i1, i0);
                        }
                    }
                }
            }

            return mesh;
        }

        public static VoxelMeshData BuildSeam(
            VoxelChunkData chunk,
            DensitySettings settings,
            float isoLevel,
            ChunkDirection direction)
        {
            return BuildTransition(chunk, settings, isoLevel, direction);
        }

        private static int GetCaseCode(float[] samples, float isoLevel)
        {
            int caseCode = Sign(samples[0], isoLevel);
            caseCode |= Sign(samples[1], isoLevel) << 1;
            caseCode |= Sign(samples[2], isoLevel) << 2;
            caseCode |= Sign(samples[5], isoLevel) << 3;
            caseCode |= Sign(samples[8], isoLevel) << 4;
            caseCode |= Sign(samples[7], isoLevel) << 5;
            caseCode |= Sign(samples[6], isoLevel) << 6;
            caseCode |= Sign(samples[3], isoLevel) << 7;
            caseCode |= Sign(samples[4], isoLevel) << 8;
            return caseCode;
        }

        private static int Sign(float value, float isoLevel)
        {
            return value > isoLevel ? 1 : 0;
        }

        private static void FillCellSamples(
            VoxelChunkData chunk,
            DensitySettings settings,
            ChunkDirection direction,
            int fx,
            int fy,
            Vector3[] positions,
            float[] samples)
        {
            positions[0] = GetFacePosition(chunk, direction, fx, fy);
            positions[1] = GetFacePosition(chunk, direction, fx + 1, fy);
            positions[2] = GetFacePosition(chunk, direction, fx + 2, fy);
            positions[3] = GetFacePosition(chunk, direction, fx, fy + 1);
            positions[4] = GetFacePosition(chunk, direction, fx + 1, fy + 1);
            positions[5] = GetFacePosition(chunk, direction, fx + 2, fy + 1);
            positions[6] = GetFacePosition(chunk, direction, fx, fy + 2);
            positions[7] = GetFacePosition(chunk, direction, fx + 1, fy + 2);
            positions[8] = GetFacePosition(chunk, direction, fx + 2, fy + 2);

            for (int i = 0; i < 9; i++)
            {
                samples[i] = DensitySampler.Sample(positions[i], settings);
            }

            positions[9] = positions[0];
            positions[10] = positions[2];
            positions[11] = positions[6];
            positions[12] = positions[8];

            samples[9] = samples[0];
            samples[10] = samples[2];
            samples[11] = samples[6];
            samples[12] = samples[8];
        }

        private static Vector3 GetFacePosition(
            VoxelChunkData chunk,
            ChunkDirection direction,
            int faceX,
            int faceY)
        {
            return direction switch
            {
                ChunkDirection.South => chunk.GetPosition(faceX, faceY, 0),
                ChunkDirection.North => chunk.GetPosition(faceY, faceX, chunk.Resolution),
                ChunkDirection.East => chunk.GetPosition(chunk.Resolution, faceY, faceX),
                _ => chunk.GetPosition(0, faceX, faceY)
            };
        }
    }
}
