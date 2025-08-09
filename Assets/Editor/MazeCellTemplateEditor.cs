#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Dungonator.Client.Layers.Maze.MazeCellTemplates;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Editor
{
    /// <summary>
    /// Custom inspector + JSON import/export for <see cref="MazeCellTemplate"/>.
    /// Place this script anywhere inside an <c>Editor/</c> folder.
    /// </summary>
    [CustomEditor(typeof(MazeCellTemplate))]
    public class MazeCellTemplateEditor : UnityEditor.Editor
    {
        private SerializedProperty _cellSizeProp;
        private SerializedProperty _flatLayoutProp;

        // GUI constants
        private const float TilePreviewSize = 48f;
        private const float TilePreviewPadding = 2f;

        [Serializable]
        private class TemplateJson
        {
            public List<string> tilePaths = new();
            public int size = 10;
        }

        private void OnEnable()
        {
            _cellSizeProp = serializedObject.FindProperty("cellSizeXY");
            _flatLayoutProp = serializedObject.FindProperty("flatLayout");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Size field ----------------------------------------
            EditorGUILayout.PropertyField(_cellSizeProp, new GUIContent("Cell Size (tiles)"));            
            int size = Mathf.Max(1, _cellSizeProp.intValue);

            EnsureArraySize(size * size);

            EditorGUILayout.Space(4);
            DrawTileGrid(size);
            EditorGUILayout.Space(6);

            DrawJsonButtons();

            serializedObject.ApplyModifiedProperties();
        }

        #region GUI helpers ----------------------------------------------------

        private void DrawTileGrid(int size)
        {
            // Unity’s (0,0) bottom‑left vs designer top‑left: flip Y for intuitive editing
            for (int y = size - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(6);
                for (int x = 0; x < size; x++)
                {
                    int index = y * size + x;
                    SerializedProperty element = _flatLayoutProp.GetArrayElementAtIndex(index);

                    element.objectReferenceValue = EditorGUILayout.ObjectField(
                        element.objectReferenceValue,
                        typeof(TileBase),
                        false,
                        GUILayout.Width(TilePreviewSize),
                        GUILayout.Height(TilePreviewSize)) as TileBase;

                    GUILayout.Space(TilePreviewPadding);
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawJsonButtons()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Export to JSON"))
                ExportJson();
            if (GUILayout.Button("Import from JSON"))
                ImportJson();
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region JSON import / export ------------------------------------------

        private void ExportJson()
        {
            string path = EditorUtility.SaveFilePanel("Export MazeCellTemplate", Application.dataPath, target.name, "json");
            if (string.IsNullOrEmpty(path))
                return;

            var json = new TemplateJson
            {
                size = _cellSizeProp.intValue
            };

            for (int i = 0; i < _flatLayoutProp.arraySize; i++)
            {
                TileBase tile = _flatLayoutProp.GetArrayElementAtIndex(i).objectReferenceValue as TileBase;
                string tilePath = tile != null ? AssetDatabase.GetAssetPath(tile) : string.Empty;
                json.tilePaths.Add(tilePath);
            }

            File.WriteAllText(path, JsonUtility.ToJson(json, true));
            AssetDatabase.Refresh();
            Debug.Log($"MazeCellTemplate exported to {path}");
        }

        private void ImportJson()
        {
            string path = EditorUtility.OpenFilePanel("Import MazeCellTemplate", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path))
                return;

            string text = File.ReadAllText(path);
            TemplateJson json;
            try
            {
                json = JsonUtility.FromJson<TemplateJson>(text);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse JSON: {e.Message}");
                return;
            }

            if (json == null || json.tilePaths == null)
            {
                Debug.LogError("JSON file did not contain valid data.");
                return;
            }

            _cellSizeProp.intValue = json.size;
            EnsureArraySize(json.size * json.size);

            for (int i = 0; i < _flatLayoutProp.arraySize; i++)
            {
                string tilePath = i < json.tilePaths.Count ? json.tilePaths[i] : string.Empty;
                TileBase tile = string.IsNullOrEmpty(tilePath) ? null : AssetDatabase.LoadAssetAtPath<TileBase>(tilePath);
                _flatLayoutProp.GetArrayElementAtIndex(i).objectReferenceValue = tile;
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            Debug.Log($"MazeCellTemplate imported from {path}");
        }

        #endregion

        #region Utilities ------------------------------------------------------

        private void EnsureArraySize(int required)
        {
            if (_flatLayoutProp.arraySize != required)
            {
                _flatLayoutProp.arraySize = required;
                // Initialize new elements to null
                for (int i = 0; i < _flatLayoutProp.arraySize; i++)
                {
                    var el = _flatLayoutProp.GetArrayElementAtIndex(i);
                    if (el.objectReferenceValue == null)
                        el.objectReferenceValue = null;
                }
            }
        }

        #endregion
    }
}
#endif
