using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Pathing;
namespace Map
{
    [CustomEditor(typeof(MapManager))]
    public class MapManagerEditor : Editor
    {
        public GUIContent GUIContentGenerate
        {
            get
            {
                if (guiContentGenerate == null)
                {
                    if (guiIconGenerate == null)
                        guiIconGenerate = EditorGUIUtility.FindTexture("Animation.Record");
                    guiContentGenerate = new GUIContent("Generate",guiIconGenerate, "Generate new map");
                }
                return guiContentGenerate;
            }
        }
        public GUIContent GUIContentClear
        {
            get
            {
                if (guiContentClear == null)
                {
                    if (guiIconClear == null)
                        guiIconClear = EditorGUIUtility.FindTexture("winbtn_win_close");
                    guiContentClear = new GUIContent("Clear",guiIconClear, "Clear the map");
                }
                return guiContentClear;
            }
        }
        public GUIContent GUIContentGizmos
        {
            get
            {
                if (guiContentGizmos == null)
                {
                    if (guiIconGizmos == null)
                        guiIconGizmos = EditorGUIUtility.FindTexture("animationvisibilitytoggleon");
                    guiContentGizmos = new GUIContent("Show Neighbours", guiIconGizmos, "Displays all Neighbours for each tile selected");
                }
                return guiContentGizmos;
            }
        }

        public static bool showGizmos = false;
        private bool isPathCalculated = false;
        private MapManager map = null;
        private GUIContent guiContentClear;
        private GUIContent guiContentGizmos;
        private GUIContent guiContentGenerate;
        private Texture guiIconClear = null;
        private Texture guiIconGizmos = null;
        private Texture guiIconGenerate = null;
        private Vector2 mousePosition = Vector2.zero;
        private RaycastHit hoverCast;
        private Camera sceneCamera = null;
        private MapTile goalTile = null;
        private MapTile originTile = null;
        private MapTile targetHoverTile = null;
        private MapTile currentHoverTile = null;
        private MapManager lastMapSelected = null;
        private List<MapTile> tilesHighLighted = new List<MapTile>();

        private void OnDisable()
        {
            showGizmos = false;
            if(!Application.isPlaying)
            ClearTiles();
        }
        private void OnSceneGUI()
        {
            EditorGUI.BeginChangeCheck();
            if (map == null)
                map = (MapManager)target;
            GUI.backgroundColor = Color.magenta;
            if (Event.current.type == EventType.Repaint) return;
            GUILayout.Window(0, new Rect(SceneView.lastActiveSceneView.position.width - 210, 150, 200, 400), (id) =>
            {
                GUILayout.Space(50);
                if (GUILayout.Button(GUIContentGenerate))
                {
                    map.Generate();
                }
                if (GUILayout.Button(GUIContentClear))
                {
                    map.Clear();
                }
                GUIContentGizmos.text = showGizmos ? " Hide Neighbours" : " Show Neighbours";
                if (GUILayout.Button(GUIContentGizmos))
                {
                    showGizmos = !showGizmos;
                }
                GUILayout.Space(10f);
                ShowTileInfo("-Selected-",Application.isPlaying? map.CurrentHovered : currentHoverTile);
                GUILayout.Space(10f);
                ShowTileInfo("-Origin-", Application.isPlaying ? map.CurrentOrigin : originTile);
                GUILayout.Space(10f);
                ShowTileInfo("-Goal-", Application.isPlaying ? map.CurrentGoal : goalTile);
            }, "Denchos\nMap Generator");

            if (Application.isPlaying) return;
            UpdateTileSelection();
            UpdatePath();
        }
        private void UpdateTileSelection()
        {
            if(sceneCamera == null)
            {
                var camera = SceneView.GetAllSceneCameras();
                if (camera != null)
                    sceneCamera = camera[0];
                return;
            }
            mousePosition = Event.current.mousePosition;
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            mousePosition = ray.origin;
            if (Physics.Raycast(ray, out hoverCast, float.PositiveInfinity))
            {
                currentHoverTile = hoverCast.collider.GetComponentInParent<MapTile>();
            }
            else
            {
                currentHoverTile = null;
            }
            if (currentHoverTile != null)
            {
                float size = GetCellSize(currentHoverTile);
                Handles.color = Color.clear;
                if (Handles.Button(currentHoverTile.transform.position + Vector3.up * 0.15f, Quaternion.AngleAxis(90, Vector3.right), 0.5f, 2f, Handles.CircleHandleCap))
                {
                    if (currentHoverTile.TileType == MapTileType.Water) return;
                    lastMapSelected = currentHoverTile.Map;
                    Selection.activeObject = lastMapSelected;
                    if (originTile != null && goalTile != null)
                    {
                        OnTileReset(originTile);
                        OnTileReset(goalTile);
                        ResetHighlightedTiles();
                        originTile = null;
                        goalTile = null;
                        return;
                    }
                    if (originTile == null)
                    {
                        originTile = currentHoverTile;
                        OnTileSelect(originTile);
                        isPathCalculated = false;
                        return;
                    }
                    if (goalTile == null)
                    {
                        goalTile = currentHoverTile;
                        OnTileSelect(goalTile);
                        isPathCalculated = false;
                        return;
                    }
                }
                if (currentHoverTile != targetHoverTile)
                {
                    if (targetHoverTile != null && !IsTileSelected(targetHoverTile))
                        OnTileReset(targetHoverTile);
                    if (!IsTileSelected(currentHoverTile))
                        OnTileHover(currentHoverTile);
                    targetHoverTile = currentHoverTile;
                }
            }
            else
            {
                if (targetHoverTile != null && targetHoverTile != originTile && targetHoverTile != goalTile)
                {
                    if (!IsTileSelected(targetHoverTile))
                    {
                        OnTileReset(targetHoverTile);
                    }

                    targetHoverTile = null;
                }
            }
            
        }
        private void UpdatePath()
        {
            if (!isPathCalculated)
            {
                //Debug.Log(string.Format("Calculating Path: Origin={0} Goal={1}", originTile == null ? "NULL" : originTile.name, goalTile == null ? "NULL" : goalTile.name));
                if (originTile == null || goalTile == null) return;
                var newPath = AStar.GetPath(originTile, goalTile);
                if (newPath != null&&newPath.Count > 0)
                {
                    SetHighlightedTiles(newPath);
                }
                else//Work around for a bug that happens upon editor refresh
                {
                    map.Generate();
                }
                isPathCalculated = true;
            }
        }
        private void ShowTileInfo(string title_,MapTile tile_)
        {
            using (var infoScope = new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(title_, EditorStyles.centeredGreyMiniLabel);
                if(tile_ == null)
                {
                    GUILayout.Label("NULL", EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    GUILayout.Label(string.Format("{0}", tile_.name),EditorStyles.centeredGreyMiniLabel);
                    float cost = tile_.HCost;
                    GUILayout.Label(string.Format("HCost:{0}", cost),EditorStyles.centeredGreyMiniLabel);
                }
            }
        }
        public void SetHighlightedTiles(IList<IAStarNode> tiles_)
        {
            for (int i = 0; i < tiles_.Count; i++)
            {
                var tile = (MapTile)tiles_[i];
                OnTileHighlight(tile);
                tilesHighLighted.Add(tile);
            }
        }
        public void ResetHighlightedTiles()
        {
            if (tilesHighLighted.Count > 0)
            {
                for (int i = tilesHighLighted.Count - 1; i >= 0; i--)
                {
                    var tile = tilesHighLighted[i];
                    tilesHighLighted.RemoveAt(i);
                    if (tile != null)
                        OnTileReset(tile);
                }
            }
            isPathCalculated = false;
        }
        private void OnTileHighlight(MapTile tile_)
        {
            if (!IsTileSelected(tile_))
                tile_.SetTileColor(map.PathColor);
            if (showGizmos && tile_.Map.DebugMesh != null)
                tile_.DebugMesh = tile_.Map.DebugMesh;
        }
        private void ClearTiles()
        {
            if (currentHoverTile != null)
            {
                OnTileReset(currentHoverTile);
                currentHoverTile = null;
            }
            if (originTile != null)
            {
                OnTileReset(originTile);
                originTile = null;
            }
            if (targetHoverTile != null)
            {
                OnTileReset(targetHoverTile);
                targetHoverTile = null;
            }
            if (goalTile != null)
            {
                OnTileReset(goalTile);
                goalTile = null;
            }
            ResetHighlightedTiles();
        }
        private void OnTileSelect(MapTile tile_)
        {
            EditorGUIUtility.PingObject(tile_.gameObject);
            //Selection.activeObject = tile_.Map.gameObject;
            tile_.SetTileColor(map.SelectColor);
            if (showGizmos && tile_.Map.DebugMesh != null)
                tile_.DebugMesh = tile_.Map.DebugMesh;
        }
        private void OnTileHover(MapTile tile_)
        {
            if (tile_.TileType == MapTileType.Water)
                tile_.SetTileColor(map.InvalidColor);
            else
                tile_.SetTileColor(map.HoverColor);
        }
        private void OnTileReset(MapTile tile_)
        {
            tile_.SetTileColor(map.DefaultColor);
            tile_.DebugMesh = null;
        }
        private float GetCellSize(MapTile tile_)
        {
            return tile_.Grid.cellSize.x * 0.5f;
        }
        private bool IsTileSelected(MapTile tile_)
        {
            if (tilesHighLighted.Contains(tile_)) return true;
            if (originTile == tile_) return true;
            if (goalTile == tile_) return true;
            return false;
        }
    }
}