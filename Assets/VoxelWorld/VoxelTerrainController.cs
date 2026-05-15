using System.Collections.Generic;
using UnityEngine;

namespace VoxelWorld
{
    [ExecuteAlways]
    public sealed class VoxelTerrainController : MonoBehaviour
    {
        [Header("Viewer")]
        [SerializeField] private Transform viewer;

        [Header("Chunk Settings")]
        [SerializeField, Min(8)] private int chunkSize = 16;
        [SerializeField, Min(0.25f)] private float voxelSize = 1f;
        [SerializeField, Min(1)] private int viewDistanceInChunks = 4;
        [SerializeField, Range(1, 3)] private int lodCount = 3;
        [SerializeField] private float isoLevel = 0f;
        [SerializeField] private bool generateColliders = true;

        [Header("Rendering")]
        [SerializeField] private Material terrainMaterial;

        [Header("Noise")]
        [SerializeField] private DensitySettings densitySettings = default;

        private readonly Dictionary<VoxelChunkCoord, VoxelChunkRenderable> _chunks =
            new Dictionary<VoxelChunkCoord, VoxelChunkRenderable>();

        private readonly Dictionary<VoxelChunkCoord, VoxelChunkData> _chunkData =
            new Dictionary<VoxelChunkCoord, VoxelChunkData>();

        private Vector3 _lastViewerPosition;

        private void OnEnable()
        {
            EnsureDefaults();

            RebuildAll();
        }

        private void OnDisable()
        {
            ClearTerrain();
        }

        private void OnValidate()
        {
            chunkSize = Mathf.Max(8, chunkSize);
            viewDistanceInChunks = Mathf.Max(1, viewDistanceInChunks);
            lodCount = Mathf.Clamp(lodCount, 1, 3);
            voxelSize = Mathf.Max(0.25f, voxelSize);
            EnsureDefaults();

            if (enabled)
            {
                RebuildAll();
            }
        }

        private void Update()
        {
            Transform activeViewer = viewer != null ? viewer : Camera.main != null ? Camera.main.transform : transform;
            if ((activeViewer.position - _lastViewerPosition).sqrMagnitude >= (chunkSize * voxelSize * 0.5f) * (chunkSize * voxelSize * 0.5f))
            {
                RebuildAll();
            }
        }

        [ContextMenu("Rebuild Terrain")]
        public void RebuildAll()
        {
            EnsureDefaults();
            Transform activeViewer = viewer != null ? viewer : Camera.main != null ? Camera.main.transform : transform;
            _lastViewerPosition = activeViewer.position;

            HashSet<VoxelChunkCoord> desired = CollectDesiredChunks(activeViewer.position);

            List<VoxelChunkCoord> toRemove = new List<VoxelChunkCoord>();
            foreach (VoxelChunkCoord coord in _chunks.Keys)
            {
                if (!desired.Contains(coord))
                {
                    toRemove.Add(coord);
                }
            }

            foreach (VoxelChunkCoord coord in toRemove)
            {
                if (_chunks.TryGetValue(coord, out VoxelChunkRenderable renderable) && renderable != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(renderable.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(renderable.gameObject);
                    }
                }

                _chunks.Remove(coord);
                _chunkData.Remove(coord);
            }

            foreach (VoxelChunkCoord coord in desired)
            {
                EnsureChunk(coord);
            }

            foreach (KeyValuePair<VoxelChunkCoord, VoxelChunkRenderable> entry in _chunks)
            {
                BuildSeams(entry.Key, entry.Value);
            }
        }

        [ContextMenu("Clear Terrain")]
        public void ClearTerrain()
        {
            ClearAllChunks();
        }

        public void EnsureDefaults()
        {
            if (densitySettings.horizontalFrequency <= 0f)
            {
                densitySettings = DensitySettings.Default;
            }
        }

        private HashSet<VoxelChunkCoord> CollectDesiredChunks(Vector3 viewerPosition)
        {
            HashSet<VoxelChunkCoord> coords = new HashSet<VoxelChunkCoord>();
            float baseWorldChunkSize = chunkSize * voxelSize;

            for (int lod = 0; lod < lodCount; lod++)
            {
                float lodScale = 1 << lod;
                float lodWorldChunkSize = baseWorldChunkSize * lodScale;
                int centerX = Mathf.FloorToInt(viewerPosition.x / lodWorldChunkSize);
                int centerZ = Mathf.FloorToInt(viewerPosition.z / lodWorldChunkSize);
                int radius = Mathf.Max(1, Mathf.CeilToInt((float)viewDistanceInChunks / lodScale));
                float minDistance = lod == 0 ? 0f : baseWorldChunkSize * (1 << (lod - 1));
                float maxDistance = baseWorldChunkSize * Mathf.Max(1, viewDistanceInChunks) * lodScale;

                for (int dz = -radius; dz <= radius; dz++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int x = centerX + dx;
                        int z = centerZ + dz;
                        Vector2 chunkCenter = new Vector2(
                            (x + 0.5f) * lodWorldChunkSize,
                            (z + 0.5f) * lodWorldChunkSize);
                        Vector2 viewerXZ = new Vector2(viewerPosition.x, viewerPosition.z);
                        float chebyshevDistance = Mathf.Max(
                            Mathf.Abs(chunkCenter.x - viewerXZ.x),
                            Mathf.Abs(chunkCenter.y - viewerXZ.y));

                        if (chebyshevDistance >= minDistance && chebyshevDistance <= maxDistance)
                        {
                            coords.Add(new VoxelChunkCoord(x, z, lod));
                        }
                    }
                }
            }

            return coords;
        }

        private void EnsureChunk(VoxelChunkCoord coord)
        {
            if (_chunks.ContainsKey(coord))
            {
                return;
            }

            float lodScale = 1 << coord.lod;
            float step = voxelSize * lodScale;
            float worldChunkSize = chunkSize * voxelSize * lodScale;
            Vector3 origin = new Vector3(coord.x * worldChunkSize, 0f, coord.z * worldChunkSize);

            VoxelChunkData chunkData = new VoxelChunkData(coord, chunkSize, step, origin);
            chunkData.Fill(densitySettings);

            VoxelMeshData meshData = TransvoxelMesher.BuildRegular(chunkData, densitySettings, isoLevel);
            Mesh mesh = meshData.IsEmpty ? new Mesh { name = $"Chunk_{coord}_Empty" } : meshData.ToMesh($"Chunk_{coord}");

            GameObject chunkObject = new GameObject($"Chunk_{coord}");
            chunkObject.transform.SetParent(transform, false);
            VoxelChunkRenderable renderable = chunkObject.AddComponent<VoxelChunkRenderable>();
            renderable.Initialize(ResolveMaterial(), generateColliders);
            renderable.SetMainMesh(mesh);

            _chunks.Add(coord, renderable);
            _chunkData.Add(coord, chunkData);
        }

        private void BuildSeams(VoxelChunkCoord coord, VoxelChunkRenderable renderable)
        {
            renderable.ClearSeams();

            if (!_chunkData.TryGetValue(coord, out VoxelChunkData highChunk))
            {
                return;
            }

            BuildDirectionalSeam(coord, renderable, highChunk, ChunkDirection.North, new VoxelChunkCoord(coord.x, coord.z + 1, coord.lod + 1));
            BuildDirectionalSeam(coord, renderable, highChunk, ChunkDirection.South, new VoxelChunkCoord(coord.x, coord.z - 1, coord.lod + 1));
            BuildDirectionalSeam(coord, renderable, highChunk, ChunkDirection.East, new VoxelChunkCoord(coord.x + 1, coord.z, coord.lod + 1));
            BuildDirectionalSeam(coord, renderable, highChunk, ChunkDirection.West, new VoxelChunkCoord(coord.x - 1, coord.z, coord.lod + 1));
        }

        private void BuildDirectionalSeam(
            VoxelChunkCoord coord,
            VoxelChunkRenderable renderable,
            VoxelChunkData chunk,
            ChunkDirection direction,
            VoxelChunkCoord coarseProbe)
        {
            if (coord.lod >= lodCount - 1)
            {
                return;
            }

            Rect highBounds = GetChunkBounds(coord);
            bool hasCoarseNeighbor = false;

            foreach (VoxelChunkCoord existing in _chunkData.Keys)
            {
                if (existing.lod != coord.lod + 1)
                {
                    continue;
                }

                Rect coarseBounds = GetChunkBounds(existing);
                if (TouchesOnDirection(highBounds, coarseBounds, direction))
                {
                    hasCoarseNeighbor = true;
                    break;
                }
            }

            if (!hasCoarseNeighbor)
            {
                return;
            }

            VoxelMeshData seamData = TransvoxelMesher.BuildSeam(chunk, densitySettings, isoLevel, direction);
            if (!seamData.IsEmpty)
            {
                renderable.SetSeamMesh(direction, seamData.ToMesh($"Seam_{coord}_{direction}"), ResolveMaterial());
            }
        }

        private Rect GetChunkBounds(VoxelChunkCoord coord)
        {
            float worldChunkSize = chunkSize * voxelSize * (1 << coord.lod);
            return new Rect(coord.x * worldChunkSize, coord.z * worldChunkSize, worldChunkSize, worldChunkSize);
        }

        private static bool TouchesOnDirection(Rect high, Rect coarse, ChunkDirection direction)
        {
            const float epsilon = 0.001f;

            return direction switch
            {
                ChunkDirection.North =>
                    Mathf.Abs(high.yMax - coarse.yMin) < epsilon && RangesOverlap(high.xMin, high.xMax, coarse.xMin, coarse.xMax),
                ChunkDirection.South =>
                    Mathf.Abs(high.yMin - coarse.yMax) < epsilon && RangesOverlap(high.xMin, high.xMax, coarse.xMin, coarse.xMax),
                ChunkDirection.East =>
                    Mathf.Abs(high.xMax - coarse.xMin) < epsilon && RangesOverlap(high.yMin, high.yMax, coarse.yMin, coarse.yMax),
                _ =>
                    Mathf.Abs(high.xMin - coarse.xMax) < epsilon && RangesOverlap(high.yMin, high.yMax, coarse.yMin, coarse.yMax)
            };
        }

        private static bool RangesOverlap(float minA, float maxA, float minB, float maxB)
        {
            return maxA > minB && maxB > minA;
        }

        private Material ResolveMaterial()
        {
            if (terrainMaterial != null)
            {
                return terrainMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            terrainMaterial = new Material(shader)
            {
                name = "VoxelTerrain_AutoMaterial"
            };
            terrainMaterial.color = new Color(0.45f, 0.65f, 0.42f, 1f);
            return terrainMaterial;
        }

        private void ClearAllChunks()
        {
            foreach (VoxelChunkRenderable renderable in _chunks.Values)
            {
                if (renderable == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(renderable.gameObject);
                }
                else
                {
                    DestroyImmediate(renderable.gameObject);
                }
            }

            _chunks.Clear();
            _chunkData.Clear();
        }
    }
}
