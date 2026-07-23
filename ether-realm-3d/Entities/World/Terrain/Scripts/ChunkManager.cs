using System.Collections.Generic;
using System.Threading.Tasks;
using EtherRealm3D.Entities.World.Terrain.Scripts.Data;
using Godot;

namespace EtherRealm3D.Entities.World.Terrain.Scripts;

public partial class ChunkManager : Node3D
{
    private readonly Dictionary<Vector3I, TerrainChunk> _chunks = new();
    // Replace with disk serialization later
    private readonly Dictionary<Vector3I, TerrainChunk> _cachedChunks = new();

    // Coordinates of chunks that are currently generating to avoid generating the same chunk multiple times
    private readonly HashSet<Vector3I> _chunksInFlight = new();
    
    private int _cellsPerAxis;
    private float _voxelSize;
    private float _isoLevel;
    private FastNoiseLite _noise;
    private float _amplitude;
    private const float TerrainBaseHeight = 10.0f;

    /// <summary>
    /// Initialize the manager with terrain generation parameters.
    /// </summary>
    public void Initialize(int cellsPerAxis, float voxelSize, float isoLevel, FastNoiseLite noise, float amplitude)
    {
        _cellsPerAxis = cellsPerAxis;
        _voxelSize = voxelSize;
        _isoLevel = isoLevel;
        _noise = noise;
        _amplitude = amplitude;
    }
    
    public async void UpdateTerrain(Vector3 position, int radius)
    {
        if (_noise == null)
        {
            GD.PrintErr("[ChunkManager] UpdateTerrain called before Initialize.");
            return;
        }

        float chunkWorldSize = _cellsPerAxis * _voxelSize;

        // Convert world position → chunk coordinate
        var center = new Vector3I(
            Mathf.FloorToInt(position.X / chunkWorldSize),
            Mathf.FloorToInt(position.Y / chunkWorldSize),
            Mathf.FloorToInt(position.Z / chunkWorldSize)
        );

        // Build the desired set of chunk coordinates
        var desired = new HashSet<Vector3I>();
        for (int x = -radius; x <= radius; x++)
        for (int y = -radius; y <= radius; y++)
        for (int z = -radius; z <= radius; z++)
            desired.Add(center + new Vector3I(x, y, z));

        // Unload out-of-range chunks into cache
        var toUnload = new List<Vector3I>();
        foreach (var coord in _chunks.Keys)
            if (!desired.Contains(coord))
                toUnload.Add(coord);

        foreach (var coord in toUnload)
        {
            var chunk = _chunks[coord];
            _chunks.Remove(coord);
            RemoveChild(chunk);             // detach from scene tree; node still exists
            _cachedChunks[coord] = chunk;
        }

        // Schedule generation for not yet generated chunks
        var pendingTasks = new List<(Vector3I coord, Task<ChunkData> dataTask)>();

        foreach (var coord in desired)
        {
            if (_chunks.ContainsKey(coord)) continue;       // already in the scene
            if (_chunksInFlight.Contains(coord)) continue;  // already being generated

            if (_cachedChunks.TryGetValue(coord, out var cached))
            {
                // get cached chunk
                _cachedChunks.Remove(coord);
                _chunks[coord] = cached;
                AddChild(cached);
            }
            else
            {
                // generate a new chunk
                _chunksInFlight.Add(coord);
                pendingTasks.Add((coord, SampleDensitiesAsync(coord)));
            }
        }

        // Await each task and build the mesh on the main thread 
        foreach (var (coord, dataTask) in pendingTasks)
        {
            // Background: density field sampling
            ChunkData data;
            try
            {
                data = await dataTask;
            }
            catch (System.Exception e)
            {
                GD.PrintErr($"[ChunkManager] Density sampling failed for {coord}: {e.Message}");
                _chunksInFlight.Remove(coord);
                continue;
            }

            // Main thread: create and configure the chunk node
            var chunk = new TerrainChunk();
            chunk.Configure(data);
            _chunks[coord] = chunk;
            AddChild(chunk);

            // Background: marching-cubes triangulation
            try
            {
                await chunk.GenerateAsync();
            }
            catch (System.Exception e)
            {
                GD.PrintErr($"[ChunkManager] Mesh generation failed for {coord}: {e.Message}");
                _chunksInFlight.Remove(coord);
                continue;
            }

            // Main thread: upload mesh + build collision
            chunk.ApplyPendingMesh();
            _chunksInFlight.Remove(coord);
        }
    }
    
    /// <summary>
    /// Asynchronously samples the density field, returns ChunkData.
    /// </summary>
    private Task<ChunkData> SampleDensitiesAsync(Vector3I coord)
    {
        // Capture fields used inside the lambda to avoid closure over 'this'
        // while the background thread is running.
        int cellsPerAxis = _cellsPerAxis;
        float voxelSize  = _voxelSize;
        float isoLevel   = _isoLevel;
        float amplitude  = _amplitude;
        var   noise      = _noise;

        return Task.Run(() =>
        {
            int samples = cellsPerAxis + 1;
            var densities = new float[samples, samples, samples];

            var origin = new Vector3(
                coord.X * cellsPerAxis * voxelSize,
                coord.Y * cellsPerAxis * voxelSize,
                coord.Z * cellsPerAxis * voxelSize
            );

            for (int x = 0; x < samples; x++)
            for (int y = 0; y < samples; y++)
            for (int z = 0; z < samples; z++)
            {
                var worldPos   = origin + new Vector3(x * voxelSize, y * voxelSize, z * voxelSize);
                float noiseVal = noise.GetNoise2D(worldPos.X, worldPos.Z);
                float height   = TerrainBaseHeight + noiseVal * amplitude;
                densities[x, y, z] = worldPos.Y - height;
            }

            return new ChunkData(coord, densities, isoLevel, voxelSize, cellsPerAxis);
        });
    }

}
