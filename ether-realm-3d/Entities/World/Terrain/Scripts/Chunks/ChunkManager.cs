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
/// Streams terrain around the player one chunk column at a time. Each column only loads the
/// chunks its surface passes through, so tall mountains don't cost a full cube of chunks.
/// Loading, generation and meshing run in the background; edited chunks are saved on unload.
/// </summary>
public partial class ChunkManager : Node3D
{
    [Export] public Player Player;

    [ExportGroup("Chunks")]
    [Export] public int CellsPerAxis = 32;
    // Larger voxels give bigger low-poly facets and let each chunk cover more ground.
    [Export] public float VoxelSize = 2.0f;
    // Horizontal view distance, in chunk columns.
    [Export] public int LoadRadius = 8;
    // Only columns this close to the player get collision.
    [Export] public int CollisionRadius = 1;
    // Chunk meshes added to the scene per frame; higher loads faster but can stutter.
    [Export] public int MaxUploadsPerFrame = 8;

    [Export] public WorldGenSettings Settings;

    // Loaded columns and their chunk nodes. Only chunks with a surface get a node.
    private readonly Dictionary<Vector2I, List<TerrainChunk>> _columns = new();

    private TerrainGenerator _generator;
    private StandardMaterial3D _material;
    private Vector2I _collisionCenter = new(int.MaxValue, int.MaxValue);
    private bool _isUpdating;
    private bool _isExiting;

    private float ChunkWorldSize => CellsPerAxis * VoxelSize;

    public override void _Ready()
    {
        Settings ??= new WorldGenSettings();
        _generator = new TerrainGenerator(Settings, CellsPerAxis, VoxelSize);
        _material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true
        };

        if (Player != null)
            PlaceOnSurface(Player);
    }

    public override void _Process(double delta)
    {
        if (_isExiting || Player == null) return;

        Vector3 position = Player.GlobalPosition;
        var center = new Vector2I(
            Mathf.FloorToInt(position.X / ChunkWorldSize),
            Mathf.FloorToInt(position.Z / ChunkWorldSize));

        if (center != _collisionCenter)
        {
            _collisionCenter = center;
            UpdateCollision();
        }

        UpdateTerrain(center);
    }

    public void OnExitPressed()
    {
        _isExiting = true;
        SaveWorld();
    }

    public async void SaveWorld()
    {
        UnloadColumns(_columns.Keys.ToList());
        await SaveQueue.FlushAsync();
    }

    #region Private Helpers
    private async void UpdateTerrain(Vector2I center)
    {
        if (_isUpdating) return;
        _isUpdating = true;

        try
        {
            int radius = LoadRadius;
            var desired = new HashSet<Vector2I>();
            for (int x = -radius; x <= radius; x++)
            for (int z = -radius; z <= radius; z++)
                if (x * x + z * z <= radius * radius)
                    desired.Add(center + new Vector2I(x, z));

            var toUnload = _columns.Keys.Where(c => !desired.Contains(c)).ToList();
            if (toUnload.Count > 0)
            {
                UnloadColumns(toUnload);
                _generator.ForgetColumnsOutside(center, radius + 1);
            }

            // Start every load up front so they run in parallel; nearest first so they appear first.
            var pending = desired
                .Where(c => !_columns.ContainsKey(c))
                .OrderBy(c => (c - center).LengthSquared())
                .Select(c => (column: c, task: LoadColumnAsync(c)))
                .ToList();

            int uploads = 0;
            foreach (var (column, task) in pending)
            {
                try
                {
                    var chunks = await task;
                    if (!IsInstanceValid(this) || _isExiting) return;

                    var nodes = new List<TerrainChunk>();
                    _columns[column] = nodes;

                    foreach (var (data, meshData) in chunks)
                    {
                        // Generated chunks can be regenerated from the seed and disk chunks are already
                        // saved, so a freshly loaded chunk with nothing to render needs no node.
                        if (meshData.IsEmpty) continue;

                        nodes.Add(CreateChunk(data, meshData));

                        // Spread mesh and collision uploads over frames to avoid hitches.
                        if (++uploads < MaxUploadsPerFrame) continue;
                        uploads = 0;
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        if (!IsInstanceValid(this) || _isExiting) return;
                    }
                }
                catch (System.Exception e)
                {
                    GD.PrintErr($"[ChunkManager] Column {column} failed: {e.Message}");
                }
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    /// <summary>
    /// Background: works out which chunks the column's surface passes through, then loads or
    /// generates and meshes each of them.
    /// </summary>
    private async Task<(ChunkData data, ChunkMeshData meshData)[]> LoadColumnAsync(Vector2I column)
    {
        var generator = _generator;
        float chunkWorldSize = ChunkWorldSize;

        ColumnData columns = await Task.Run(() => generator.GetColumns(column));

        int lowest  = Mathf.FloorToInt(columns.MinHeight / chunkWorldSize);
        int highest = Mathf.FloorToInt(columns.MaxHeight / chunkWorldSize);

        var loads = new Task<(ChunkData, ChunkMeshData)>[highest - lowest + 1];
        for (int y = lowest; y <= highest; y++)
            loads[y - lowest] = LoadChunkAsync(new Vector3I(column.X, y, column.Y), columns);

        return await Task.WhenAll(loads);
    }

    /// <summary>
    /// Background: reads the chunk from disk or generates it, then builds its mesh data.
    /// </summary>
    private async Task<(ChunkData data, ChunkMeshData meshData)> LoadChunkAsync(Vector3I coord, ColumnData columns)
    {
        ChunkData saved = await SaveQueue.LoadChunkAsync(coord);

        var generator = _generator;
        float voxelSize = VoxelSize;

        return await Task.Run(() =>
        {
            ChunkData data = saved ?? generator.GenerateChunk(coord, columns);
            return (data, TerrainMesher.GenerateMeshData(data, columns, generator.Surface, voxelSize));
        });
    }

    /// <summary>
    /// Moves a node to just above the ground (or the sea) at its current X/Z.
    /// </summary>
    private void PlaceOnSurface(Node3D node)
    {
        Vector3 position = node.GlobalPosition;
        float ground = _generator.SampleColumn(position.X, position.Z).Height;
        node.GlobalPosition = new Vector3(position.X, Mathf.Max(ground, Settings.SeaLevel) + 4.0f, position.Z);
    }

    private bool HasCollision(Vector2I column) =>
        Mathf.Abs(column.X - _collisionCenter.X) <= CollisionRadius &&
        Mathf.Abs(column.Y - _collisionCenter.Y) <= CollisionRadius;

    private void UpdateCollision()
    {
        foreach (var (column, nodes) in _columns)
        {
            bool enabled = HasCollision(column);
            foreach (var chunk in nodes)
                chunk.SetCollisionEnabled(enabled);
        }
    }

    /// <summary>
    /// Main thread only.
    /// </summary>
    private TerrainChunk CreateChunk(ChunkData data, ChunkMeshData meshData)
    {
        var chunk = new TerrainChunk();
        chunk.Configure(data, isDirty: false, ChunkWorldSize);
        AddChild(chunk);
        chunk.ApplyMesh(meshData, _material);
        chunk.SetCollisionEnabled(HasCollision(new Vector2I(data.ChunkCoordinate.X, data.ChunkCoordinate.Z)));
        return chunk;
    }

    private async void UnloadColumns(List<Vector2I> columns)
    {
        var dirtyData = new List<ChunkData>();

        foreach (var column in columns)
        {
            foreach (var chunk in _columns[column])
            {
                if (chunk.IsDirty)
                    dirtyData.Add(chunk.Data);

                RemoveChild(chunk);
                chunk.QueueFree();
            }
            _columns.Remove(column);
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
