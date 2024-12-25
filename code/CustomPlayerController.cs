using Sandbox;

namespace Sandbox
{
	public sealed class CustomPlayerController : Component
	{
		protected override void OnStart()
		{
			Mouse.Visible = true;
			
			Log.Info( "CustomPlayerController started!" );
		}
	}
}
