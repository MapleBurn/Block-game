using System.Collections.Generic;
using System.Threading.Tasks;
using EtherRealm3D.Entities.World.Terrain.Scripts.Data;
using Godot;

namespace EtherRealm3D.Entities.World.Terrain.Scripts;

public partial class ChunkManager : Node3D
{
    private readonly Dictionary<Vector3I, TerrainChunk> _chunks = new();
    // Coordinates of chunks that are currently generating to avoid generating the same chunk multiple times
    private readonly HashSet<Vector3I> _chunksInFlight = new();
    
    private int _cellsPerAxis;
    private float _voxelSize;
    private float _isoLevel;
    private FastNoiseLite _noise;
    private float _amplitude;
    private const float TerrainBaseHeight = 10.0f;

    // UpdateTerrain is an async void driven every frame by WorldGenerator._Process.
    // Only one pass may be in progress at a time, otherwise passes interleave at every
    // await and can observe each other's half-built state.
    private bool _isUpdating;

    /// <summary>
    /// Initialize the manager with terrain generation parameters.
    /// </summary>
    public void Initialize(int cellsPerAxis, float voxelSize, float isoLevel, FastNoiseLite noise, float amplitude, string worldSaveDirectory = null)
    {
        _cellsPerAxis = cellsPerAxis;
        _voxelSize = voxelSize;
        _isoLevel = isoLevel;
        _noise = noise;
        _amplitude = amplitude;
        
        string saveDir = worldSaveDirectory ?? ProjectSettings.GlobalizePath("user://world/regions");
    }
    
    public async void UpdateTerrain(Vector3 position, int radius)
    {
        if (_noise == null)
        {
            GD.PrintErr("[ChunkManager] UpdateTerrain called before Initialize.");
            return;
        }

        if (_isUpdating) return;
        _isUpdating = true;

        try
        {
            float chunkWorldSize = _cellsPerAxis * _voxelSize;

            var center = new Vector3I(
                Mathf.FloorToInt(position.X / chunkWorldSize),
                Mathf.FloorToInt(position.Y / chunkWorldSize),
                Mathf.FloorToInt(position.Z / chunkWorldSize));

            var desired = new HashSet<Vector3I>();
            for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
            for (int z = -radius; z <= radius; z++)
                desired.Add(center + new Vector3I(x, y, z));

            // Unload out-of-range chunks
            var toUnload = new List<Vector3I>();
            foreach (var coord in _chunks.Keys)
                if (!desired.Contains(coord))
                    toUnload.Add(coord);

            if (toUnload.Count > 0)
                UnloadChunks(toUnload);

            // Kick every load off up front so density sampling runs in parallel.
            var pendingTasks = new List<(Vector3I coord, Task<(ChunkData data, bool fromDisk)> task)>();

            foreach (var coord in desired)
            {
                if (_chunks.ContainsKey(coord)) continue; // already active
                if (_chunksInFlight.Contains(coord)) continue; // already being generated

                _chunksInFlight.Add(coord);
                pendingTasks.Add((coord, GetChunkDataAsync(coord)));
            }

            foreach (var (coord, task) in pendingTasks)
            {
                try
                {
                    // Background: disk load or density sampling, then marching cubes.
                    // Neither touches the scene tree, so nothing here can be freed underneath us.
                    var (data, fromDisk) = await task;

                    float isoLevel = _isoLevel;
                    float voxelSize = _voxelSize;
                    var meshData = await Task.Run(() =>
                        TerrainMesher.GenerateMeshData(data.Densities, isoLevel, voxelSize));

                    // The world may have been torn down while we were awaiting.
                    if (!IsInstanceValid(this)) return;

                    // Main thread, and no await from here down: the node is created, parented and
                    // meshed in one uninterrupted block, so an unload can never free it midway.
                    var chunk = new TerrainChunk();
                    chunk.Configure(data, isDirty: !fromDisk, _cellsPerAxis, _voxelSize);
                    chunk.SetMeshData(meshData);
                    _chunks[coord] = chunk;
                    AddChild(chunk);
                    chunk.ApplyPendingMesh();
                }
                catch (System.Exception e)
                {
                    GD.PrintErr($"[ChunkManager] Chunk {coord} failed: {e.Message}");
                }
                finally
                {
                    // Must always run: a stranded coord is never retried, leaving a permanent hole.
                    _chunksInFlight.Remove(coord);
                }
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }
    
    private void UnloadChunks(List<Vector3I> coords)
    {
        var dirtyData = new List<ChunkData>(coords.Count);

        foreach (var coord in coords)
        {
            var chunk = _chunks[coord];
            _chunks.Remove(coord);
            RemoveChild(chunk);

            if (chunk.IsDirty)
                dirtyData.Add(chunk.Data);
            
            chunk.QueueFree();
        }

        if (dirtyData.Count == 0) return;

        foreach (var dat in dirtyData)
        {
            // One bad write must not abort the rest of the unload.
            try
            {
                RegionFileManager.SaveChunk(dat);
            }
            catch (System.Exception e)
            {
                GD.PrintErr($"[ChunkManager] Save failed for {dat.ChunkCoordinate}: {e.Message}");
            }
        }
    }
    
    private async Task<(ChunkData data, bool fromDisk)> GetChunkDataAsync(Vector3I coord)
    {
        // Disk load attempt
        ChunkData fromDisk = RegionFileManager.LoadChunk(coord);

        if (fromDisk != null)
            return (fromDisk, true);

        // Procedural generation
        ChunkData generated = await SampleDensitiesAsync(coord);
        return (generated, false);
    }
    
    /// <summary>
    /// Asynchronously samples the density field, returns ChunkData.
    /// </summary>
    private Task<ChunkData> SampleDensitiesAsync(Vector3I coord)
    {
        int cellsPerAxis = _cellsPerAxis;
        float voxelSize = _voxelSize;
        float amplitude = _amplitude;
        var   noise = _noise;

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

            return new ChunkData(coord, densities);
        });
    }

}
