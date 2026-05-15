# Voxel Prototype Plan

## Goal
Build a minimal runtime prototype of smooth voxel terrain in `Assets/VoxelWorld` using:

- signed density field sampling
- Marching Cubes style surface extraction
- separate transition meshes for LOD seams

## Runtime Components
- `VoxelTerrainController`: owns chunk lifecycle, LOD selection, and viewer tracking
- `DensitySampler`: deterministic noise-based SDF sampler
- `VoxelChunkData`: regular density grid with one-voxel overlap
- `MarchingCubesMesher`: generates the main chunk mesh
- `TransvoxelMesher`: generates seam meshes where a chunk borders a coarser neighbor

## Implementation Order
1. Create density sampling and chunk data containers.
2. Generate main chunk meshes for a small procedural terrain.
3. Add chunk LOD selection around a viewer.
4. Add seam mesh generation between `LOD n` and `LOD n + 1`.
5. Expose a single `VoxelTerrainController` MonoBehaviour for scene setup.

## Demo Ready Criteria
- Smooth terrain appears at runtime with deterministic shape from a seed.
- Nearby chunks use finer detail than distant chunks.
- Border seams to coarser chunks are filled by transition meshes.
- The prototype runs as an isolated module without changes to the FPS gameplay code.
