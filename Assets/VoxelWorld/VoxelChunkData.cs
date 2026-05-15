using UnityEngine;

namespace VoxelWorld
{
    public sealed class VoxelChunkData
    {
        public readonly VoxelChunkCoord Coord;
        public readonly int Resolution;
        public readonly float Step;
        public readonly Vector3 Origin;

        private readonly float[] _densities;

        public VoxelChunkData(VoxelChunkCoord coord, int resolution, float step, Vector3 origin)
        {
            Coord = coord;
            Resolution = resolution;
            Step = step;
            Origin = origin;
            _densities = new float[(resolution + 1) * (resolution + 1) * (resolution + 1)];
        }

        public void Fill(DensitySettings settings)
        {
            int size = Resolution + 1;
            for (int z = 0; z < size; z++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        Vector3 world = Origin + new Vector3(x * Step, y * Step, z * Step);
                        _densities[Index(x, y, z)] = DensitySampler.Sample(world, settings);
                    }
                }
            }
        }

        public float GetDensity(int x, int y, int z)
        {
            return _densities[Index(x, y, z)];
        }

        public Vector3 GetPosition(int x, int y, int z)
        {
            return Origin + new Vector3(x * Step, y * Step, z * Step);
        }

        private int Index(int x, int y, int z)
        {
            int size = Resolution + 1;
            return x + size * (y + size * z);
        }
    }
}
