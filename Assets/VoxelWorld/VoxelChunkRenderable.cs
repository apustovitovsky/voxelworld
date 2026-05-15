using System.Collections.Generic;
using UnityEngine;

namespace VoxelWorld
{
    [DisallowMultipleComponent]
    public sealed class VoxelChunkRenderable : MonoBehaviour
    {
        private readonly Dictionary<ChunkDirection, GameObject> _seamObjects = new Dictionary<ChunkDirection, GameObject>();

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MeshCollider _meshCollider;

        public void Initialize(Material material, bool generateCollision)
        {
            _meshFilter = GetComponent<MeshFilter>();
            if (_meshFilter == null)
            {
                _meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshRenderer == null)
            {
                _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            _meshRenderer.sharedMaterial = material;

            if (generateCollision)
            {
                _meshCollider = GetComponent<MeshCollider>();
                if (_meshCollider == null)
                {
                    _meshCollider = gameObject.AddComponent<MeshCollider>();
                }
            }
        }

        public void SetMainMesh(Mesh mesh)
        {
            _meshFilter.sharedMesh = mesh;
            if (_meshCollider != null)
            {
                _meshCollider.sharedMesh = mesh;
            }
        }

        public void SetSeamMesh(ChunkDirection direction, Mesh mesh, Material material)
        {
            if (!_seamObjects.TryGetValue(direction, out GameObject seamObject) || seamObject == null)
            {
                seamObject = new GameObject($"Seam_{direction}");
                seamObject.transform.SetParent(transform, false);
                seamObject.AddComponent<MeshFilter>();
                MeshRenderer renderer = seamObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                _seamObjects[direction] = seamObject;
            }

            seamObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            seamObject.SetActive(mesh != null);
        }

        public void ClearSeams()
        {
            foreach (GameObject seamObject in _seamObjects.Values)
            {
                if (seamObject != null)
                {
                    seamObject.SetActive(false);
                }
            }
        }
    }
}
