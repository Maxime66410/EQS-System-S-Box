using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.Citizen;

namespace Sandbox
{
    public sealed class EqsQuery : Component
    {
        [Property] public bool bDebugMode { get; set; } = false; // Debug mode
		[Property] public bool bUse2D { get; set; } = true; // 2D or 3D
        [Property] public Vector3 Origin { get; set; } // Origin point
        [Property] public Vector3 NewLocationToMove { get; set; } // New location to move

        [Property, Range(0.0f, 500.0f)] public float Radius { get; set; } =	100f; // Radius - default 100 : Radius of the sphere
        
        [Property, Range(1.0f, 50.0f)] public float Step { get; set; } = 10f;  // Step - default 10 : Step is the distance between each point
        
        [Property, Range(1.0f, 500.0f)] public float DesiredDistance { get; set; } = 50f; // Desired distance - default 50 : Distance to the target
        
        [Property, Range(1.0f, 50.0f)] public float StopDistance { get; set; } = 30f; // Stop distance - default 15 : Distance to stop

        [Property] public List<EqsTest> Tests { get; set; } = new List<EqsTest>(); // List of tests

        private NavMeshAgent _agent; // NavMeshAgent
        private CitizenAnimationHelper _animationHelper; // CitizenAnimationHelper
        private List<(Vector3 Point, float Score)> _cachedResults = new(); // Cached results
        

        // Execute the query : Generate points, evaluate points with scores and return the results
        public List<(Vector3 Point, float Score)> Execute()
        {
            List<Vector3> potentialPoints = bUse2D ? GeneratePoints2D() : GeneratePoints3D(); // Generate points
            return EvaluatePointsWithScores(potentialPoints); // Evaluate points with scores
        }

        // Generate points Based on 2D or 3D mode (Deprecated)
        private List<Vector3> GeneratePoints()
        {
	        List<Vector3> points = new List<Vector3>();
	        float step = 10f;

	        for (float x = -Radius; x <= Radius; x += step)
	        {
		        for (float y = -Radius; y <= Radius; y += step)
		        {
			        float zMin = bUse2D ? 0 : -Radius;
			        float zMax = bUse2D ? 0 : Radius;

			        for (float z = zMin; z <= zMax; z += (bUse2D ? Radius : step))
			        {
				        var point = Origin + new Vector3(x, y, z);

				        if ((point - Origin).Length <= Radius)
					        points.Add(point);
			        }
		        }
	        }

	        return points;
        }
        
        // Generate points in 2D mode : Generate points in a 2D circle
        private List<Vector3> GeneratePoints2D()
        {
	        List<Vector3> points = new List<Vector3>();
	        for (float x = -Radius; x <= Radius; x += Step) // Step is the distance between each point
	        {
		        for (float y = -Radius; y <= Radius; y += Step)
		        {
			        var point = Origin + new Vector3(x, y, 0);
			        if ((point - Origin).Length <= Radius) // Check if the point is inside the sphere
				        points.Add(point);
		        }
	        }
	        return points;
        }

        // Generate points in 3D mode : Generate points in a sphere
        private List<Vector3> GeneratePoints3D()
        {
	        List<Vector3> points = new List<Vector3>();
	        for (float x = -Radius; x <= Radius; x += Step) // Step is the distance between each point
	        {
		        for (float y = -Radius; y <= Radius; y += Step)
		        {
			        for (float z = -Radius; z <= Radius; z += Step)
			        {
				        var point = Origin + new Vector3(x, y, z);
				        if ((point - Origin).Length <= Radius) // Check if the point is inside the sphere
					        points.Add(point);
			        }
		        }
	        }
	        return points;
        }

        // Evaluate points with scores : Evaluate each point and normalize the scores
        private List<(Vector3 Point, float Score)> EvaluatePointsWithScores(List<Vector3> points)
        {
            var scoredPoints = new List<(Vector3 Point, float Score)>();

            foreach (var point in points) // Evaluate each point
            {
                float totalScore = 0.0f;
                float totalWeight = Tests.Sum(test => test.Weight); // Sum of the weights

                foreach (var test in Tests)
                {
                    totalScore += test.GetScore(point) * test.Weight; // Get the score of the point
                }

                totalScore /= totalWeight > 0 ? totalWeight : 1.0f; // Normalize the score
                scoredPoints.Add((point, totalScore)); // Add the point and the score
            }

            NormalizeScores(scoredPoints);
            scoredPoints = scoredPoints.Where(p => p.Score > 0.1f).ToList(); // Filter the points with a score > 0.1

            return scoredPoints;
        }

        // Normalize the scores : Get the max score and normalize the scores
        private void NormalizeScores(List<(Vector3 Point, float Score)> scoredPoints)
        {
            float maxScore = scoredPoints.Max(p => p.Score); // Get the max score

            for (int i = 0; i < scoredPoints.Count; i++) // Normalize the scores
            {
                var (point, score) = scoredPoints[i]; // Get the point and the score
                scoredPoints[i] = (point, maxScore > 0 ? score / maxScore : 0.0f); // Normalize the score
            }
        }

        // Select a point based on the probability : Select a point based on the scores
        private Vector3 SelectPointBasedOnProbability(List<(Vector3 Point, float Score)> scoredPoints)
        {
            float totalScore = scoredPoints.Sum(p => p.Score); // Sum of the scores
            float randomValue = Game.Random.Float(0.0f, totalScore); // Random value

            float cumulativeScore = 0.0f; // Cumulative score
            foreach (var (point, score) in scoredPoints) // Select the point based on the scores
            {
                cumulativeScore += score;
                if (randomValue <= cumulativeScore) // Select the point
                {
                    return point;
                }
            }

            return scoredPoints.First().Point; // Fallback
        }

        // Awake : Called when the entity is created
        protected override void OnAwake()
        {
            Origin = this.WorldPosition;
            _agent = GetComponent<NavMeshAgent>();
            _animationHelper = GetComponent<CitizenAnimationHelper>();

            if (!_agent.IsValid())
			{
				Log.Error("No NavMeshAgent found on the entity");
			}
            
            if(!_animationHelper.IsValid())
			{
				Log.Error("No CitizenAnimationHelper found on the entity");
			}
        }
        
        // Start : Called when the entity is started
        protected override void OnStart()
		{
			// Example of custom parameters
			var customParams = new Dictionary<string, object>
			{
				{ "MaxIterations", 500 },
				{ "DebugColor", Color.Blue },
				{ "Radius", 250.0f },
				{ "Priority", "High" }
			};
			
			// Execute the query
			ExecuteQuery(new VisibilityTest(Origin), new DistanceTest(Origin, DesiredDistance), customParams);
		}

        // Update : Called every frame
        protected override void OnUpdate()
        {
	        Origin = this.WorldPosition;
	        
            if (bDebugMode) // Debug mode
            {
                DebugOverlay.Box(Origin + new Vector3(0, 0, 35), new Vector3(15.0f, 15.0f, 65.0f), Color.Green);
                DebugOverlay.Line(_agent.WorldPosition, NewLocationToMove, Color.Green, 0.0f);
                DebugOverlay.Sphere(Sphere.Unit, Color.Yellow, 0.0f, new Transform(NewLocationToMove), true);
            
                DebugOverlay.Text((Origin + NewLocationToMove) / 2 + new Vector3(0, 0, 10), $"Distance: {(_agent.WorldPosition - NewLocationToMove).Length}", 10.0f, 0.0f, Color.Blue);
                DebugOverlay.Text(Origin + new Vector3(0, 0, 100), $"Move To: {(NewLocationToMove)}", 10.0f, 0.0f, Color.Yellow);
            }
            
            if(_animationHelper.IsValid()) _animationHelper.WithVelocity(_agent.Velocity); // Update the velocity of the entity

            if ( _agent.IsValid() )
            {
	            if((_agent.WorldPosition - NewLocationToMove).Length <= StopDistance)
	            {
		            _agent.Stop();
		            _agent.Velocity = Vector3.Zero;
		            
		            // Recall the query
		            ExecuteQuery();
	            }
            }
        }

        // Execute the query : Generate points, evaluate points with scores and return the results
        public void ExecuteQuery(VisibilityTest visibilityTest = null, DistanceTest distanceTest = null, Dictionary<string, object> customParams = null)
		{
			Tests.Clear();
			
			if (visibilityTest != null) // Add the visibility test
			{
				Tests.Add(visibilityTest);
			}
			else
			{
				Tests.Add(new VisibilityTest(Origin));
				Log.Warning("No VisibilityTest provided, using default");
			}
			
			if (distanceTest != null) // Add the distance test
			{
				Tests.Add(distanceTest);
			}
			else
			{
				Tests.Add(new DistanceTest(Origin, DesiredDistance));
				Log.Warning("No DistanceTest provided, using default");
			}
			
			// Custom params from VisibilityOrigin
			if (customParams != null && customParams.ContainsKey("VisibilityOrigin") && customParams["VisibilityOrigin"] is Vector3 visibilityOrigin)
			{
				Tests.Add(new VisibilityTest(visibilityOrigin));
			}

			// Custom params from DesiredDistance
			if (customParams != null && customParams.ContainsKey("DesiredDistance") && customParams["DesiredDistance"] is float desiredDistance)
			{
				Tests.Add(new DistanceTest(Origin, desiredDistance));
			}
			
			// Full custom params : is for better request mode for better results & AI
			if (customParams != null)
			{
				foreach (var parameter in customParams)
				{
					Log.Info($"Custom Parameter: {parameter.Key} = {parameter.Value}"); // Debug FOR THE MOMENT
				}
			}
			
			var results = Execute(); // Execute the query
			var selectedPoint = SelectPointBasedOnProbability(results); // Select a point based on the probability

			if (bDebugMode) // Debug mode
			{
				VisualizeResults(results);
			}

			if (_agent != null) // Move the entity to the selected point
			{
				NewLocationToMove = selectedPoint;
				_agent.MoveTo(NewLocationToMove);
				_animationHelper.MoveStyle = CitizenAnimationHelper.MoveStyles.Run;
				_animationHelper.WithVelocity(_agent.Velocity);
			}
			else
			{
				Log.Error("No NavMeshAgent found on the entity");
			}
			
			_cachedResults.Add((selectedPoint, results.First(p => p.Point == selectedPoint).Score)); // Add the selected point and the score to the cached results
		}

        // Visualize the results : Allow to visualize the results
        private void VisualizeResults(List<(Vector3 Point, float Score)> results)
        {
            foreach (var (point, score) in results)
            {
                Color pointColor = Color.Lerp(Color.Red, Color.Green, score);
                DebugOverlay.Sphere(Sphere.Unit, pointColor, 5.0f, new Transform(point), true);
                DebugOverlay.Text(point + Vector3.Up * 2, $"Score: {score:0.00}", 5.0f, 0.0f, Color.White, 5.0f);
            }
        }
    }

    // Extension methods for the EqsQuery
    public abstract class EqsTest 
    {
        public float Weight { get; set; } = 1.0f; // Weight - default 1 : Weight of the test
        public abstract float GetScore(Vector3 point); // Get the score of the point
    }

    // Visibility test : Check if the point is visible
    public class VisibilityTest : EqsTest
    {
        private Vector3 _origin;

        public VisibilityTest(Vector3 origin)
        {
            _origin = origin;
        }

        // Get the score of the point
        public override float GetScore(Vector3 point)
        {
            return Scene.CreateEditorScene().Trace.Ray(_origin, point).Run().Hit ? 0.0f : 1.0f; // Check if the point is visible
        }
    }

    
    // Distance test : Check the distance between the point and the origin
    public class DistanceTest : EqsTest
    {
        private Vector3 _origin;
        private float _desiredDistance;

        public DistanceTest(Vector3 origin, float desiredDistance)
        {
            _origin = origin;
            _desiredDistance = desiredDistance;
        }

        // Get the score of the point
        public override float GetScore(Vector3 point)
        {
            float distance = (_origin - point).Length; // Get the distance between the point and the origin
            return 1.0f - MathF.Abs(_desiredDistance - distance) / _desiredDistance; // Return the score
        }
    }

    // Flatness test : Check if the point is flat
    public class FlatnessTest : EqsTest
    {
        public override float GetScore(Vector3 point)
        {
            var trace = Scene.CreateEditorScene().Trace.Ray(point, point - Vector3.Up * 10).Run(); // Check if the point is flat
            return trace.Hit && trace.Normal.Dot(Vector3.Up) > 0.9f ? 1.0f : 0.0f; // Return the score
        }
    }
}
