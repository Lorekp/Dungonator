#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Dungonator.Client.Layers.Maze.MazeCellTemplates;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// -----------------------------------------------------------------------------
// Custom inspector + JSON import/export for MazeCellTemplate.
// Place this script anywhere inside an Editor/ folder.
// -----------------------------------------------------------------------------

namespace Editor
{
    /// <summary>
    /// Custom inspector + JSON import/export for <see cref="MazeCellTemplate"/>.
    /// </summary>
    [CustomEditor(typeof(MazeCellTemplate))]
    public class MazeCellTemplateEditor : UnityEditor.Editor
    {
        // --------- JSON DTO ---------
        [Serializable]
        private class TemplateJson
        {
            public string[] tilePaths;
            public int size;
        }

        private MazeCellTemplate _tpl;

        // Painting state
        private TileBase _paintTile;
        private bool _paintContinuous;
        private bool _showGrid = true;
        private int _previewPx = 48;
        private Vector2 _scroll;

        // GUI styles/cached
        private GUIStyle _gridCell;
        private readonly Color _gridLine = new Color(1,1,1,0.12f);

        private const float HeaderSpace = 6f;

        private void OnEnable()
        {
            _tpl = (MazeCellTemplate)target;
            EnsureArraySize();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHeader();
            EditorGUILayout.Space(4);

            DrawTopBar();
            EditorGUILayout.Space(6);

            DrawGridArea();

            EditorGUILayout.Space(6);
            DrawToolsRow();

            EditorGUILayout.Space(10);
            DrawImportExport();

            serializedObject.ApplyModifiedProperties();
        }

        // ---------------- UI Sections ----------------

        private new void DrawHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var shapeProp = serializedObject.FindProperty("shape");
                var sizeProp  = serializedObject.FindProperty("cellSizeXY");

                EditorGUILayout.LabelField("Maze Cell Template", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(shapeProp);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(sizeProp, new GUIContent("Cell Size (X=Y)"));
                    if (GUILayout.Button("Resize", GUILayout.Width(70)))
                    {
                        ResizeGrid(sizeProp.intValue);
                    }
                }

                // Paint source tile
                using (new EditorGUILayout.HorizontalScope())
                {
                    _paintTile = (TileBase)EditorGUILayout.ObjectField(new GUIContent("Brush Tile"), _paintTile, typeof(TileBase), false);
                    _previewPx = EditorGUILayout.IntSlider(new GUIContent("Preview px"), _previewPx, 24, 96);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _showGrid = EditorGUILayout.ToggleLeft("Show grid lines", _showGrid, GUILayout.Width(140));
                    GUILayout.Space(6);
                    EditorGUILayout.HelpBox("LMB: paint • RMB: pick • Alt/Shift: erase • Drag to paint continuously", MessageType.None);
                }
            }
        }

        private void DrawTopBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Fill"))
                {
                    RecordUndo("Fill");
                    for (int i = 0; i < _tpl.flatLayout.Length; i++)
                        _tpl.flatLayout[i] = _paintTile;
                    EditorUtility.SetDirty(_tpl);
                }

                if (GUILayout.Button("Clear"))
                {
                    RecordUndo("Clear");
                    Array.Clear(_tpl.flatLayout, 0, _tpl.flatLayout.Length);
                    EditorUtility.SetDirty(_tpl);
                }

                if (GUILayout.Button("Rotate 90°"))
                {
                    RecordUndo("Rotate 90°");
                    Rotate90();
                    EditorUtility.SetDirty(_tpl);
                }

                if (GUILayout.Button("Flip H"))
                {
                    RecordUndo("Flip H");
                    FlipH();
                    EditorUtility.SetDirty(_tpl);
                }

                if (GUILayout.Button("Flip V"))
                {
                    RecordUndo("Flip V");
                    FlipV();
                    EditorUtility.SetDirty(_tpl);
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawGridArea()
        {
            EnsureArraySize();

            var size = Mathf.Max(1, _tpl.cellSizeXY);
            var totalPx = size * _previewPx;

            // Make a scrollable view if grid is large.
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(Mathf.Min(512, totalPx + 12)));
            var rect = GUILayoutUtility.GetRect(totalPx, totalPx, GUILayout.ExpandWidth(false));

            // Background
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.14f, 0.14f, 0.14f) : new Color(0.9f, 0.9f, 0.9f));

            // Draw cells
            var evt = Event.current;
            bool inGrid = rect.Contains(evt.mousePosition);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var cellRect = new Rect(rect.x + x * _previewPx, rect.y + (size - 1 - y) * _previewPx, _previewPx, _previewPx);

                    // Background checker
                    DrawChecker(cellRect);

                    // Tile preview
                    var tile = _tpl.GetTile(x, y);
                    DrawTilePreview(cellRect, tile);

                    // Gridlines
                    if (_showGrid)
                        DrawBorder(cellRect, _gridLine);

                    // Painting
                    if (inGrid && cellRect.Contains(evt.mousePosition))
                    {
                        HandlePainting(evt, x, y);
                    }
                }
            }

            // Allow continuous paint while dragging
            if (inGrid && (evt.type == EventType.MouseDrag))
                Repaint();

            EditorGUILayout.EndScrollView();

            // Toolbar beneath grid for direct object fields (optional per-cell edit)
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Selected Brush", EditorStyles.boldLabel);
                _paintTile = (TileBase)EditorGUILayout.ObjectField("Brush Tile", _paintTile, typeof(TileBase), false);
            }
        }

        private void DrawToolsRow()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate / Fix Array"))
                {
                    RecordUndo("Validate");
                    EnsureArraySize();
                    EditorUtility.SetDirty(_tpl);
                }

                if (GUILayout.Button("Ping Used Tiles"))
                {
                    var paths = _tpl.flatLayout
                        .Where(t => t != null)
                        .Select(AssetDatabase.GetAssetPath)
                        .Distinct()
                        .OrderBy(p => p)
                        .ToArray();

                    if (paths.Length == 0)
                        Debug.Log("No tiles in template.");
                    else
                    {
                        foreach (var p in paths)
                            Debug.Log(p, AssetDatabase.LoadAssetAtPath<TileBase>(p));
                    }
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawImportExport()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("JSON Import / Export", EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Export JSON"))
                        ExportJson();

                    if (GUILayout.Button("Import JSON"))
                        ImportJson();
                }

                EditorGUILayout.HelpBox(
                    "Export format:\n{\n  \"tilePaths\": [\"Assets/.../tile.asset\", ...],\n  \"size\": N\n}\n" +
                    "Empty cells export as empty strings.", MessageType.None);
            }
        }

        // ---------------- Painting helpers ----------------

        private void HandlePainting(Event evt, int x, int y)
        {
            // LMB paint, RMB pick, Alt/Shift erase.
            bool erase = evt.alt || evt.shift;
            if (evt.type == EventType.MouseDown && (evt.button == 0 || evt.button == 1))
            {
                _paintContinuous = true;
                GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
                evt.Use();

                if (evt.button == 1) // pick
                {
                    _paintTile = _tpl.GetTile(x, y);
                    Repaint();
                    return;
                }

                // paint
                RecordUndo("Paint Tile");
                _tpl.SetTile(x, y, erase ? null : _paintTile);
                EditorUtility.SetDirty(_tpl);
                Repaint();
            }
            else if (evt.type == EventType.MouseDrag && _paintContinuous && evt.button == 0)
            {
                RecordUndo("Paint Tile");
                _tpl.SetTile(x, y, erase ? null : _paintTile);
                EditorUtility.SetDirty(_tpl);
                evt.Use();
            }
            else if (evt.rawType == EventType.MouseUp)
            {
                _paintContinuous = false;
                GUIUtility.hotControl = 0;
            }
        }

        private void DrawTilePreview(Rect r, TileBase tile)
        {
            if (!tile)
                return;

            Texture2D tex = null;
            Rect uv = default;

            // Best quality: direct sprite (for Tile)
            if (tile is Tile concrete && concrete.sprite && concrete.sprite.texture)
            {
                var sp = concrete.sprite;
                tex = sp.texture;
                uv = new Rect(
                    sp.rect.x / tex.width,
                    sp.rect.y / tex.height,
                    sp.rect.width / tex.width,
                    sp.rect.height / tex.height);
            }
            else
            {
                // Fallback to AssetPreview
                tex = AssetPreview.GetAssetPreview(tile) ?? AssetPreview.GetMiniThumbnail(tile);
                uv = new Rect(0, 0, 1, 1);
            }

            if (tex)
                GUI.DrawTextureWithTexCoords(r, tex, uv, alphaBlend: true);
        }

        private void DrawChecker(Rect r)
        {
            // Simple checker background for transparency
            var c1 = new Color(0, 0, 0, 0.10f);
            var c2 = new Color(1, 1, 1, 0.10f);
            float half = r.width / 2f;

            EditorGUI.DrawRect(new Rect(r.x, r.y, half, half), c1);
            EditorGUI.DrawRect(new Rect(r.x + half, r.y, half, half), c2);
            EditorGUI.DrawRect(new Rect(r.x, r.y + half, half, half), c2);
            EditorGUI.DrawRect(new Rect(r.x + half, r.y + half, half, half), c1);
        }

        private void DrawBorder(Rect r, Color color)
        {
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawAAPolyLine(1.5f,
                new Vector3(r.x, r.y),
                new Vector3(r.xMax, r.y),
                new Vector3(r.xMax, r.yMax),
                new Vector3(r.x, r.yMax),
                new Vector3(r.x, r.y));
            Handles.EndGUI();
        }

        // ---------------- Array / Transform ops ----------------

        private void EnsureArraySize()
        {
            var sizeProp  = serializedObject.FindProperty("cellSizeXY");
            var layoutProp = serializedObject.FindProperty("flatLayout");

            int size = Mathf.Max(1, sizeProp.intValue);
            int needed = size * size;

            if (layoutProp == null) return;

            if (layoutProp.arraySize != needed)
            {
                layoutProp.arraySize = needed;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(_tpl);
            }
        }

        private void ResizeGrid(int newSize)
        {
            newSize = Mathf.Max(1, newSize);
            if (newSize == _tpl.cellSizeXY)
            {
                EnsureArraySize();
                return;
            }

            RecordUndo("Resize Grid");

            var oldSize = _tpl.cellSizeXY;
            var old = _tpl.flatLayout;

            _tpl.cellSizeXY = newSize;
            _tpl.flatLayout = new TileBase[newSize * newSize];

            int copy = Mathf.Min(oldSize, newSize);
            for (int y = 0; y < copy; y++)
            {
                for (int x = 0; x < copy; x++)
                {
                    _tpl.SetTile(x, y, old[y * oldSize + x]);
                }
            }

            EditorUtility.SetDirty(_tpl);
        }

        private void Rotate90()
        {
            int n = _tpl.cellSizeXY;
            var src = (TileBase[])_tpl.flatLayout.Clone();

            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // new(x,y) = old(y, n-1-x)
                var val = src[(y) * n + (n - 1 - x)];
                _tpl.flatLayout[y * n + x] = val;
            }
        }

        private void FlipH()
        {
            int n = _tpl.cellSizeXY;
            var src = (TileBase[])_tpl.flatLayout.Clone();
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                _tpl.flatLayout[y * n + x] = src[y * n + (n - 1 - x)];
        }

        private void FlipV()
        {
            int n = _tpl.cellSizeXY;
            var src = (TileBase[])_tpl.flatLayout.Clone();
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                _tpl.flatLayout[y * n + x] = src[(n - 1 - y) * n + x];
        }

        // ---------------- JSON I/O ----------------

        private void ExportJson()
        {
            var path = EditorUtility.SaveFilePanel("Export MazeCellTemplate to JSON", Application.dataPath, $"{_tpl.name}.json", "json");
            if (string.IsNullOrEmpty(path))
                return;

            var size = Mathf.Max(1, _tpl.cellSizeXY);
            var arr = new string[size * size];

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var t = _tpl.GetTile(x, y);
                arr[y * size + x] = t ? AssetDatabase.GetAssetPath(t) : string.Empty;
            }

            var dto = new TemplateJson { tilePaths = arr, size = size };
            var json = JsonUtility.ToJson(dto, prettyPrint: true);
            File.WriteAllText(path, json);
            EditorUtility.RevealInFinder(path);
            Debug.Log($"Exported {_tpl.name} → {path}");
        }

        private void ImportJson()
        {
            var path = EditorUtility.OpenFilePanel("Import MazeCellTemplate from JSON", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                var json = File.ReadAllText(path);
                var dto = JsonUtility.FromJson<TemplateJson>(json);
                if (dto == null || dto.tilePaths == null)
                {
                    EditorUtility.DisplayDialog("Import JSON", "Invalid JSON format.", "OK");
                    return;
                }

                if (dto.size <= 0)
                {
                    EditorUtility.DisplayDialog("Import JSON", "Invalid 'size' in JSON.", "OK");
                    return;
                }

                if (dto.tilePaths.Length != dto.size * dto.size)
                {
                    if (!EditorUtility.DisplayDialog("Import JSON",
                            $"tilePaths length ({dto.tilePaths.Length}) does not match size*size ({dto.size * dto.size}). Continue and clamp?",
                            "Continue", "Cancel"))
                        return;
                }

                RecordUndo("Import JSON");

                ResizeGrid(dto.size);

                var n = _tpl.cellSizeXY;
                for (var i = 0; i < _tpl.flatLayout.Length && i < dto.tilePaths.Length; i++)
                {
                    var p = dto.tilePaths[i];
                    if (string.IsNullOrEmpty(p))
                    {
                        _tpl.flatLayout[i] = null;
                    }
                    else
                    {
                        var tile = AssetDatabase.LoadAssetAtPath<TileBase>(p);
                        if (tile == null)
                            Debug.LogWarning($"Missing tile at path: {p}");
                        _tpl.flatLayout[i] = tile;
                    }
                }

                EditorUtility.SetDirty(_tpl);
                Debug.Log($"Imported {_tpl.name} from {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Import failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void RecordUndo(string s)
        {
            Undo.RecordObject(_tpl, s);
        }
    }
}
#endif
