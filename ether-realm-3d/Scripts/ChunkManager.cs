using Godot;
using System;
using System.Threading;
using System.Threading.Tasks;
using Godot.Collections;
using SCG = System.Collections.Generic;

namespace EtherRealm3D.Scripts;

public partial class ChunkManager : Node
{
    [Export] private int _chunkSize = 16;
    [Export] private int _noiseSeed = 1;
    [Export] private Array<Color> _colors = new();

    [Export] private Node3D _player;

    private readonly SCG.Dictionary<Vector3I, Chunk> _loadedChunks = new();
    private readonly SCG.HashSet<Vector3I> _chunksInProgress = new();
    private readonly SCG.Queue<Chunk> _readyChunks = new();
    private readonly object _lock = new(); // ensures thread-safe access to _loadedChunks, _chunksInProgress and _readyChunks

    private FastNoiseLite _noise = new();

    private PackedScene _chunkScene;

    private const int RenderDistance = 8;
    private const int ChunksPerFrame = 2;

    public override void _Ready()
    {
        _chunkScene = GD.Load<PackedScene>("res://Scenes/chunk.tscn");

        _noise.Seed = _noiseSeed;
        _noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;

        if (_player == null)
            GD.PrintErr("Player is not assigned to the ChunkManager script!");
    }

    public override void _Process(double delta)
    {
        if (_player == null) return;

        // přidáme hotové chunky do scény (jen z hlavního vlákna)
        int added = 0;
        lock (_lock)
        {
            while (_readyChunks.Count > 0 && added < ChunksPerFrame)
            {
                var chunk = _readyChunks.Dequeue();

                // Kontrola, zda je instance stále platná (nebyla smazána v UpdateChunks)
                if (!GodotObject.IsInstanceValid(chunk))
                    continue;

                // Pokud už chunk ve scéně je (např. z jiného důvodu), přeskočíme
                if (chunk.GetParent() != null)
                    continue;

                AddChild(chunk);
                added++;
            }
        }

        UpdateChunks();
    }

    private void UpdateChunks()
    {
        Vector3 playerPos = _player.GlobalPosition;

        Vector3I playerChunk = new Vector3I(
            Mathf.FloorToInt(playerPos.X / _chunkSize),
            Mathf.FloorToInt(playerPos.Y / _chunkSize),
            Mathf.FloorToInt(playerPos.Z / _chunkSize)
        );

        SCG.HashSet<Vector3I> required = new();

        for (int x = -RenderDistance; x <= RenderDistance; x++)  
        for (int z = -RenderDistance; z <= RenderDistance; z++)  
        {  
            // vyřadíme chunky mimo kruh pomocí Pythagorovy věty  
            if (x * x + z * z > RenderDistance * RenderDistance)  
                continue;  
  
            for (int y = 0; y < 4; y++)  
            {  
                required.Add(new Vector3I(  
                    playerChunk.X + x,  
                    y,  
                    playerChunk.Z + z  
                ));  
            }  
        }

        // odstranění chunků mimo oblast – THREAD-SAFE
        SCG.List<Vector3I> toRemove = new();

        lock (_lock)
        {
            foreach (var key in _loadedChunks.Keys)
                if (!required.Contains(key))
                    toRemove.Add(key);

            foreach (var rem in toRemove)
            {
                _loadedChunks[rem].QueueFree();
                _loadedChunks.Remove(rem);
            }
        }

        // generování nových chunků – každý v samostatném vlákně
        foreach (var chunkPos in required)
        {
            if (!_loadedChunks.ContainsKey(chunkPos) && !_chunksInProgress.Contains(chunkPos))
            {
                _chunksInProgress.Add(chunkPos);
                var pos = chunkPos; // capture pro lambda
                Task.Run(() => GenerateChunkAsync(pos));
            }
        }
    }

    private void GenerateChunkAsync(Vector3I chunkCoord)
    {
        // Instantiate musí být na hlavním vlákně – data a mesh generujeme zde
        // Proto chunk vytvoříme přes CallDeferred nebo předpočítáme data a mesh odděleně.
        // Chunk.cs generuje data a mesh interně, takže ho vytvoříme a naplníme zde,
        // ale do scény ho přidáme až v _Process přes _readyChunks queue.

        var chunk = _chunkScene.Instantiate<Chunk>();

        chunk.Position = new Vector3(
            chunkCoord.X * _chunkSize,
            chunkCoord.Y * _chunkSize,
            chunkCoord.Z * _chunkSize
        );

        chunk.GenerateData(_chunkSize, 256, _noise, _colors);
        chunk.GenerateMesh();

        lock (_lock)
        {
            _readyChunks.Enqueue(chunk);
            _loadedChunks[chunkCoord] = chunk;
            _chunksInProgress.Remove(chunkCoord);
        }
    }

    public override void _ExitTree()
    {
        // počkáme na dokončení všech tasků při ukončení
        Task.WaitAll(Task.Run(() => { while (_chunksInProgress.Count > 0) Thread.Sleep(10); }));
    }
}