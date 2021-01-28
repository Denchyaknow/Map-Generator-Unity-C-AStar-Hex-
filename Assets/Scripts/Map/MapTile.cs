using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathing;
using System.Linq;
namespace Map
{

    public class MapTile : MonoBehaviour, IAStarNode
    {
        #region Properties

        /// <summary>
        /// Cost of Tile to Goal
        /// </summary>
        public int HCost
        {
            get => hCost = GetHCost();
        }
        private int hCost = 0;
        private int GetHCost()
        {
            return (int)tileType;
        }

        /// <summary>
        /// The Manager that spawned this Tile
        /// </summary>
        public MapManager Map
        {
            get
            {
                if (map == null)
                {
                    map = transform.GetComponentInParent<MapManager>();
                }
                return map;
            }
            set => map = value;
        }
        private MapManager map = null;
        /// <summary>
        /// The Grid that this TIle is on
        /// </summary>
        public Grid Grid
        {
            get
            {
                if (grid == null)
                    grid = transform.GetComponentInParent<Grid>();
                return grid;
            }
        }
        private Grid grid = null;
        /// <summary>
        /// The Coordinates relative to index in the <see cref="MapManager.TileRows"/> list
        /// </summary>
        public Vector3Int Coordinates = new Vector3Int();
        public Vector3 Position
        {
            get => position;
            set => position = transform.position = value;
        }
        private Vector3 position = Vector2.zero;
        /// <summary>
        /// The current type of this Tile set during prefabbing
        /// </summary>
        public MapTileType TileType
        {
            get => tileType;
        }

        /// <summary>
        /// The thing on this tile that changes the materials color
        /// Looks alot better with Denchos custom shaders :D
        /// </summary>
        private MapTileMaterial Material
        {
            get
            {
                if (material == null)
                    material = transform.GetComponentInChildren<MapTileMaterial>();
                return material;
            }
        }
        private MapTileMaterial material = null;

        public Mesh DebugMesh
        {
            get => debugMesh;
            set => debugMesh = value;
        }
        private Mesh debugMesh = null;

        private Vector3Int lastNeighbourCoordinate = Vector3Int.zero;
        private Vector3 lastEstimatedDirection = Vector3.zero;

        #endregion
        #region AStar Controls

        IEnumerable<IAStarNode> IAStarNode.Neighbours
        {
            get
            {
                for (int i = 0; i < 6; i++)
                {
                    MapTile tile = Map.GetTileAtCoordinate(GetNeighbourCoordinate(i));
                    if (tile != null && tile.tileType != MapTileType.Water)
                    {
                        yield return (IAStarNode)tile;
                    }
                }
            }
        }
        public float CostTo(IAStarNode neighbour)
        {
            var tile = (MapTile)neighbour;
            if (tile != null)
            {
                //Debug.Log(string.Format("Tile Found! CostTo({0})", tile.HCost + HCost));
                return tile.HCost + HCost;
            }
            return HCost;
        }
        public float EstimatedCostTo(IAStarNode goal)
        {
            var tile = (MapTile)goal;
            if (tile != null)
            {
                lastEstimatedDirection.x = Mathf.Abs(tile.transform.position.x - transform.position.x);//Quick distance check
                lastEstimatedDirection.z = Mathf.Abs(tile.transform.position.z - transform.position.z);
            }
            lastEstimatedDirection.y = (lastEstimatedDirection.x + lastEstimatedDirection.z) / 2;//Store distance in unused axis
            //Debug.Log(string.Format("Estimated Cost: {0}", lastEstimatedDirection.y));
            return lastEstimatedDirection.y;
        }

        #endregion

        [SerializeField]
        private MapTileType tileType = MapTileType.Water;

        /// <summary>
        /// Give and index of 0-5 and get that tile
        /// The index represents the side of the hex tile, starting on the right
        /// </summary>
        public Vector3Int GetNeighbourCoordinate(int index_)
        {
            if (index_ < 0 || index_ >= 6) return lastNeighbourCoordinate;//Fail safe
            lastNeighbourCoordinate = Coordinates + MapManager.TileCoordinates[index_];
            if (Coordinates.z % 2 == 0)//Shift over odd rows
                lastNeighbourCoordinate.x -= lastNeighbourCoordinate.z & 1;
            return lastNeighbourCoordinate;
        }
       
        /// <summary>
        /// Change the color of the tile
        /// </summary>
        /// <param name="color_"></param>
        public void SetTileColor(Color color_)
        {
            if (Material != null)
                Material.SetColor(color_);
        }

        /// <summary>
        /// Sets the default and current color of the tile
        /// </summary>
        public void ResetTileColor(Color color_)
        {
            if (Material != null)
                Material.ResetColor(color_);
        }
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying) return;
            if (Map == null || Grid == null) return;
            if (DebugMesh != null)
            {
                Gizmos.color = Color.red;
                for (int i = 0; i < 6; i++)
                {
                    Gizmos.DrawMesh(DebugMesh, Map.GetTilePosition(GetNeighbourCoordinate(i), Grid) + Vector3.up * 0.05f);
                }
            }
        }
    }
    public enum MapTileType
    {
        Grass = 1,
        Desert = 5,
        Mountain = 10,
        Forest = 3,
        Water = -1//Unpassible
    }
}