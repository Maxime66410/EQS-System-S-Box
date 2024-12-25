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
        
        [Property, Range(0.0f, 1.0f)] public float minScoreThreshold { get; set; } = 0.8f; // Min score threshold - default 0.8 : Min score threshold to select a point
        
        [Property, Range(1.0f, 50.0f)] public float StopDistance { get; set; } = 30f; // Stop distance - default 15 : Distance to stop

        [Property] public List<EqsTest> Tests { get; set; } = new List<EqsTest>(); // List of tests
        
        
        private Vector3 _lastPosition; // Last position
        private float _blockCheckInterval = 1.0f; // Block check interval
        private float _timeSinceLastCheck = 0.0f; // Time since last check
        private bool _isAgentActive = false; // Is agent active

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
	        float localRadius = Radius;
	        
	        // Check if Tests we are Overriding the Radius
	        if (Tests.Any(test => test is OverrideRadius))
	        {
		        localRadius = Tests.First(test => test is OverrideRadius).GetScore(Vector3.Zero);
	        }
	        
	        List<Vector3> points = new List<Vector3>();
	        for (float x = -localRadius; x <= localRadius; x += Step) // Step is the distance between each point
	        {
		        for (float y = -localRadius; y <= localRadius; y += Step)
		        {
			        var point = Origin + new Vector3(x, y, 0);
			        if ((point - Origin).Length <= localRadius) // Check if the point is inside the sphere
				        points.Add(point);
		        }
	        }
	        return points;
        }

        // Generate points in 3D mode : Generate points in a sphere
        private List<Vector3> GeneratePoints3D()
        {
	        float localRadius = Radius;
	        
	        // Check if Tests we are Overriding the Radius
	        if (Tests.Any(test => test is OverrideRadius))
	        {
		        localRadius = Tests.First(test => test is OverrideRadius).GetScore(Vector3.Zero);
	        }
	        
	        List<Vector3> points = new List<Vector3>();
	        for (float x = -localRadius; x <= localRadius; x += Step) // Step is the distance between each point
	        {
		        for (float y = -localRadius; y <= localRadius; y += Step)
		        {
			        for (float z = -localRadius; z <= localRadius; z += Step)
			        {
				        var point = Origin + new Vector3(x, y, z);
				        if ((point - Origin).Length <= localRadius) // Check if the point is inside the sphere
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
                float totalWeight = Tests.Sum(test => test.Weight * test.Priority); // Sum of the weights

                foreach (var test in Tests)
                {
                    totalScore += test.GetScore(point) * test.Weight * test.Priority; // Get the score of the point
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
        private Vector3 SelectPointBasedOnProbability(List<(Vector3 Point, float Score)> scoredPoints, float localminScoreThreshold = 0.8f)
        {
	        // Filter the points based on the min score threshold
	        var maxPriority = Tests.Max(test => test.Priority);
	        var filteredPoints = scoredPoints.Where(p => p.Score >= localminScoreThreshold).ToList() // Filter the points with a score >= minScoreThreshold
		        .Where(p => Tests.Where(test => test.Priority == maxPriority).All(test => test.GetScore(p.Point) > 0.0f)).ToList(); // Filter the points with the max priority

	        if (filteredPoints.Count == 0)
	        {
		        // If no points are above the threshold, use all points
		        filteredPoints = scoredPoints;
	        }

	        float totalScore = filteredPoints.Sum(p => p.Score); // Score total
	        float randomValue = Game.Random.Float(0.0f, totalScore); // Random value

	        float cumulativeScore = 0.0f; // Cumulative score
	        foreach (var (point, score) in filteredPoints) // Select a point based on the scores
	        {
		        cumulativeScore += score;
		        if (randomValue <= cumulativeScore)
		        {
			        return point;
		        }
	        }

	        return filteredPoints.First().Point; // Return the first point
        }
        
        // Execute the query : Generate points, evaluate points with scores and return the results
        public void ExecuteQuery(VisibilityTest visibilityTest = null, DistanceTest distanceTest = null, Dictionary<string, object> customParams = null)
		{
			Origin = this.WorldPosition;
			Tests.Clear();

			int customPriority = 1;
			
			if (visibilityTest != null) // Add the visibility test
			{
				Tests.Add(visibilityTest);
			}
			else
			{
				Tests.Add(new VisibilityTest(Origin) { Priority = customPriority });
				Log.Warning("No VisibilityTest provided, using default");
			}
			
			if (distanceTest != null) // Add the distance test
			{
				Tests.Add(distanceTest);
			}
			else
			{
				Tests.Add(new DistanceTest(Origin, DesiredDistance) { Priority = customPriority });
				Log.Warning("No DistanceTest provided, using default");
			}
			
			// Custom params from VisibilityOrigin
			if (customParams != null && customParams.ContainsKey("VisibilityOrigin") && customParams["VisibilityOrigin"] is Vector3 visibilityOrigin)
			{
				Tests.Add(new VisibilityTest(visibilityOrigin) { Priority = customPriority });
			}

			// Custom params from DesiredDistance
			if (customParams != null && customParams.ContainsKey("DesiredDistance") && customParams["DesiredDistance"] is float desiredDistance)
			{
				Tests.Add(new DistanceTest(Origin, desiredDistance) { Priority = customPriority });
			}
			
			// Full custom params : is for better request mode for better results & AI
			if (customParams != null)
			{
				foreach (var parameter in customParams)
				{
					if(bDebugMode) Log.Info($"Custom Parameter: {parameter.Key} = {parameter.Value}");
					if(bDebugMode) Log.Info($"Custom Priority: {customPriority}");
					
					switch (parameter.Key)
					{
						case "Priority": // If the user wants to set the priority to high
							if (parameter.Value is String highPriority)
							{
								if (highPriority == "High")
								{
									Log.Info($"High priority set to: {highPriority}");
									Tests.Add(new DistanceTest(Origin, DesiredDistance) { Weight =  2.0f, Priority = 3});
									customPriority = 3;
								}
								else if (highPriority == "Medium")
								{
									Log.Info($"Medium priority set to: {highPriority}");
									Tests.Add(new DistanceTest(Origin, DesiredDistance) { Weight =  1.0f, Priority = 2});
									customPriority = 2;
								}
								else if (highPriority == "Low")
								{
									Log.Info($"Low priority set to: {highPriority}");
									Tests.Add(new DistanceTest(Origin, DesiredDistance) { Weight =  0.5f, Priority = 1});
									customPriority = 1;
								}
								else
								{
									Log.Warning($"Invalid value for 'HighPriority', got {parameter.Value}");
								}
							}
							else
							{
								Log.Warning($"Invalid value for 'HighPriority'. Expected a bool, got {parameter.Value?.GetType()}");
							}
							break;
						
						case "TargetObject": // If the user wants to search for an entity with a GameObject
							if (parameter.Value is GameObject targetEntity)
							{
								if ( targetEntity.IsValid() )
								{
									Log.Info($"Searching for entity: {targetEntity}");
									Tests.Add(new TargetEntityTest(targetEntity) { Priority = customPriority });
									Tests.RemoveAll(test => test is VisibilityTest);
									Tests.RemoveAll(test => test is DistanceTest);
								}
								else
								{
									Log.Warning($"Invalid value for 'TargetObject'. Expected an Entity, got {parameter.Value?.GetType()}");
								}
							}
							else
							{
								Log.Warning($"Invalid value for 'TargetObject'. Expected an Entity, got {parameter.Value?.GetType()}");
							}
							break;
						
						case "TargetObjectWithTag": // If the user wants to search for an entity with a tag
							if (parameter.Value is String targetEntityWithTag)
							{
								foreach ( var item in Scene.GetAllObjects( true ).Where( X => X.Tags.Has( targetEntityWithTag ) ) )
								{
									if(item.IsValid())
									{
										Log.Info($"Searching for entity with tag: {targetEntityWithTag}");
										Tests.Add(new TargetEntityTest(item) { Priority = customPriority });
										Tests.RemoveAll(test => test is VisibilityTest);
										Tests.RemoveAll(test => test is DistanceTest);
									}
									else
									{
										Log.Warning($"Invalid value for 'TargetObject'. Expected an Entity, got {parameter.Value?.GetType()}");
									}
								}
							}
							else
							{
								Log.Warning($"Invalid value for 'TargetObject'. Expected an Entity, got {parameter.Value?.GetType()}");
							}
							break;

						case "RadiusOverride": // If the user wants to override the search radius
							if (parameter.Value is float newRadius)
							{
								Log.Info($"Overriding search radius: {newRadius}");
								Tests.Add(new OverrideRadius(newRadius) { Priority = customPriority });
							}
							else
							{
								Log.Warning($"Invalid value for 'RadiusOverride'. Expected a float, got {parameter.Value?.GetType()}");
							}
							break;

						case "DebugMode": // If the user wants to enable debug mode
							if (parameter.Value is bool debug)
							{
								Log.Info($"Debug mode set to: {debug}");
								bDebugMode = debug;
							}
							else
							{
								Log.Warning($"Invalid value for 'DebugMode'. Expected a bool, got {parameter.Value?.GetType()}");
							}
							break;

						default:
							Log.Warning($"Unknown custom parameter: {parameter.Key}");
							break;
					}
				}
			}
			
			// Check if we have a test for TargetEntity
			if (Tests.Any(test => test is TargetEntityTest))
			{
				Tests.RemoveAll(test => test is VisibilityTest);
				Tests.RemoveAll(test => test is DistanceTest);
			}
			
			var results = Execute(); // Execute the query
			var selectedPoint = SelectPointBasedOnProbability(results, minScoreThreshold); // Select a point based on the probability

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
				_isAgentActive = true;
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
                DebugOverlay.Sphere(Sphere.Unit, pointColor, 2.0f, new Transform(point), true);
                DebugOverlay.Text(point + Vector3.Up * 2, $"Score: {score:0.00}", 5.0f, 0.0f, Color.White, 2.0f);
            }
        }
        
        private bool IsAgentBlocked(Vector3 currentPosition, Vector3 lastPosition, float tolerance = 0.1f)
        {
	        return (currentPosition - lastPosition).Length <= tolerance;
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
				//{ "Priority", "High" },
				//{ "TargetObjectWithTag", "player" },
				//{ "RadiusOverride", 500.0f },
				//{ "DebugMode", true }
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
            
            if ( _agent.IsValid() && _isAgentActive)
            {
	            _timeSinceLastCheck += Time.Delta;
	            
	            // Check if the agent is blocked
	            if ( _timeSinceLastCheck >= _blockCheckInterval )
	            {
		            _timeSinceLastCheck = 0.0f;

		            if ( IsAgentBlocked( _agent.WorldPosition, _lastPosition ) )
		            {
			            _agent.Stop();
			            _agent.Velocity = Vector3.Zero;
			            _lastPosition = _agent.WorldPosition;
			            _isAgentActive = false;
			            Log.Warning($"Agent {_agent.GameObject.Name} is blocked. Aborting current task.");
		            }
		            
		            _lastPosition = _agent.WorldPosition;
	            }
	            
	            if((_agent.WorldPosition - NewLocationToMove).Length <= StopDistance)
	            {
		            _agent.Stop();
		            _agent.Velocity = Vector3.Zero;
		            _isAgentActive = false;
		            
		            // Recall the query
		            ExecuteQuery();
	            }
            }
        }
    }

    // Extension methods for the EqsQuery
    public abstract class EqsTest 
    {
        public float Weight { get; set; } = 1.0f; // Weight - default 1 : Weight of the test
        
        public int Priority { get; set; } = 1; // Priority - default 0 : Priority of the test
		
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
	        var score = 1.0f;
	        score = Scene.CreateEditorScene().Trace.Ray(_origin, point).Run().Hit ? 0.0f : 1.0f; // Check if the point is visible
	        if(Math.Abs(score - 1.0f) < 0.01f) score = Scene.CreateEditorScene().Trace.Ray(point, point - Vector3.Up * 10).Run().Hit ? 1.0f : 0.0f; // Check if the point is not in void
            return score; // Return the score
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
    
    // Override radius test : Override the radius of the sphere
    public class OverrideRadius : EqsTest
    {
	    private float _radiusoverride;

	    public OverrideRadius(float desiredDistance)
	    {
		    _radiusoverride = desiredDistance;
	    }

	    // Get the score of the point
	    public override float GetScore(Vector3 point)
	    {
		    return _radiusoverride; // Return the score
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
    
    // Target entity test : Check the distance between the point and the target entity
    public class TargetEntityTest : EqsTest
    {
	    private GameObject TargetEntity;

	    public TargetEntityTest(GameObject targetEntity, float weight = 1.0f)
	    {
		    TargetEntity = targetEntity;
		    Weight = weight;
	    }

	    public override float GetScore(Vector3 point)
	    {
		    if (!TargetEntity.IsValid()) return 0.0f;

		    // Score based on distance to the target entity
		    float distance = (TargetEntity.WorldPosition - point).Length;
		    return distance > 0 ? 2.0f / distance : 0.0f; // Return the score
	    }
    }
}
