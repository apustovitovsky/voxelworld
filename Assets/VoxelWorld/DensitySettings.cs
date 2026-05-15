using System;
using UnityEngine;

namespace VoxelWorld
{
    [Serializable]
    public struct DensitySettings
    {
        [Min(1)]
        public int seed;

        [Min(0.001f)]
        public float baseHeight;

        [Min(0.001f)]
        public float heightScale;

        [Min(0.001f)]
        public float horizontalFrequency;

        [Min(0.001f)]
        public float detailFrequency;

        [Min(0f)]
        public float detailAmplitude;

        [Min(0.001f)]
        public float ridgeFrequency;

        [Range(0f, 2f)]
        public float ridgeStrength;

        [Min(0.001f)]
        public float warpFrequency;

        [Min(0f)]
        public float warpStrength;

        public static DensitySettings Default => new DensitySettings
        {
            seed = 1337,
            baseHeight = 10f,
            heightScale = 28f,
            horizontalFrequency = 0.0065f,
            detailFrequency = 0.045f,
            detailAmplitude = 2.5f,
            ridgeFrequency = 0.018f,
            ridgeStrength = 0.85f,
            warpFrequency = 0.012f,
            warpStrength = 18f
        };
    }
}
