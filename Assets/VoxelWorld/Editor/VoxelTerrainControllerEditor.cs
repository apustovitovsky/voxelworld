using UnityEditor;
using UnityEngine;

namespace VoxelWorld.Editor
{
    [CustomEditor(typeof(VoxelTerrainController))]
    public sealed class VoxelTerrainControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            VoxelTerrainController controller = (VoxelTerrainController)target;

            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clear"))
                {
                    Undo.RegisterFullObjectHierarchyUndo(controller.gameObject, "Clear Voxel Terrain");
                    controller.ClearTerrain();
                    EditorUtility.SetDirty(controller);
                }

                if (GUILayout.Button("Regenerate"))
                {
                    Undo.RegisterFullObjectHierarchyUndo(controller.gameObject, "Regenerate Voxel Terrain");
                    controller.RebuildAll();
                    EditorUtility.SetDirty(controller);
                }
            }
        }
    }
}
