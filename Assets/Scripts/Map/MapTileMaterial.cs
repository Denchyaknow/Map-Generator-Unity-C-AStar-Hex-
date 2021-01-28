using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Map
{
    [RequireComponent(typeof(MeshRenderer))]
    public class MapTileMaterial : MonoBehaviour
    {
        private const string HASH_MAINCOLOR = "_EmissionColor";
        public MapTile Tile
        {
            get
            {
                if (tile == null)
                    tile = transform.GetComponentInParent<MapTile>();
                return tile;
            }
        }
        private MapTile tile = null;
        public MeshRenderer Mesh
        {
            get
            {
                if (mesh == null)
                    mesh = transform.GetComponent<MeshRenderer>();
                return mesh;
            }
        }
        private MeshRenderer mesh = null;

        public bool isAnimated = true;

        private int colorProperty = Shader.PropertyToID(HASH_MAINCOLOR);
        private bool colorUpdated = false;
        private float colorRate= 0f;
        private float colorLerp = 0f;
        private Color previousColor;
        private Color currentColor;
        private Color targetColor;
        private Material currentMaterial = null;

        [ExecuteAlways]
        private void OnDestroy()
        {
            if (currentMaterial != null)
                Destroy(currentMaterial);
        }
        private void OnDrawGizmos()
        {
            if (Application.isPlaying) return;
            UpdateColor();
        }
        private void Update()
        {
            UpdateColor();
        }
        private void UpdateColor()
        {
            if (!isAnimated) return;
            if (colorLerp > 0f)
            {
                colorLerp -= Time.deltaTime * colorRate;
            }
            previousColor.r = Mathf.Abs(currentColor.r - targetColor.r);
            previousColor.g = Mathf.Abs(currentColor.g - targetColor.g);
            previousColor.b = Mathf.Abs(currentColor.b - targetColor.b);
            previousColor.a = Mathf.Abs(currentColor.a - targetColor.a);
            if(previousColor.r > float.Epsilon)
            {
                currentColor.r = Mathf.MoveTowards(currentColor.r, targetColor.r, Time.deltaTime * Tile.Map.AnimationRate * previousColor.r);
                colorUpdated = true;
            }
            if(previousColor.g > float.Epsilon)
            {
                currentColor.g = Mathf.MoveTowards(currentColor.g, targetColor.g, Time.deltaTime * Tile.Map.AnimationRate * previousColor.g);
                colorUpdated = true;
            }
            if(previousColor.b > float.Epsilon)
            {
                currentColor.b = Mathf.MoveTowards(currentColor.b, targetColor.b, Time.deltaTime * Tile.Map.AnimationRate * previousColor.b);
                colorUpdated = true;
            }
            if(previousColor.a > float.Epsilon)
            {
                currentColor.a = Mathf.MoveTowards(currentColor.a, targetColor.a, Time.deltaTime * Tile.Map.AnimationRate * previousColor.a);
                colorUpdated = true;
            }
            if(colorUpdated)
                ChangeColor(currentColor);
        }
        public void SetColor(Color color_)
        {
            if (Mesh == null) return;
            isAnimated = Tile.Map.AnimationRate > 0f;
            CheckMaterial(ref currentMaterial);
            if (isAnimated)
                FadeColor(color_);
            else
                ChangeColor(color_);
        }
        public void ResetColor(Color color_)
        {
            targetColor = Tile.Map.DefaultColor;
            currentColor = Tile.Map.DefaultColor;
            CheckMaterial(ref currentMaterial);
            currentMaterial.SetColor(colorProperty, color_);
        }
        private void FadeColor(Color color_)
        {
            targetColor = color_;
        }
        private void ChangeColor(Color color_)
        {
           currentMaterial.SetColor(colorProperty,color_);
        }
        private void CheckMaterial(ref Material mat_)
        {
            if (currentMaterial == null)
            {
#if UNITY_EDITOR
                currentMaterial = Material.Instantiate(Mesh.sharedMaterial);
                Mesh.material = currentMaterial;
#else
                currentMaterial = Mesh.material;
#endif
            }
        }
    }

}