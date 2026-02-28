using Godot;

namespace EtherRealm3D.Scripts;

public partial class PauseScreen : Control
{
	public override void _Ready()
	{
		Visible = false;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("pause"))
		{
			Visible = !Visible;
			if (!Visible)
				Input.SetMouseMode(Input.MouseModeEnum.Captured);
			else
				Input.SetMouseMode(Input.MouseModeEnum.Visible);
			//GetTree().Paused = Visible;
		}
	}

	public override void _Process(double delta)
	{
	}
	
	public void BtnResumePressed()
	{
		Visible = false;
	}
	
	public void BtnExitPressed()
	{
		//GetTree().Quit();
	}
}
