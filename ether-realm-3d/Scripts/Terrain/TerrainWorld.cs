using System.Collections.Generic;
using Godot;

namespace EtherRealm3D.Scripts.Terrain;

public partial class TerrainWorld : Node3D
{
    // Terrain properties
    public int CellsPerAxis = 32;  
    public float VoxelSize = 1.0f;  
    public float IsoLevel = 0.0f;  
    public int NoiseSeed = 0;  
    public float NoiseFrequency = 0.025f;
    [Export] public float TerrainAmplitude = 15.0f;

    [Export] public Player Player;
    [Export] public int ViewDistance = 5;
    [Export] public int VerticalViewDistance = 4;
    
    private FastNoiseLite _noise;  
    private readonly Dictionary<Vector3I, TerrainChunk> _activeChunks = new();  
    private Vector3I _lastViewerChunk = new Vector3I(int.MaxValue, int.MaxValue, int.MaxValue);  
    private readonly HashSet<Vector3I> _generatingChunks = new();
    
    public int MaxApplyPerFrame = 2;
  
public override void _Ready()  
    {  
        _noise = new FastNoiseLite  
        {  
            Seed = NoiseSeed,  
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,  
            Frequency = NoiseFrequency  
        };
    }  
  
    public override void _Process(double delta)  
    {  
        // Apply finished chunks — throttled to MaxApplyPerFrame per frame.  
        int applied = 0;  
        foreach (var chunk in _activeChunks.Values)  
        {  
            if (applied >= MaxApplyPerFrame) break;  
            if (!chunk.IsReadyToApply) continue;  
  
            chunk.ApplyPendingMesh();  
            _generatingChunks.Remove(chunk.ChunkCoordinate);  
            applied++;  
        }  
  
        // Check if viewer moved to a new chunk.  
        float chunkSize = CellsPerAxis * VoxelSize;  
        Vector3I currentCoord = new Vector3I(  
            Mathf.FloorToInt(Player.GlobalPosition.X / chunkSize),  
            Mathf.FloorToInt(Player.GlobalPosition.Y / chunkSize),  
            Mathf.FloorToInt(Player.GlobalPosition.Z / chunkSize)  
        );  
  
        if (currentCoord != _lastViewerChunk)  
        {  
            UpdateVisibleChunks(currentCoord);  
            _lastViewerChunk = currentCoord;  
        }  
    }  
  
    private void UpdateVisibleChunks(Vector3I viewerCoord)  
    {  
        HashSet<Vector3I> toKeep = new();  
  
        for (int x = -ViewDistance; x <= ViewDistance; x++)  
        for (int y = -VerticalViewDistance; y <= VerticalViewDistance; y++)  
        for (int z = -ViewDistance; z <= ViewDistance; z++)  
        {  
            Vector3I coord = viewerCoord + new Vector3I(x, y, z);  
            toKeep.Add(coord);  
  
            if (!_activeChunks.ContainsKey(coord) &&  
                !_generatingChunks.Contains(coord))  
            {  
                SpawnChunk(coord);  
            }  
        }  
  
        // Remove out-of-range chunks.  
        List<Vector3I> toRemove = new();  
        foreach (var coord in _activeChunks.Keys)  
            if (!toKeep.Contains(coord)) toRemove.Add(coord);  
  
        foreach (var coord in toRemove)  
        {  
            _activeChunks[coord].QueueFree();  
            _activeChunks.Remove(coord);  
            _generatingChunks.Remove(coord);  
        }  
    }  
  
    private void SpawnChunk(Vector3I coord)  
    {  
        TerrainChunk chunk = new();  
        AddChild(chunk);  
        chunk.Configure(coord, CellsPerAxis, VoxelSize, IsoLevel, _noise, TerrainAmplitude);  
  
        _activeChunks[coord] = chunk;  
        _generatingChunks.Add(coord);  
  
        // Fire and forget — result is polled in _Process().  
        chunk.GenerateAsync();  
    }
}