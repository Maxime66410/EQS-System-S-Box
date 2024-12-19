using Sandbox;
using System.Collections.Generic;
using Sandbox.Citizen;

namespace Sandbox;

public sealed class Eqsquery : Component
{
	[Property] public bool bDebugMode { get; set; } = false;
	[Property] public Vector3 Origin { get; set; }
	
	[Property] public Vector3 NewLocationToMove { get; set; }

	[Property,Range(0.0f, 500.0f)] public float Radius { get; set; }
	[Property] public List<EqsTest> Tests { get; set; } = new List<EqsTest>();

	// Private members
	private NavMeshAgent _agent;
	private CitizenAnimationHelper _animationHelper;
	
	public List<Vector3> Execute()
	{
		List<Vector3> potentialPoints = GeneratePoints();
		return EvaluatePoints(potentialPoints);
	}

	private List<Vector3> GeneratePoints()
	{
		List<Vector3> points = new List<Vector3>();
		for (float x = -Radius; x <= Radius; x += 10f)
		{
			for (float y = -Radius; y <= Radius; y += 10f)
			{
				var point = Origin + new Vector3(x, y, 0);
				if ((point - Origin).Length <= Radius)
					points.Add(point);
			}
		}
		return points;
	}
	
	public List<Vector3> Filter(List<Vector3> points)
	{
		List<Vector3> visiblePoints = new List<Vector3>();
		foreach (var point in points)
		{
			if (Scene.Trace.Ray(Origin, point).Run().Hit == false)
			{
				visiblePoints.Add(point);
			}
		}
		return visiblePoints;
	}

	private List<Vector3> EvaluatePoints(List<Vector3> points)
	{
		foreach (var test in Tests)
		{
			points = test.Filter(points);
		}
		return points;
	}
	
	protected override void OnAwake()
	{
		Origin = this.WorldPosition;
		_agent = GetComponent<NavMeshAgent>();
		_animationHelper = GetComponent<CitizenAnimationHelper>();
		
		var query = this;
		query.Tests.Add(new VisibilityTest(Origin));

		var results = query.Execute();

		foreach (var point in results)
		{
			if ( bDebugMode )
			{
				Transform localDrawDebugSphere = new Transform();
				localDrawDebugSphere.Position = point;
				// Make a debug sphere at each point
				DebugOverlay.Sphere(Sphere.Unit, Color.Red, 5.0f, localDrawDebugSphere, true);
			}
			
			// Move the Agent to the point
			
			if (_agent != null)
			{
				Log.Info("Moving agent to point: " + point);
				NewLocationToMove = point;
				_agent.MoveTo(point);
				_animationHelper.MoveStyle = CitizenAnimationHelper.MoveStyles.Walk;
				_animationHelper.WithVelocity(_agent.Velocity);
			}
			else
			{
				Log.Error("No NavMeshAgent found on the entity");
			}
			
			break; // DO ONCE FOR TESTING
		}
	}
	
	protected override void OnUpdate()
	{
		Origin = this.WorldPosition;

		if ( bDebugMode )
		{
			// Box debug draw for the origin
			DebugOverlay.Box(Origin, new Vector3(15.0f, 15.0f, 150.0f), Color.Green);
			
			DebugOverlay.Line(_agent.WorldPosition, NewLocationToMove, Color.Green, 0.0f);
			DebugOverlay.Sphere(Sphere.Unit, Color.Yellow, 0.0f, new Transform(NewLocationToMove), true);
			
			DebugOverlay.Text((Origin + NewLocationToMove) / 2 + new Vector3(0, 0, 10), $"Distance: {(_agent.WorldPosition - NewLocationToMove).Length}", 10.0f, 0.0f, Color.Blue);
			DebugOverlay.Text(Origin+ new Vector3(0, 0, 100), $"Move To: {(NewLocationToMove)}", 10.0f, 0.0f, Color.Yellow);
			
		}
		
		if(_agent.IsValid() && _animationHelper.IsValid())
		{
			_agent.MoveTo(NewLocationToMove);
			_animationHelper.MoveStyle = CitizenAnimationHelper.MoveStyles.Walk;
			_animationHelper.WithVelocity(_agent.Velocity);
		}
	}
}

public abstract class EqsTest
{
	public abstract List<Vector3> Filter(List<Vector3> points);
}


public class VisibilityTest : EqsTest
{
	private Vector3 _origin;

	public VisibilityTest(Vector3 origin)
	{
		_origin = origin;
	}

	public override List<Vector3> Filter(List<Vector3> points)
	{
		List<Vector3> visiblePoints = new List<Vector3>();
		foreach (var point in points)
		{
			if (Scene.CreateEditorScene().Trace.Ray(_origin, point).Run().Hit == false)
			{
				visiblePoints.Add(point);
			}
		}
		return visiblePoints;
	}
}
