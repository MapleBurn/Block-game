using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using EtherRealm3D.Entities.World.Terrain.Scripts.Chunks;
using Godot;

namespace EtherRealm3D.Entities.World.Terrain.Scripts.Persistence;

public static class SaveQueue
{
    private abstract record DiskOp;
    private sealed record SaveOp(ChunkData Data) : DiskOp;
    private sealed record LoadOp(Vector3I Coord, TaskCompletionSource<ChunkData> Tcs) : DiskOp;
    private sealed record FlushOp(TaskCompletionSource Tcs) : DiskOp;
    private static readonly ActionBlock<DiskOp> Worker;

    static SaveQueue()
    {
        Worker = new ActionBlock<DiskOp>(op =>
        {
            switch (op)
            {
                case SaveOp save:
                    try
                    {
                        RegionFileManager.SaveChunk(save.Data);
                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"[SaveQueue] Save failed at {save.Data.ChunkCoordinate}: {ex.Message}");
                    }
                    break;

                case LoadOp load:
                    try
                    {
                        var result = RegionFileManager.LoadChunk(load.Coord);
                        load.Tcs.SetResult(result);
                    }
                    catch (Exception ex)
                    {
                        load.Tcs.SetException(ex);
                    }
                    break;

                case FlushOp flush:
                    // Ops run one at a time in order, so everything queued before this is done.
                    flush.Tcs.SetResult();
                    break;
            }
        },
        new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 1, // only one thread at a time for disk usage
            BoundedCapacity        = 1000
        });
    }

    // Save
    public static Task<bool> EnqueueSaveAsync(ChunkData data, CancellationToken ct = default)
        => Worker.SendAsync(new SaveOp(data), ct);

    // Load
    public static async Task<ChunkData> LoadChunkAsync(Vector3I coord, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<ChunkData>(TaskCreationOptions.RunContinuationsAsynchronously);

        bool accepted = await Worker.SendAsync(new LoadOp(coord, tcs), ct);
        if (!accepted)
            throw new InvalidOperationException("[SaveQueue] Not accepting operations.");

        // WaitAsync propagates cancellation while we wait for the worker result
        return await tcs.Task.WaitAsync(ct);
    }

    // Call on game exit. Waits for every operation queued so far; the queue stays usable
    // so a world can be loaded again without restarting the game.
    public static async Task FlushAsync()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await Worker.SendAsync(new FlushOp(tcs));
        await tcs.Task;
    }
}
