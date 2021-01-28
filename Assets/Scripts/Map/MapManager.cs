using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathing;
using UnityEngine.Rendering;
namespace Map
{
    [RequireComponent(typeof(Grid)), SelectionBase, DefaultExecutionOrder(-1000)]
    public class MapManager : MonoBehaviour
    {
        #region Properties

        public static readonly Vector3Int[] TileCoordinates = new Vector3Int[6]
        {
            new Vector3Int(1,0,0),
            new Vector3Int(1,0,1),
            new Vector3Int(0,0,1),
            new Vector3Int(-1,0,0),
            new Vector3Int(0 ,0, -1),
            new Vector3Int(1 ,0,-1)
        };
        private const string HASH_TILEROOT = "[Tiles]";

        /// <summary>
        /// Camera lookin at the map
        /// </summary>
        public Camera MapCamera
        {
            get
            {
                if (mapCamera == null)
                {
                    mapCamera = GameObject.FindObjectOfType<Camera>();
                }
                return mapCamera;
            }
        }
        private Camera mapCamera = null;

        /// <summary>
        /// Each row has <see cref="gridSize"/> amount of <see cref="MapTile"/>(s)
        /// </summary>
        public List<MapRow> TileRows
        {
            get => tileRows;
            protected set => tileRows = value;
        }
        private List<MapRow> tileRows = new List<MapRow>();

        /// <summary>
        /// The Root Transform where the <see cref="MapTile"/>(s) are stored, Generated dynamically.
        /// </summary>
        public Transform TileRoot
        {
            get
            {
                if (tileRoot == null)
                {
                    tileRoot = transform.Find(HASH_TILEROOT);
                    if (tileRoot == null)
                    {
                        var newRoot = new GameObject(HASH_TILEROOT);
                        newRoot.transform.SetParent(transform);
                        newRoot.transform.localPosition = Vector3.zero;
                        newRoot.transform.localRotation = Quaternion.identity;
                        newRoot.transform.localScale = Vector3.one;
                        tileRoot = newRoot.transform;
                    }
                }
                return tileRoot;
            }
        }
        private Transform tileRoot = null;

        /// <summary>
        /// The Grid spawned with Manager
        /// </summary>
        public Grid TileGrid
        {
            get
            {
                if (tileGrid == null)
                    tileGrid = transform.GetComponent<Grid>();
                return tileGrid;
            }
        }
        private Grid tileGrid = null;
        public Color SelectColor
        {
            get => selectColor;
        }
        public Color PathColor
        {
            get => pathColor;
        }
        public Color HoverColor
        {
            get => hoverColor;
        }
        public Color InvalidColor
        {
            get => invalidColor;
        }
        public Color DefaultColor
        {
            get => defaultColor;
        }
        public float AnimationRate
        {
            get => animationRate;
        }
        public MapTile CurrentGoal { get; private set; } = null;
        public MapTile CurrentOrigin { get; private set; } = null;
        public MapTile CurrentHovered { get; private set; } = null;

        #endregion
        #region Serialized

        [SerializeField, ColorUsage(true, true)]
        private Color selectColor = Color.red;
        [SerializeField, ColorUsage(true, true)]
        private Color pathColor = Color.green;
        [SerializeField, ColorUsage(true, true)]
        private Color hoverColor = Color.magenta;
        [SerializeField, ColorUsage(true, true)]
        private Color invalidColor = Color.grey;
        [SerializeField, ColorUsage(true, true)]
        private Color defaultColor = Color.white;
        [SerializeField,Min(0f), Tooltip("Default: 12, 0 = no animation, will make the colors lerp all cool like")]
        private float animationRate = 12;
        [SerializeField, Tooltip("Default: True, Will Generate a mnew map during OnEnable")]
        private bool generateOnEnable = true;
        [SerializeField, Tooltip("The Tiles to spawn on the map selected by random during Generation")]
        private List<MapTile> tilePrefabs = new List<MapTile>();
        [SerializeField]
        private Vector2Int gridSize = new Vector2Int(8, 8);

        #endregion
        #region Private

        private bool isLeftDown = false;
        private bool isRightDown = false;
        private int clickStateLeft = 0;
        private int clickStateRight = 0;
        private Vector3 lastTilePosition = Vector3.zero;
        private Vector3Int lastTileCoordinate = Vector3Int.zero;
        private Ray mapRay;
        private RaycastHit mapHit;
        private MapTile tileHit = null;
        private bool isPathCalculated = false;
        private float lastPathStep = 0f;
        private List<MapTile> pathTarget = new List<MapTile>();
        private List<MapTile> pathCurrent = new List<MapTile>();
        private List<MapTile> pathCache = new List<MapTile>();

        #endregion
        #region MonoBehaviours

        private void OnEnable()
        {
            if (generateOnEnable)
                Generate();
        }
        private void Update()
        {
            UpdateInput();
            UpdatePath();
        }
        private void FixedUpdate()
        {
            UpdateSelection();
        }
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying) return;
            if (tilePrefabs.Count < 1 || tilePrefabs[0] == null) return;
            if (DebugMesh == null) return;
            Gizmos.color = selectColor;
            if (ValidateComponents())
                DrawMapPreview(gridSize.x, gridSize.y);
        }

        #endregion
        #region Public Tile Logic

        /// <summary>
        /// Sets the given <see cref="MapTile"/> as Tile being Hovered
        /// </summary>
        public void SetHoverTile(MapTile tile_)
        {

            if (CurrentHovered != null && !IsTileSelected(CurrentHovered))
            {
                OnTileReset(CurrentHovered);
            }
            CurrentHovered = tile_;
            if (!IsTileSelected(CurrentHovered))
                OnTileHover(CurrentHovered);
        }

        /// <summary>
        /// Sets the <see cref="MapTile"/> as the Origin
        /// Path is calculated if the Goal was already set
        /// </summary>
        public bool SetOriginTile(MapTile tile_)
        {
            //Debug.Log(string.Format("ORIGIN_SET: {0}", tile_.name));
            ResetOriginTile();
            CurrentOrigin = tile_;
            OnTileSelect(CurrentOrigin);
            ClearPath();
            return true;
        }

        /// <summary>
        /// Sets the Path list to be highlighted
        /// </summary>
        public void SetPathTiles(IList<IAStarNode> tiles_)
        {
            for (int i = 0; i < tiles_.Count; i++)
            {
                var tile = (MapTile)tiles_[i];
                pathTarget.Add(tile);
            }
        }

        /// <summary>
        /// Clears the highliched path
        /// </summary>
        public void ResetHighlightedTiles()
        {
            if (pathCurrent.Count > 0)
            {
                for (int i = 0; i < pathCurrent.Count; i++)
                {
                    if (pathCurrent[i] != null)
                        OnTileReset(pathCurrent[i]);
                }
                pathCurrent.Clear();
            }
        }

        /// <summary>
        /// Clears the Origin <see cref="MapTile"/> if there was one
        /// </summary>
        public void ResetOriginTile()
        {
            if (CurrentOrigin != null)
                OnTileReset(CurrentOrigin);
            CurrentOrigin = null;
        }

        /// <summary>
        /// Sets the <see cref="MapTile"/> as the Origin
        /// Path is calculated if the Goal was already set
        /// </summary>
        public bool SetGoalTile(MapTile tile_)
        {
            //Debug.Log(string.Format("GOAL_SET: {0}", tile_.name));
            ResetGoalTile();
            CurrentGoal = tile_;
            OnTileSelect(CurrentGoal);
            ClearPath();
            return true;
        }

        /// <summary>
        /// Clears the Goal <see cref="MapTile"/> if there was one
        /// </summary>
        public void ResetGoalTile()
        {
            if (CurrentGoal != null)
                OnTileReset(CurrentGoal);
            CurrentGoal = null;
        }

        /// <summary>
        /// True if the tile is selected already by the map manager
        /// A Tile is selected if the tile is highlighted as a path, is the origin, or is the goal
        /// </summary>
        public bool IsTileSelected(MapTile tile_)
        {
            if (CurrentOrigin != null && CurrentOrigin.gameObject.GetInstanceID() == tile_.gameObject.GetInstanceID()) return true;
            if (CurrentGoal != null && CurrentGoal.gameObject.GetInstanceID() == tile_.gameObject.GetInstanceID()) return true;
            if (pathCurrent.Contains(tile_)) return true;
            return false;
        }

        /// <summary>
        /// True if the given <see cref="MapTile"/> is not water
        /// </summary>
        public bool IsTileWalkable(MapTile tile_)
        {
            return tile_.TileType != MapTileType.Water;
        }

        /// <summary>
        /// Returns a <see cref="MapTile"/> at the given Coordinates
        /// Null if no Tile was found or out of range
        /// </summary>
        public MapTile GetTileAtCoordinate(Vector3Int coordinate_)
        {
            if (coordinate_.z < 0 || coordinate_.z >= TileRows.Count) return null;
            if (coordinate_.x < 0 || coordinate_.x >= TileRows[coordinate_.z].Tiles.Count) return null;
            return TileRows[coordinate_.z].Tiles[coordinate_.x];
        }

        /// <summary>
        /// Returns the world position of a <see cref="MapTile"/> on the given <see cref="Grid"/>
        /// </summary>
        public Vector3 GetTilePosition(Vector3Int coordinates_, Grid grid_)
        {
            return GetTilePosition(coordinates_.x, coordinates_.z, grid_);
        }

        #endregion
        #region Private Tile Logic

        private void UpdateInput()
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                Generate();
            }
            if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete))
            {
                Clear();
            }
            if (Input.GetMouseButtonDown(0))
            {
                isLeftDown = true;
            }
            if (Input.GetMouseButtonDown(1))
            {
                isRightDown = true;
            }
        }
        private void UpdatePath()
        {
            if (Time.timeSinceLevelLoad < lastPathStep) return;
            if(pathCache.Count > 0)
            {
                var cachedTile = pathCache[0];
                OnTileReset(cachedTile);
                pathCache.RemoveAt(0);
            }
            if (!isPathCalculated)
            {
                if (CurrentOrigin == null || CurrentGoal == null) return;
                if(pathTarget.Count < 1)//No path to render yet
                {
                    //Debug.Log(string.Format("Calculating Path: Origin={0} Goal={1}", CurrentOrigin == null ? "NULL" : CurrentOrigin.name, CurrentGoal == null ? "NULL" : CurrentGoal.name));
                    var newPath = AStar.GetPath(CurrentOrigin, CurrentGoal);
                    if (newPath != null && newPath.Count > 0)
                    {
                        SetPathTiles(newPath);
                    }
                }
                else//We have a target path to render
                {
                    int index = originFirst ? 0 : pathTarget.Count - 1;
                    var targetTile = pathTarget[index];//Lets empty the list like bullets in a clip
                    OnTileHighlight(targetTile);//Set the color before adding to current path is important
                    pathCurrent.Add(targetTile);//Add to current path
                    pathTarget.RemoveAt(index);
                    if(pathTarget.Count == 0)//Clip empty
                        isPathCalculated = true;
                }
            }
            lastPathStep = Time.timeSinceLevelLoad + Time.deltaTime;
        }
        private void UpdateSelection()
        {
            if (MapCamera == null) return;
            mapRay = MapCamera.ScreenPointToRay(Input.mousePosition);
            tileHit = null;
            if (Physics.Raycast(mapRay, out mapHit, float.PositiveInfinity))
            {
                tileHit = mapHit.transform.GetComponentInParent<MapTile>();
                if (tileHit != null)
                {
                    SetHoverTile(tileHit);
                }
            }
            if (isRightDown)
            {
                OnRightClick();
                isRightDown = false;
            }
            if (isLeftDown)
            {
                OnLeftClick();
                isLeftDown = false;
            }
        }
        private void ClearPath()
        {
            for (int i = 0; i < pathCurrent.Count; i--)
            {
                pathCache.Add(pathCurrent[i]);
            }
            pathTarget.Clear();
            pathCurrent.Clear();
            isPathCalculated = false;
        }
        private bool originFirst = false;
        private void OnLeftClick()
        {
            if (CurrentHovered == null) return;
            if (!IsTileWalkable(CurrentHovered)) return;//Cant walk on water
            switch (clickStateLeft)
            {
                case 0://Origin State
                    if (clickStateRight == 2)//Origin + Goal already set
                    {
                        ResetHighlightedTiles();
                        ResetOriginTile();
                        ResetGoalTile();
                        clickStateRight = 0;
                    }
                    if (SetOriginTile(CurrentHovered))
                    {
                        if (clickStateRight == 1)//Goal was already set
                        {
                            clickStateLeft = 2;//Move to Reset state
                            clickStateRight = 0;//Prep right State for dynamic recover
                        }
                        else//Origin is only tile set
                        {
                            clickStateLeft = 1;
                        }
                    }
                    break;
                case 1://Goal State
                    if (SetGoalTile(CurrentHovered))
                        clickStateLeft = 2;
                    break;
                case 2://Reset State
                    ResetHighlightedTiles();
                    ResetGoalTile();
                    ResetOriginTile();
                    clickStateLeft = 0;
                    clickStateRight = 0;
                    break;
            }
            originFirst = true;
        }
        private void OnRightClick()
        {
            if (CurrentHovered == null) return;
            if (!IsTileWalkable(CurrentHovered)) return;
            switch (clickStateRight)
            {
                case 0:
                    if (clickStateLeft == 2)
                    {
                        ResetHighlightedTiles();
                        ResetOriginTile();
                        ResetGoalTile();
                        clickStateLeft = 0;
                    }
                    if (SetGoalTile(CurrentHovered))
                    {
                        if (clickStateLeft == 1)
                        {
                            clickStateRight = 2;
                            clickStateLeft = 0;
                        }
                        else
                        {
                            clickStateRight = 1;
                        }
                    }
                    break;
                case 1:
                    if (SetOriginTile(CurrentHovered))
                        clickStateRight = 2;
                    break;
                case 2:
                    ResetGoalTile();
                    ResetOriginTile();
                    ResetHighlightedTiles();
                    clickStateLeft = 0;
                    clickStateRight = 0;
                    break;
            }
            originFirst = false;
        }
        private void OnTileHover(MapTile tile_)
        {
            if (tile_.TileType == MapTileType.Water)
                tile_.SetTileColor(invalidColor);
            else
                tile_.SetTileColor(hoverColor);
        }
        private void OnTileSelect(MapTile tile_)
        {
            tile_.SetTileColor(selectColor);
        }
        private void OnTileHighlight(MapTile tile_)
        {
            if (!IsTileSelected(tile_))
                tile_.SetTileColor(pathColor);
        }
        private void OnTileReset(MapTile tile_)
        {
            tile_.SetTileColor(defaultColor);
        }
        private Vector3 GetTilePosition(int x_, int z_, Grid grid_)
        {
            lastTileCoordinate.x = x_;
            lastTileCoordinate.z = z_;
            lastTilePosition = grid_.CellToWorld(lastTileCoordinate);
            lastTilePosition.x += (grid_.cellSize.x + (z_ & 1)) / 2;
            lastTilePosition.z -= z_ * (grid_.cellSize.z / 4);
            return lastTilePosition;
        }
        private MapTile GenerateTile(Transform parent_)
        {
            int tileID = Random.Range(0, tilePrefabs.Count);
            var tileGO = GameObject.Instantiate(tilePrefabs[tileID]);
            tileGO.name = tilePrefabs[tileID].name;
            tileGO.transform.SetParent(parent_);
            tileGO.transform.localPosition = Vector3.zero;
            tileGO.transform.localRotation = Quaternion.identity;
            tileGO.transform.localScale = Vector3.one;
            return tileGO.GetComponent<MapTile>();
        }

        #endregion
        #region Map Logic

        /// <summary>
        /// Does a Validation Check on Map Components
        /// Main Generate Function, wipes the map clean and Generates a new Layout
        /// </summary>
        public void Generate()
        {
            if (ValidateComponents())
                GenerateMap(gridSize.x, gridSize.y, TileRoot);
        }

        /// <summary>
        /// Main Clear Function, wipes the Map clean, Destroying all the <see cref="MapTile"/>(s) under the <see cref="TileRoot"/>
        /// </summary>
        public void Clear()
        {
            if (TileRoot == null) return;
            ClearMap(TileRoot);
        }
        private void ClearMap(Transform parent_)
        {
            if (TileRows.Count > 0)
            {
                TileRows.Clear();
            }
            if (parent_.childCount < 1) return;
            for (int i = parent_.childCount - 1; i >= 0; i--)
            {
                var target = parent_.GetChild(i);
                if (target != null)
                {
#if UNITY_EDITOR
                    if (Application.isPlaying)
                        Destroy(target.gameObject);
                    else
                        DestroyImmediate(target.gameObject);
#else
                    Destroy(target.gameObject);
#endif
                }
            }
        }
        private void GenerateMap(int xSize_, int ySize_, Transform parent_)
        {
            ClearMap(parent_);
            for (int y = 0; y < ySize_; y++)
            {
                MapRow newRow = new MapRow();
                for (int x = 0; x < xSize_; x++)
                {
                    MapTile newTile = GenerateTile(parent_);
                    newTile.Coordinates.x = x;
                    newTile.Coordinates.z = y;
                    newTile.Map = this;
                    newTile.Position = GetTilePosition(newTile.Coordinates, tileGrid);
                    newTile.gameObject.name = string.Format("{0} ({1},{2})", newTile.TileType.ToString(), x, y);
                    newTile.ResetTileColor(DefaultColor);
                    newRow.Tiles.Add(newTile);
                }
                TileRows.Add(newRow);
            }
        }
        private bool ValidateComponents()
        {
            if (TileGrid == null)
                return false;

            if (tilePrefabs.Count < 1)
            {
                Debug.LogError(string.Format("Null Tile Prefabs in MapManager({0})", gameObject.name), gameObject);
                return false;
            }
            else
            {
                for (int i = 0; i < tilePrefabs.Count; i++)
                {
                    if (tilePrefabs[i] == null)
                    {
                        Debug.LogError(string.Format("Null Tile Prefabs in MapManager({0}): Index:{1}", gameObject.name, i.ToString()), gameObject);
                        return false;
                    }
                }
            }
            if (gridSize.x < 1)
                gridSize.x = 1;
            if (gridSize.y < 1)
                gridSize.y = 1;
            return true;
        }

        #endregion
        #region Gizmos

        public Mesh DebugMesh
        {
            get => debugMesh;
        }
        [SerializeField, Tooltip("When not null, Tile Gizmos will show")]
        private Mesh debugMesh = null;

        private void DrawMapPreview(int xSize_, int ySize_)
        {
            for (int y = 0; y < ySize_; y++)
            {
                for (int x = 0; x < xSize_; x++)
                {
                    Gizmos.DrawWireMesh(DebugMesh, GetTilePosition(x, y, TileGrid), TileRoot.rotation, Vector3.one);
                }
            }
        }

        #endregion
    }
    [System.Serializable]
    public class MapRow
    {
        public List<MapTile> Tiles = new List<MapTile>();
    }

}