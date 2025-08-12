#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Dungonator.Client.Layers.Maze.MazeCellTemplates;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Editor
{
    [CustomEditor(typeof(MazeCellTemplate))]
    public class MazeCellTemplateEditor : UnityEditor.Editor
    {
        // --- COMPAT: your saved JSONs use row0 = TOP ---
        private const bool JSON_ROW0_IS_TOP = true;

        [Serializable]
        private class TemplateJson { public string[] tilePaths; public int size; }

        private MazeCellTemplate _tpl;
        private TileBase _paintTile;
        private bool _paintContinuous;
        private bool _showGrid = true;
        private int _previewPx = 48;
        private Vector2 _scroll;
        private readonly Color _gridLine = new Color(1,1,1,0.12f);

        private void OnEnable() { _tpl = (MazeCellTemplate)target; EnsureArraySize(); }

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

        private new void DrawHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var shapeProp = serializedObject.FindProperty("shape");
                var sizeProp  = serializedObject.FindProperty("cellSizeXY");
                var thicknessProp = serializedObject.FindProperty("wallThickness");

                EditorGUILayout.LabelField("Maze Cell Template", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(shapeProp);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(sizeProp, new GUIContent("Cell Size (X=Y)"));
                    if (GUILayout.Button("Resize", GUILayout.Width(70)))
                        ResizeGrid(sizeProp.intValue);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(thicknessProp, new GUIContent("Wall Thickness"));
                }

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

                EditorGUILayout.HelpBox("Storage = row0 bottom (matches MazeDrawer). JSON import/export stays compatible with old files (row0 top).", MessageType.Info);
            }
        }

        private void DrawTopBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Fill"))
                {
                    Undo.RecordObject(_tpl, "Fill");
                    for (int i = 0; i < _tpl.flatLayout.Length; i++) _tpl.flatLayout[i] = _paintTile;
                    EditorUtility.SetDirty(_tpl);
                }

                if (GUILayout.Button("Clear"))
                {
                    Undo.RecordObject(_tpl, "Clear");
                    Array.Clear(_tpl.flatLayout, 0, _tpl.flatLayout.Length);
                    EditorUtility.SetDirty(_tpl);
                }

                if (GUILayout.Button("Rotate 90°")) { Undo.RecordObject(_tpl, "Rotate 90°"); Rotate90(); EditorUtility.SetDirty(_tpl); }
                if (GUILayout.Button("Flip H"))      { Undo.RecordObject(_tpl, "Flip H");     FlipH();   EditorUtility.SetDirty(_tpl); }
                if (GUILayout.Button("Flip V"))      { Undo.RecordObject(_tpl, "Flip V");     FlipV();   EditorUtility.SetDirty(_tpl); }

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawGridArea()
        {
            EnsureArraySize();
            int size = Mathf.Max(1, _tpl.cellSizeXY);
            int totalPx = size * _previewPx;

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(Mathf.Min(512, totalPx + 12)));
            var rect = GUILayoutUtility.GetRect(totalPx, totalPx, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.14f,0.14f,0.14f) : new Color(0.9f,0.9f,0.9f));

            var evt = Event.current;
            bool inGrid = rect.Contains(evt.mousePosition);

            // y=0 is BOTTOM internally; draw it at the bottom of the preview
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var cellRect = new Rect(rect.x + x * _previewPx, rect.y + (size - 1 - y) * _previewPx, _previewPx, _previewPx);
                DrawChecker(cellRect);
                DrawTilePreview(cellRect, _tpl.GetTile(x, y));
                if (_showGrid) DrawBorder(cellRect, _gridLine);
                if (inGrid && cellRect.Contains(evt.mousePosition)) HandlePainting(evt, x, y);
            }

            if (inGrid && evt.type == EventType.MouseDrag) Repaint();
            EditorGUILayout.EndScrollView();

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
                if (GUILayout.Button("Validate / Fix Array")) { Undo.RecordObject(_tpl, "Validate"); EnsureArraySize(); EditorUtility.SetDirty(_tpl); }

                if (GUILayout.Button("Ping Used Tiles"))
                {
                    var paths = _tpl.flatLayout.Where(t => t != null).Select(AssetDatabase.GetAssetPath).Distinct().OrderBy(p => p).ToArray();
                    if (paths.Length == 0) Debug.Log("No tiles in template.");
                    else foreach (var p in paths) Debug.Log(p, AssetDatabase.LoadAssetAtPath<TileBase>(p));
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
                    if (GUILayout.Button("Export JSON")) ExportJson();
                    if (GUILayout.Button("Import JSON")) ImportJson();
                }

                EditorGUILayout.HelpBox(
                    "JSON stays compatible with old files (row0 = TOP).\nInternally we store row0 = BOTTOM for MazeDrawer.",
                    MessageType.None);
            }
        }

        // ----- Painting helpers (unchanged) -----
        private void HandlePainting(Event evt, int x, int y)
        {
            bool erase = evt.alt || evt.shift;
            if (evt.type == EventType.MouseDown && (evt.button == 0 || evt.button == 1))
            {
                _paintContinuous = true;
                GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
                evt.Use();

                if (evt.button == 1) { _paintTile = _tpl.GetTile(x, y); Repaint(); return; }

                Undo.RecordObject(_tpl, "Paint Tile");
                _tpl.SetTile(x, y, erase ? null : _paintTile);
                EditorUtility.SetDirty(_tpl);
                Repaint();
            }
            else if (evt.type == EventType.MouseDrag && _paintContinuous && evt.button == 0)
            {
                Undo.RecordObject(_tpl, "Paint Tile");
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
            if (!tile) return;
            Texture2D tex = null; Rect uv = default;
            if (tile is Tile concrete && concrete.sprite && concrete.sprite.texture)
            {
                var sp = concrete.sprite; tex = sp.texture;
                uv = new Rect(sp.rect.x/tex.width, sp.rect.y/tex.height, sp.rect.width/tex.width, sp.rect.height/tex.height);
            }
            else { tex = AssetPreview.GetAssetPreview(tile) ?? AssetPreview.GetMiniThumbnail(tile); uv = new Rect(0,0,1,1); }
            if (tex) GUI.DrawTextureWithTexCoords(r, tex, uv, true);
        }
        private void DrawChecker(Rect r)
        {
            var c1 = new Color(0,0,0,0.10f); var c2 = new Color(1,1,1,0.10f); float half = r.width/2f;
            EditorGUI.DrawRect(new Rect(r.x, r.y, half, half), c1);
            EditorGUI.DrawRect(new Rect(r.x+half, r.y, half, half), c2);
            EditorGUI.DrawRect(new Rect(r.x, r.y+half, half, half), c2);
            EditorGUI.DrawRect(new Rect(r.x+half, r.y+half, half, half), c1);
        }
        private void DrawBorder(Rect r, Color color)
        { Handles.BeginGUI(); Handles.color = color; Handles.DrawAAPolyLine(1.5f, new Vector3(r.x,r.y), new Vector3(r.xMax,r.y), new Vector3(r.xMax,r.yMax), new Vector3(r.x,r.yMax), new Vector3(r.x,r.y)); Handles.EndGUI(); }

        // ----- Array ops -----
        private void EnsureArraySize()
        {
            var sizeProp  = serializedObject.FindProperty("cellSizeXY");
            var layoutProp = serializedObject.FindProperty("flatLayout");
            int size = Mathf.Max(1, sizeProp.intValue), needed = size * size;
            if (layoutProp != null && layoutProp.arraySize != needed)
            {
                layoutProp.arraySize = needed;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(_tpl);
            }
        }

        private void ResizeGrid(int newSize)
        {
            newSize = Mathf.Max(1, newSize);
            if (newSize == _tpl.cellSizeXY) { EnsureArraySize(); return; }

            Undo.RecordObject(_tpl, "Resize Grid");
            int oldSize = _tpl.cellSizeXY;
            var old = _tpl.flatLayout;

            _tpl.cellSizeXY = newSize;
            _tpl.flatLayout = new TileBase[newSize * newSize];

            int copy = Mathf.Min(oldSize, newSize);
            for (int y = 0; y < copy; y++)
            for (int x = 0; x < copy; x++)
                _tpl.SetTile(x, y, old[y * oldSize + x]);

            EditorUtility.SetDirty(_tpl);
        }

        private void Rotate90()
        {
            int n = _tpl.cellSizeXY; var src = (TileBase[])_tpl.flatLayout.Clone();
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                _tpl.flatLayout[y * n + x] = src[(y) * n + (n - 1 - x)]; // new(x,y)=old(y,n-1-x)
        }
        private void FlipH()
        {
            int n = _tpl.cellSizeXY; var src = (TileBase[])_tpl.flatLayout.Clone();
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                _tpl.flatLayout[y * n + x] = src[y * n + (n - 1 - x)];
        }
        private void FlipV()
        {
            int n = _tpl.cellSizeXY; var src = (TileBase[])_tpl.flatLayout.Clone();
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                _tpl.flatLayout[y * n + x] = src[(n - 1 - y) * n + x];
        }

        // ----- JSON I/O (compat layer) -----
        private void ExportJson()
        {
            var path = EditorUtility.SaveFilePanel("Export MazeCellTemplate to JSON", Application.dataPath, $"{_tpl.name}.json", "json");
            if (string.IsNullOrEmpty(path)) return;

            int size = Mathf.Max(1, _tpl.cellSizeXY);
            var arr = new string[size * size];

            // INTERNAL y=0 is bottom. JSON wants yTop=0 at top.
            for (int yBottom = 0; yBottom < size; yBottom++)
            for (int x = 0; x < size; x++)
            {
                int yTop = JSON_ROW0_IS_TOP ? (size - 1 - yBottom) : yBottom;
                var t = _tpl.GetTile(x, yBottom);
                arr[yTop * size + x] = t ? AssetDatabase.GetAssetPath(t) : string.Empty;
            }

            var dto = new TemplateJson { tilePaths = arr, size = size };
            File.WriteAllText(path, JsonUtility.ToJson(dto, true));
            EditorUtility.RevealInFinder(path);
            Debug.Log($"Exported {_tpl.name} → {path}");
        }

        private void ImportJson()
        {
            var path = EditorUtility.OpenFilePanel("Import MazeCellTemplate from JSON", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var dto = JsonUtility.FromJson<TemplateJson>(File.ReadAllText(path));
                if (dto == null || dto.tilePaths == null) { EditorUtility.DisplayDialog("Import JSON", "Invalid JSON format.", "OK"); return; }
                if (dto.size <= 0) { EditorUtility.DisplayDialog("Import JSON", "Invalid 'size' in JSON.", "OK"); return; }

                if (dto.tilePaths.Length != dto.size * dto.size)
                {
                    if (!EditorUtility.DisplayDialog("Import JSON",
                        $"tilePaths length ({dto.tilePaths.Length}) != size*size ({dto.size * dto.size}). Continue and clamp?",
                        "Continue", "Cancel")) return;
                }

                Undo.RecordObject(_tpl, "Import JSON");
                ResizeGrid(dto.size);

                int n = _tpl.cellSizeXY;
                for (int i = 0; i < _tpl.flatLayout.Length && i < dto.tilePaths.Length; i++)
                {
                    int x = i % n;
                    int yTop = i / n;
                    int yBottom = JSON_ROW0_IS_TOP ? (n - 1 - yTop) : yTop; // flip into internal storage

                    var p = dto.tilePaths[i];
                    if (string.IsNullOrEmpty(p)) { _tpl.SetTile(x, yBottom, null); continue; }

                    var tile = AssetDatabase.LoadAssetAtPath<TileBase>(p);
                    if (tile == null) Debug.LogWarning($"Missing tile at path: {p}");
                    _tpl.SetTile(x, yBottom, tile);
                }

                EditorUtility.SetDirty(_tpl);
                Debug.Log($"Imported {_tpl.name} from {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Import failed: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
#endif
