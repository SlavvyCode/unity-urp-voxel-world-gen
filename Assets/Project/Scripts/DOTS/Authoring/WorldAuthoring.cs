using Unity.Entities;
using UnityEngine;

namespace Project.Scripts.DOTS.Authoring
{
    public class WorldAuthoring: MonoBehaviour
    {
        [Tooltip("smaller values = more detail\n" +
                 "larger values = smoother terrain")]
        [Range(0.005f, 1f)]
        [SerializeField]
        private float perlinScale = .5f; // Scale for Perlin noise
    
        [Header("Terrain Shape")]
        [Tooltip("Base height of the terrain (world units)")]
        [SerializeField] private float baseHeight = 48f;
    
        [Tooltip("Height variation amplitude (world units)")]
        [SerializeField] private int heightVariation = 64;
    
        [Tooltip("Controls how quickly terrain features change (smaller = smoother)")]
        [Range(0.001f, 0.1f)]
        [SerializeField] private float terrainRoughness = 0.02f;
    
        [Header("Advanced")]
        public bool randomSeed = true;
        public int worldSeed;
        [Tooltip("How many noise layers to combine (more = more detail)")]
        [Range(1, 8)] 
        [SerializeField] private int noiseLayers = 4;

        public class Baker:Baker<WorldAuthoring>
        {
            public override void Bake(WorldAuthoring authoring) {
                Entity entity = GetEntity(TransformUsageFlags.None);

                var currWorldSeed = authoring.worldSeed;
                if (authoring.randomSeed == true)
                {
                    currWorldSeed = Random.Range(0, int.MaxValue);
                }                
                AddComponent(entity, new WorldParams 
                {
                    worldSeed = currWorldSeed,
                    terrainRoughness = authoring.terrainRoughness,
                    baseHeight = authoring.baseHeight,
                    heightVariation = authoring.heightVariation,
                    noiseLayers = authoring.noiseLayers
                });
            }

            private void Initialize(WorldAuthoring authoring)
            {
               
            }
        }

    }
}


public struct WorldParams : IComponentData
{
    public int worldSeed;
    public float terrainRoughness;
    public float baseHeight;
    public int heightVariation;
    public int noiseLayers;
}