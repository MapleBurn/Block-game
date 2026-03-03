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
                Chunk chunk = _loadedChunks[rem];  
      
                // DŮLEŽITÉ: Data musíme vytáhnout PŘEDTÍM, než zavoláme QueueFree  
                if (chunk.IsDirty)   
                {  
                    byte[] data = chunk.GetSaveData();  
                    Vector3I pos = rem;  
                    // Uložíme na pozadí, aby se hra nesekala  
                    Task.Run(() => SaveChunkToDisk(pos, data));  
                }  
  
                chunk.QueueFree();  
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
    
    private void SaveChunkToDisk(Vector3I coord, byte[] data)  
    {  
        if (data.Length == 0) return;  
  
        string path = GetChunkPath(coord);  
        string globalPath = ProjectSettings.GlobalizePath(path);  
        string dir = System.IO.Path.GetDirectoryName(globalPath);  
  
        try   
        {  
            if (!System.IO.Directory.Exists(dir))  
                System.IO.Directory.CreateDirectory(dir);  
  
            System.IO.File.WriteAllBytes(globalPath, data);  
            // GD.Print($"Ulozen chunk: {coord}"); // Pro debug odkomentuj  
        }  
        catch (Exception e)  
        {  
            GD.PrintErr($"Selhalo ulozeni chunku {coord}: {e.Message}");  
        }  
    }

    private void GenerateChunkAsync(Vector3I chunkCoord)  
    {  
        string path = ProjectSettings.GlobalizePath(GetChunkPath(chunkCoord));  
        var chunk = _chunkScene.Instantiate<Chunk>();  
      
        // Nastavení pozice musí proběhnout hned  
        chunk.Position = new Vector3(  
            chunkCoord.X * _chunkSize,  
            chunkCoord.Y * _chunkSize,  
            chunkCoord.Z * _chunkSize  
        );  
  
        if (System.IO.File.Exists(path))  
        {  
            byte[] data = System.IO.File.ReadAllBytes(path);  
            chunk.LoadSaveData(data);  
        }  
        else  
        {  
            chunk.GenerateData(_chunkSize, 256, _noise, _colors);  
            chunk.IsDirty = true;   
        }  
  
        chunk.GenerateMesh();  
  
        lock (_lock)  
        {  
            _readyChunks.Enqueue(chunk);  
            _loadedChunks[chunkCoord] = chunk;  
            _chunksInProgress.Remove(chunkCoord);  
        }  
    }

    private string GetChunkPath(Vector3I coord)  
    {  
        return $"user://saves/chunk_{coord.X}_{coord.Y}_{coord.Z}.dat";  
    }
    
    
    
    public override void _ExitTree()
    {
        // počkáme na dokončení všech tasků při ukončení
        Task.WaitAll(Task.Run(() => { while (_chunksInProgress.Count > 0) Thread.Sleep(10); }));
    }
}