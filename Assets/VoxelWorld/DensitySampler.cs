using Unity.Mathematics;
using UnityEngine;

namespace VoxelWorld
{
    public static class DensitySampler
    {
        private struct NoiseCacheKey
        {
            public int seed;
            public float horizontalFrequency;
            public float detailFrequency;
            public float ridgeFrequency;
            public float warpFrequency;
            public float warpStrength;
        }

        private static NoiseCacheKey _cachedKey;
        private static bool _hasCache;
        private static FastNoiseLite _baseNoise;
        private static FastNoiseLite _ridgeNoise;
        private static FastNoiseLite _detailNoise;
        private static FastNoiseLite _warpNoise;

        public static float Sample(Vector3 position, DensitySettings settings)
        {
            EnsureNoiseCache(settings);

            float3 world = new float3(position.x, position.y, position.z);
            float2 warpedXZ = ApplyWarp(world.xz);

            float baseHeight = settings.baseHeight + SampleBaseHeight(warpedXZ) * settings.heightScale;
            float ridges = SampleRidges(warpedXZ) * settings.heightScale * settings.ridgeStrength;
            float detail = SampleDetail(new float3(warpedXZ.x, world.y, warpedXZ.y)) * settings.detailAmplitude;

            float terrainHeight = baseHeight + ridges + detail;
            return world.y - terrainHeight;
        }

        public static Vector3 EstimateNormal(Vector3 position, DensitySettings settings, float epsilon)
        {
            float dx = Sample(position + new Vector3(epsilon, 0f, 0f), settings) -
                Sample(position - new Vector3(epsilon, 0f, 0f), settings);
            float dy = Sample(position + new Vector3(0f, epsilon, 0f), settings) -
                Sample(position - new Vector3(0f, epsilon, 0f), settings);
            float dz = Sample(position + new Vector3(0f, 0f, epsilon), settings) -
                Sample(position - new Vector3(0f, 0f, epsilon), settings);
            return new Vector3(dx, dy, dz).normalized;
        }

        private static void EnsureNoiseCache(DensitySettings settings)
        {
            NoiseCacheKey key = new NoiseCacheKey
            {
                seed = settings.seed,
                horizontalFrequency = settings.horizontalFrequency,
                detailFrequency = settings.detailFrequency,
                ridgeFrequency = settings.ridgeFrequency,
                warpFrequency = settings.warpFrequency,
                warpStrength = settings.warpStrength
            };

            if (_hasCache &&
                _cachedKey.seed == key.seed &&
                Mathf.Approximately(_cachedKey.horizontalFrequency, key.horizontalFrequency) &&
                Mathf.Approximately(_cachedKey.detailFrequency, key.detailFrequency) &&
                Mathf.Approximately(_cachedKey.ridgeFrequency, key.ridgeFrequency) &&
                Mathf.Approximately(_cachedKey.warpFrequency, key.warpFrequency) &&
                Mathf.Approximately(_cachedKey.warpStrength, key.warpStrength))
            {
                return;
            }

            _baseNoise = new FastNoiseLite(settings.seed);
            _baseNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2S);
            _baseNoise.SetFrequency(settings.horizontalFrequency);
            _baseNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
            _baseNoise.SetFractalOctaves(4);
            _baseNoise.SetFractalLacunarity(2.1f);
            _baseNoise.SetFractalGain(0.5f);

            _ridgeNoise = new FastNoiseLite(settings.seed + 101);
            _ridgeNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            _ridgeNoise.SetFrequency(settings.ridgeFrequency);
            _ridgeNoise.SetFractalType(FastNoiseLite.FractalType.Ridged);
            _ridgeNoise.SetFractalOctaves(4);
            _ridgeNoise.SetFractalLacunarity(2.2f);
            _ridgeNoise.SetFractalGain(0.55f);
            _ridgeNoise.SetFractalWeightedStrength(0.35f);

            _detailNoise = new FastNoiseLite(settings.seed + 211);
            _detailNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2S);
            _detailNoise.SetRotationType3D(FastNoiseLite.RotationType3D.ImproveXZPlanes);
            _detailNoise.SetFrequency(settings.detailFrequency);
            _detailNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
            _detailNoise.SetFractalOctaves(3);
            _detailNoise.SetFractalLacunarity(2.35f);
            _detailNoise.SetFractalGain(0.5f);

            _warpNoise = new FastNoiseLite(settings.seed + 307);
            _warpNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            _warpNoise.SetFractalType(FastNoiseLite.FractalType.DomainWarpProgressive);
            _warpNoise.SetFrequency(settings.warpFrequency);
            _warpNoise.SetDomainWarpType(FastNoiseLite.DomainWarpType.OpenSimplex2Reduced);
            _warpNoise.SetDomainWarpAmp(settings.warpStrength);
            _warpNoise.SetFractalOctaves(3);
            _warpNoise.SetFractalLacunarity(2f);
            _warpNoise.SetFractalGain(0.5f);

            _cachedKey = key;
            _hasCache = true;
        }

        private static float SampleBaseHeight(float2 position)
        {
            float continental = _baseNoise.GetNoise(position.x, position.y);
            float softened = math.sign(continental) * math.pow(math.abs(continental), 1.15f);
            return softened * 0.8f;
        }

        private static float SampleRidges(float2 position)
        {
            float ridged = _ridgeNoise.GetNoise(position.x, position.y);
            float sharpened = math.saturate((ridged + 1f) * 0.5f);
            sharpened = 1f - math.abs(sharpened * 2f - 1f);
            sharpened = 1f - sharpened;
            return sharpened * 2f - 1f;
        }

        private static float SampleDetail(float3 position)
        {
            float detail = _detailNoise.GetNoise(position.x, position.y, position.z);
            return detail * 0.5f;
        }

        private static float2 ApplyWarp(float2 position)
        {
            float x = position.x;
            float y = position.y;
            _warpNoise.DomainWarp(ref x, ref y);
            return new float2(x, y);
        }
    }
}
