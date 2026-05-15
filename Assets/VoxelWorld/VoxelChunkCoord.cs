using System;
using UnityEngine;

namespace VoxelWorld
{
    [Serializable]
    public struct VoxelChunkCoord : IEquatable<VoxelChunkCoord>
    {
        public int x;
        public int z;
        public int lod;

        public VoxelChunkCoord(int x, int z, int lod)
        {
            this.x = x;
            this.z = z;
            this.lod = lod;
        }

        public bool Equals(VoxelChunkCoord other)
        {
            return x == other.x && z == other.z && lod == other.lod;
        }

        public override bool Equals(object obj)
        {
            return obj is VoxelChunkCoord other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = x;
                hash = (hash * 397) ^ z;
                hash = (hash * 397) ^ lod;
                return hash;
            }
        }

        public override string ToString()
        {
            return $"({x},{z})@LOD{lod}";
        }
    }
}
