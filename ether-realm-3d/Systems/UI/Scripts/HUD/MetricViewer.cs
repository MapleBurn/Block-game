using Godot;

namespace EtherRealm3D.Systems.UI.Scripts.HUD;

public partial class MetricViewer : FoldableContainer
{
    [Export] private Label _fpsLabel;
    [Export] private Label _posLabel;
    [Export] private Label _chunkLabel;
    [Export] private Entities.Player.Player _player;

    public override void _Ready()
    {
        
    }

    public override void _Process(double delta)
    {
        var fps = 1f / delta;
        _fpsLabel.Text = "FPS: " + fps.ToString("0");
        _posLabel.Text = "Position: \nX = " + _player.GlobalPosition.X + "\nY = " + _player.GlobalPosition.Y + "\nZ = " + _player.GlobalPosition.Z;
        _chunkLabel.Text = "Chunks: null";
    }
}