using System;
using Dungonator.Shared.Player;
using UnityEngine;

namespace Dungonator.Client.Shaders.FogOfWar
{
    public class FogOfWarManager : MonoBehaviour
    {
        private static readonly int FogTexture = Shader.PropertyToID("_FogTexture");
        private static readonly int WorldSize = Shader.PropertyToID("_WorldSize");

        [Header("Fog Settings")]
        [SerializeField] private Material fogMaterial;
        [SerializeField] private int textureResolution;

        [Header("Exploration Settings")]
        [SerializeField] private float exploredBrightness;
        [SerializeField] private float visionRange;
        [SerializeField] private float softEdgeSize = 2f;
        
        private Texture2D fogTexture;
        private Color[] fogPixels;
        private Vector2 worldSize;

        public FogOfWarManager(int textureResolution = 2048, float exploredBrightness = 0.3f, float visionRange = 10f)
        {
            this.textureResolution = textureResolution;
            this.exploredBrightness = exploredBrightness;
            this.visionRange = visionRange;
            this.fogMaterial = Resources.Load<Material>("Shaders/FogOfWarMaterial");
        }
        
        private void OnEnable()
        {
            PlayerPrediction.OnLocalPlayerMoved += OnLocalPlayerMoved;
        }

        private void OnDisable()
        {
            PlayerPrediction.OnLocalPlayerMoved -= OnLocalPlayerMoved;
        }

        public void InitializeFog(Vector2Int gridSize, float cellSize)
        {
            worldSize = new Vector2(gridSize.x * cellSize, gridSize.y * cellSize);

            fogTexture = new Texture2D(textureResolution, textureResolution)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            fogPixels = new Color[textureResolution * textureResolution];
            for (var i = 0; i < fogPixels.Length; i++)
            {
                fogPixels[i] = Color.black;
            }
            
            fogTexture.SetPixels(fogPixels);
            fogTexture.Apply();

            fogMaterial.SetTexture(FogTexture, fogTexture);
            fogMaterial.SetVector(WorldSize, new Vector4(worldSize.x, worldSize.y, 0, 0));

            Debug.Log($"Fog initialized: World size = {worldSize}, Texture = {textureResolution}x{textureResolution}");
        }

        private void OnLocalPlayerMoved(Vector2 pos)
        {
            UpdateVisibility(pos);
            Debug.Log($"Fog updated at world pos: {pos}");
        }

        private void UpdateVisibility(Vector2 playerWorldPos)
        {
            // Step 1: Fade visible to explored
            for (var i = 0; i < fogPixels.Length; i++)
            {
                if (fogPixels[i].r > exploredBrightness)
                {
                    fogPixels[i] = new Color(exploredBrightness, exploredBrightness, exploredBrightness, 1f);
                }
            }
            
            // Step 2: Draw new visibility with soft edges
            DrawSoftCircleInFog(playerWorldPos, visionRange);
            
            fogTexture.SetPixels(fogPixels);
            fogTexture.Apply();
        }

        private void DrawSoftCircleInFog(Vector2 worldPos, float radius)
        {
            var texCoord = new Vector2(
                worldPos.x / worldSize.x * textureResolution,
                worldPos.y / worldSize.y * textureResolution
            );

            int texRadius = Mathf.CeilToInt((radius + softEdgeSize) / worldSize.x * textureResolution);

            for (int y = -texRadius; y <= texRadius; y++)
            {
                for (int x = -texRadius; x <= texRadius; x++)
                {
                    float distance = Mathf.Sqrt(x * x + y * y);
                    
                    if (distance > texRadius)
                        continue;
                    
                    int px = Mathf.RoundToInt(texCoord.x + x);
                    int py = Mathf.RoundToInt(texCoord.y + y);

                    if (px < 0 || px >= textureResolution || py < 0 || py >= textureResolution) continue;
                    
                    int index = py * textureResolution + px;
                        
                    // Calculate soft edge
                    float worldDistance = distance * worldSize.x / textureResolution;
                    float visibility;
                        
                    if (worldDistance < radius - softEdgeSize)
                    {
                        visibility = 1.0f;
                    }
                    else if (worldDistance < radius)
                    {
                        float t = (worldDistance - (radius - softEdgeSize)) / softEdgeSize;
                        visibility = Mathf.Lerp(1.0f, exploredBrightness, t);
                    }
                    else
                    {
                        continue;
                    }
                        
                    // Only update if brighter than current
                    if (visibility > fogPixels[index].r)
                    {
                        fogPixels[index] = new Color(visibility, visibility, visibility, 1f);
                    }
                }
            }
        }
    }
}