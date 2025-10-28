#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Editor {
/// Custom inspector that exposes generation controls for <see cref="CellularAutomata"/>.
[CustomEditor(typeof(CellularAutomata))]
public class CellularAutomataEditor : UnityEditor.Editor {
    /// Draws the inspector layout and appends a regenerate button.
    public override void OnInspectorGUI() {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope()) {
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Regenerate Caves", GUILayout.MaxWidth(160f))) {
                foreach (Object targetObject in targets) {
                    if (targetObject is CellularAutomata generator) {
                        generator.GenerateCaves();

                        if (!Application.isPlaying) {
                            EditorUtility.SetDirty(generator);

                            Tilemap tilemap = generator.GetComponent<Tilemap>();
                            if (tilemap != null) {
                                EditorUtility.SetDirty(tilemap);
                            }

                            EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
                        }
                    }
                }
            }
        }
    }
}
}
#endif
