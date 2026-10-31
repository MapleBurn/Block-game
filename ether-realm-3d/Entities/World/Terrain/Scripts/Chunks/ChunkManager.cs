using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EtherRealm3D.Entities.Character;
using EtherRealm3D.Entities.World.Terrain.Scripts.Generation;
using EtherRealm3D.Entities.World.Terrain.Scripts.Meshing;
using EtherRealm3D.Entities.World.Terrain.Scripts.Persistence;
using Godot;

namespace EtherRealm3D.Entities.World.Terrain.Scripts.Chunks;

/// <summary>
/// Streams terrain chunks around the player: loads or generates them in the background,
/// meshes them, and saves edited chunks when they unload.
/// </summary>
public partial class ChunkManager : Node3D
{
    [Export] public Player Player;

    [ExportGroup("Chunks")]
    [Export] public int CellsPerAxis = 32;
    [Export] public float VoxelSize = 1.0f;
    [Export] public int LoadRadius = 4;

    [ExportGroup("Generation")]
    [Export] public int Seed;
    [Export] public float NoiseFrequency = 0.025f;
    [Export] public float BaseHeight = 10.0f;
    [Export] public float Amplitude = 15.0f;

    // Loaded chunks. A null value is a loaded chunk with no surface, so it has no node.
    private readonly Dictionary<Vector3I, TerrainChunk> _chunks = new();

    private TerrainGenerator _generator;
    private StandardMaterial3D _material;
    private bool _isUpdating;
    private bool _isExiting;

    private float ChunkWorldSize => CellsPerAxis * VoxelSize;

    public override void _Ready()
    {
        _generator = new TerrainGenerator(Seed, NoiseFrequency, BaseHeight, Amplitude, CellsPerAxis, VoxelSize);
        _material = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.5f, 0.2f) };
    }

    public override void _Process(double delta)
    {
        if (!_isExiting && Player != null)
            UpdateTerrain(Player.GlobalPosition);
    }

    public void OnExitPressed()
    {
        _isExiting = true;
        SaveWorld();
    }

    public async void SaveWorld()
    {
        UnloadChunks(_chunks.Keys.ToList());
        await SaveQueue.FlushAsync();
    }

    #region Private Helpers
    private async void UpdateTerrain(Vector3 position)
    {
        if (_isUpdating) return;
        _isUpdating = true;

        try
        {
            var center = new Vector3I(
                Mathf.FloorToInt(position.X / ChunkWorldSize),
                Mathf.FloorToInt(position.Y / ChunkWorldSize),
                Mathf.FloorToInt(position.Z / ChunkWorldSize));

            int radius = LoadRadius;
            var desired = new HashSet<Vector3I>();
            for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
            for (int z = -radius; z <= radius; z++)
                desired.Add(center + new Vector3I(x, y, z));

            var toUnload = _chunks.Keys.Where(c => !desired.Contains(c)).ToList();
            if (toUnload.Count > 0)
                UnloadChunks(toUnload);

            // Start every load up front so they run in parallel; nearest first so they appear first.
            var pending = desired
                .Where(c => !_chunks.ContainsKey(c))
                .OrderBy(c => (c - center).LengthSquared())
                .Select(c => (coord: c, task: LoadChunkAsync(c)))
                .ToList();

            foreach (var (coord, task) in pending)
            {
                try
                {
                    var (data, meshData) = await task;
                    if (!IsInstanceValid(this)) return;
                    // Generated chunks can be regenerated from the seed and disk chunks are already saved,
                    // so a freshly loaded chunk only becomes dirty once it is edited.
                    AddChunk(coord, data, meshData, isDirty: false);
                }
                catch (System.Exception e)
                {
                    GD.PrintErr($"[ChunkManager] Chunk {coord} failed: {e.Message}");
                }
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    /// <summary>
    /// Background: reads the chunk from disk or generates it, then builds its mesh data.
    /// </summary>
    private async Task<(ChunkData data, ChunkMeshData meshData)> LoadChunkAsync(Vector3I coord)
    {
        ChunkData saved = await SaveQueue.LoadChunkAsync(coord);

        var generator = _generator;
        float voxelSize = VoxelSize;

        return await Task.Run(() =>
        {
            ChunkData data = saved ?? generator.GenerateChunk(coord);
            return (data, TerrainMesher.GenerateMeshData(data, voxelSize));
        });
    }

    /// <summary>
    /// Main thread only.
    /// </summary>
    private void AddChunk(Vector3I coord, ChunkData data, ChunkMeshData meshData, bool isDirty)
    {
        // Nothing to render and nothing to save: track it as loaded without a node.
        if (meshData.IsEmpty && !isDirty)
        {
            _chunks[coord] = null;
            return;
        }

        var chunk = new TerrainChunk();
        chunk.Configure(data, isDirty, ChunkWorldSize);
        _chunks[coord] = chunk;
        AddChild(chunk);
        chunk.ApplyMesh(meshData, _material);
    }

    private async void UnloadChunks(List<Vector3I> coords)
    {
        var dirtyData = new List<ChunkData>();

        foreach (var coord in coords)
        {
            var chunk = _chunks[coord];
            _chunks.Remove(coord);
            if (chunk == null) continue;

            if (chunk.IsDirty)
                dirtyData.Add(chunk.Data);

            RemoveChild(chunk);
            chunk.QueueFree();
        }

        foreach (var data in dirtyData)
        {
            try
            {
                await SaveQueue.EnqueueSaveAsync(data);
            }
            catch (System.Exception e)
            {
                GD.PrintErr($"[ChunkManager] Save failed for {data.ChunkCoordinate}: {e.Message}");
            }
        }
    }
    #endregion
}
