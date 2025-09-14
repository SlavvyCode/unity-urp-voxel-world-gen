using Project.Scripts.DOTS.Other;using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using static  Project.Scripts.DOTS.Other.DOTS_Utils;
using static Project.Scripts.DOTS.Systems.WorldHeightmapWindowHolder;

namespace Project.Scripts.DOTS.Systems
{
    [UpdateAfter(typeof(HeightMapSystem))]
    [UpdateBefore(typeof(MeshGenerationSystem))]
    public partial struct BlockGenerationSystemNEW :ISystem
    {
        private ComponentLookup<DOTS_Chunk> chunksLookup;
        private BufferLookup<DOTS_Block> blocksLookup;
        private NativeList<Entity> desiredChunks;

        private EntityQuery allChunksQuery;
        private NativeList<int2> uniqueChunkXZCoords;
        
        public void OnCreate(ref SystemState state)
        {
        
        
        
            chunksLookup = SystemAPI.GetComponentLookup<DOTS_Chunk>(true);
            blocksLookup = SystemAPI.GetBufferLookup<DOTS_Block>(false);
            // chunkStateLookup = SystemAPI.GetComponentLookup<DOTS_ChunkState>(false);

            allChunksQuery = SystemAPI.QueryBuilder()
                .WithAny<DOTS_ChunkState>()
                .Build();


            desiredChunks = new NativeList<Entity>(Allocator.Persistent);

        }
        
        
        public void OnUpdate(ref SystemState state)
        {
            desiredChunks.Clear();

            var allChunks =
                allChunksQuery.ToEntityArray(Allocator
                    .Temp); //remove from desired chunks entities which have chunkstate of different kind than ready forblockgeneration
            var chunkStates = SystemAPI.GetComponentLookup<DOTS_ChunkState>(true); // read-only

            for (int i = 0; i < allChunks.Length; i++)
            {
                var entity = allChunks[i];
                if (chunkStates[entity].Value == ChunkStateEnum.BlockGenPending)
                    if (chunkStates[entity].Value == ChunkStateEnum.BlockGenPending)
                    {
                        desiredChunks.Add(entity);
                    }
            }


            if (desiredChunks.Length == 0)
                return;
            if (blockHeightsWindow.Length==0)
                return;

            chunksLookup.Update(ref state);
            blocksLookup.Update(ref state);
            
            
            
            //todo how much can i get passed from the heightmap system and how much do i need to recalculate? i guess everything is ok to keep?
            
            int renderDistance = 0;
            int2 playerChunkCoordXZ = new int2(0, 0);
            foreach (var (settings, playerChunkCoords, tag) in SystemAPI
                         .Query<RefRO<PlayerSettings>, RefRO<EntityChunkCoords>, RefRO<PlayerTag>>())
            {
                renderDistance = settings.ValueRO.renderDistance;
                playerChunkCoordXZ = playerChunkCoords.ValueRO.newChunkCoords.xz;
            }

            // windows are 2D, centered on the player.
            int windowEdgeChunkLength = (2 * renderDistance) + 1;
            int windowEdgeBlockLength = windowEdgeChunkLength * CHUNK_SIZE;

            //int windowChunkLength = windowEdgeChunkLength * windowEdgeChunkLength;
            // int windowBlockLength = windowEdgeBlockLength * windowEdgeBlockLength;



            
            var ECBSystem = state.World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
            var ecb = ECBSystem.CreateCommandBuffer();
            //3 do generation job 
            // todo WHAT DO I NEED TO HAVE ASSIGN HEIGHTMAPS TO EACH CHUNK
            // heightmap or heights
            // each chunk 
            var generateChunkBlocksJob = new GenerateChunkBlocksJob
            {
                chunksLookup = chunksLookup,
                blocksLookup = blocksLookup,
                ecb = ecb.AsParallelWriter(),

                desiredChunks = desiredChunks,
                blockHeightsWindow = WorldHeightmapWindowHolder.blockHeightsWindow,
                // renderDistance = renderDistance,
                windowEdgeBlockLength = windowEdgeBlockLength, 
                playerChunkCoordXZ= playerChunkCoordXZ,
            };
            
            var generateHandle  = generateChunkBlocksJob.ScheduleParallel(desiredChunks.Length, 1, heightMapJobHandle);
            state.Dependency = generateHandle ;
        }
    }
    
    

[BurstCompile(CompileSynchronously = true)]
public struct GenerateChunkBlocksJob : IJobFor
{
    [ReadOnly] public NativeArray<Entity> desiredChunks;

    // [ReadOnly] public NativeArray<int2> chunkYColumnCoords;
    [ReadOnly] public ComponentLookup<DOTS_Chunk> chunksLookup;
    [NativeDisableParallelForRestriction] public BufferLookup<DOTS_Block> blocksLookup;
    // [ReadOnly] public NativeParallelHashMap<int2, int> blockColumnCoordsToHeightHashMap;
    public EntityCommandBuffer.ParallelWriter ecb;

    public int2 playerChunkCoordXZ;
    [NativeDisableParallelForRestriction] public NativeArray<int> blockHeightsWindow;
    public int windowEdgeBlockLength;
    public int renderDistance;

    //todo
    // pillar height depends on render distance (spawned chunks).
    // i could still make the cylinder render distance as opposed to a sphere to reduce the pillar height.
    // i do worry though if the player would ever encoutner any "fake bottomless holes" or "cut off mountaintops" though.
    // alternatively i coudl disable chunk rendering if they're fully obstructed by another chunk (chunk completely enclosed from all sides.)
    // - but that's a miniscule effect on the CPU right now i think, mostly gpu does that and my game is very gpu light for now still


    // EXECUTES ONCE PER EACH CHUNK IN THE RENDER DISTANCE
    [BurstCompile]
    public void Execute(int jobIndex)
    {
        Entity chunkEntity = desiredChunks[jobIndex];
        int3 chunkCoord = chunksLookup[chunkEntity].ChunkCoord;
        DynamicBuffer<DOTS_Block> blocks = blocksLookup[chunkEntity];
        InitializeChunkBlocks(ref blocks);

        int2 centerBlockCoord = new int2(
            playerChunkCoordXZ.x * CHUNK_SIZE + CHUNK_SIZE / 2,
            playerChunkCoordXZ.y * CHUNK_SIZE + CHUNK_SIZE / 2
        );
        int2 chunkColumnWorldCoord = new int2(chunkCoord.x * CHUNK_SIZE, chunkCoord.z * CHUNK_SIZE);
        
        int columnHeight;
        int stoneBottom = 0;

        // already all Air
        for (int localX = 0; localX < CHUNK_SIZE; localX++)
        for (int localZ = 0; localZ < CHUNK_SIZE; localZ++)
        {
            int worldX = chunkColumnWorldCoord.x + localX;
            int worldZ = chunkColumnWorldCoord.y + localZ;

            int dx = worldX - centerBlockCoord.x;
            int dz = worldZ - centerBlockCoord.y;
            int index = getBlockWindowIndexXZ(dx, dz, windowEdgeBlockLength);
            columnHeight = blockHeightsWindow[index];
            // blockColumnCoordsToHeightHashMap.TryGetValue(new int2(chunkColumnCoord.x+localX,chunkColumnCoord.y + localZ), out columnHeight);


            int stoneTop = math.max(stoneBottom, columnHeight - 4);
            int dirtTop = math.max(stoneTop, columnHeight - 1);
            int grassTop = columnHeight;

            // describes the world location of the chunk in unity units
            int chunkWorldY = chunkCoord.y * CHUNK_SIZE;


            // Stone
            int stoneLocalBottom = math.clamp(stoneBottom - chunkWorldY, 0, CHUNK_SIZE);
            int stoneLocalTop = math.clamp(stoneTop - chunkWorldY, 0, CHUNK_SIZE);
            for (int y = stoneLocalBottom; y < stoneLocalTop; y++)
                blocks[ToIndex(localX, y, localZ)] = new DOTS_Block { Value = BlockType.Stone };

            // Dirt
            int dirtLocalBottom = math.clamp(stoneTop - chunkWorldY, 0, CHUNK_SIZE);
            int dirtLocalTop = math.clamp(dirtTop - chunkWorldY, 0, CHUNK_SIZE);
            for (int y = dirtLocalBottom; y < dirtLocalTop; y++)
                blocks[ToIndex(localX, y, localZ)] = new DOTS_Block { Value = BlockType.Dirt };

            // Grass
            int grassLocalBottom = math.clamp(dirtTop - chunkWorldY, 0, CHUNK_SIZE);
            int grassLocalTop = math.clamp(grassTop - chunkWorldY, 0, CHUNK_SIZE);
            for (int y = grassLocalBottom; y < grassLocalTop; y++)
                blocks[ToIndex(localX, y, localZ)] = new DOTS_Block { Value = BlockType.Grass };
        }


        // foreach (var chunk in desiredChunks)
        // {
        // ecb.SetComponent<DOTS_ChunkState>(chunkEntity, new DOTS_ChunkState { Value = ChunkState.BlocksGenerated });

        ecb.SetComponent(jobIndex, chunkEntity, new DOTS_ChunkState { Value = ChunkStateEnum.MeshPending });
        // }
    }


    private void InitializeChunkBlocks(ref DynamicBuffer<DOTS_Block> blocks)
    {
        if (blocks.Length == CHUNK_VOLUME)
        {
            return;
        }

        // If the buffer is not the correct size, we need to initialize it
        blocks.ResizeUninitialized(CHUNK_VOLUME);

        for (int i = 0; i < CHUNK_VOLUME; i++)
            blocks[i] = new DOTS_Block { Value = BlockType.Air };
    }
    
    
}
    
    
    
}