using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.Citizen;

namespace Sandbox
{
    public sealed class EqsQuery : Component
    {
        [Property] public bool bDebugMode { get; set; } = false;
		[Property] public bool bUse2D { get; set; } = true; // Utilisation en 2D ou 3D
        [Property] public Vector3 Origin { get; set; }
        [Property] public Vector3 NewLocationToMove { get; set; }

        [Property, Range(0.0f, 500.0f)] public float Radius { get; set; } =	100f; // Rayon de recherche
        
        [Property, Range(1.0f, 50.0f)] public float Step { get; set; } = 10f; // Distance entre les points générés
        [Property] public List<EqsTest> Tests { get; set; } = new List<EqsTest>();

        private NavMeshAgent _agent;
        private CitizenAnimationHelper _animationHelper;
        private List<(Vector3 Point, float Score)> _cachedResults = new();

        public List<(Vector3 Point, float Score)> Execute()
        {
            List<Vector3> potentialPoints = bUse2D ? GeneratePoints2D() : GeneratePoints3D();
            return EvaluatePointsWithScores(potentialPoints);
        }

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
        
        private List<Vector3> GeneratePoints2D()
        {
	        List<Vector3> points = new List<Vector3>();
	        for (float x = -Radius; x <= Radius; x += Step)
	        {
		        for (float y = -Radius; y <= Radius; y += Step)
		        {
			        var point = Origin + new Vector3(x, y, 0);
			        if ((point - Origin).Length <= Radius)
				        points.Add(point);
		        }
	        }
	        return points;
        }

        private List<Vector3> GeneratePoints3D()
        {
	        List<Vector3> points = new List<Vector3>();
	        for (float x = -Radius; x <= Radius; x += Step)
	        {
		        for (float y = -Radius; y <= Radius; y += Step)
		        {
			        for (float z = -Radius; z <= Radius; z += Step)
			        {
				        var point = Origin + new Vector3(x, y, z);
				        if ((point - Origin).Length <= Radius)
					        points.Add(point);
			        }
		        }
	        }
	        return points;
        }

        private List<(Vector3 Point, float Score)> EvaluatePointsWithScores(List<Vector3> points)
        {
            var scoredPoints = new List<(Vector3 Point, float Score)>();

            foreach (var point in points)
            {
                float totalScore = 0.0f;
                float totalWeight = Tests.Sum(test => test.Weight);

                foreach (var test in Tests)
                {
                    totalScore += test.GetScore(point) * test.Weight;
                }

                totalScore /= totalWeight > 0 ? totalWeight : 1.0f; // Normalisation par poids total
                scoredPoints.Add((point, totalScore));
            }

            NormalizeScores(scoredPoints);
            scoredPoints = scoredPoints.Where(p => p.Score > 0.1f).ToList(); // Filtrage des scores faibles

            return scoredPoints;
        }

        private void NormalizeScores(List<(Vector3 Point, float Score)> scoredPoints)
        {
            float maxScore = scoredPoints.Max(p => p.Score);

            for (int i = 0; i < scoredPoints.Count; i++)
            {
                var (point, score) = scoredPoints[i];
                scoredPoints[i] = (point, maxScore > 0 ? score / maxScore : 0.0f);
            }
        }

        private Vector3 SelectPointBasedOnProbability(List<(Vector3 Point, float Score)> scoredPoints)
        {
            float totalScore = scoredPoints.Sum(p => p.Score);
            float randomValue = Game.Random.Float(0.0f, totalScore);

            float cumulativeScore = 0.0f;
            foreach (var (point, score) in scoredPoints)
            {
                cumulativeScore += score;
                if (randomValue <= cumulativeScore)
                {
                    return point;
                }
            }

            return scoredPoints.First().Point; // Fallback
        }

        protected override void OnAwake()
        {
            Origin = this.WorldPosition;
            _agent = GetComponent<NavMeshAgent>();
            _animationHelper = GetComponent<CitizenAnimationHelper>();

            Tests.Add(new VisibilityTest(Origin));
            Tests.Add(new DistanceTest(Origin, 100.0f));

            var results = Execute();
            var selectedPoint = SelectPointBasedOnProbability(results);

            if (bDebugMode)
            {
                VisualizeResults(results);
            }

            if (_agent != null)
            {
                NewLocationToMove = selectedPoint;
                _agent.MoveTo(selectedPoint);
                _animationHelper.MoveStyle = CitizenAnimationHelper.MoveStyles.Walk;
            }
            else
            {
                Log.Error("No NavMeshAgent found on the entity");
            }
        }

        protected override void OnUpdate()
        {
	        Origin = this.WorldPosition;
	        
            if (bDebugMode)
            {
                DebugOverlay.Box(Origin + new Vector3(0, 0, 35), new Vector3(15.0f, 15.0f, 65.0f), Color.Green);
                DebugOverlay.Line(_agent.WorldPosition, NewLocationToMove, Color.Green, 0.0f);
                DebugOverlay.Sphere(Sphere.Unit, Color.Yellow, 0.0f, new Transform(NewLocationToMove), true);
            
                DebugOverlay.Text((Origin + NewLocationToMove) / 2 + new Vector3(0, 0, 10), $"Distance: {(_agent.WorldPosition - NewLocationToMove).Length}", 10.0f, 0.0f, Color.Blue);
                DebugOverlay.Text(Origin + new Vector3(0, 0, 100), $"Move To: {(NewLocationToMove)}", 10.0f, 0.0f, Color.Yellow);
            }

            if (_agent.IsValid() && NewLocationToMove.IsNearlyZero() == false)
            {
                _agent.MoveTo(NewLocationToMove);
                _animationHelper.MoveStyle = CitizenAnimationHelper.MoveStyles.Walk;
            }
        }

        private void VisualizeResults(List<(Vector3 Point, float Score)> results)
        {
            foreach (var (point, score) in results)
            {
                Color pointColor = Color.Lerp(Color.Red, Color.Green, score);
                DebugOverlay.Sphere(Sphere.Unit, pointColor, 5.0f, new Transform(point), true);
                DebugOverlay.Text(point + Vector3.Up * 5, $"Score: {score:0.00}", 5.0f);
            }
        }
    }

    public abstract class EqsTest
    {
        public float Weight { get; set; } = 1.0f;
        public abstract float GetScore(Vector3 point);
    }

    public class VisibilityTest : EqsTest
    {
        private Vector3 _origin;

        public VisibilityTest(Vector3 origin)
        {
            _origin = origin;
        }

        public override float GetScore(Vector3 point)
        {
            return Scene.CreateEditorScene().Trace.Ray(_origin, point).Run().Hit ? 0.0f : 1.0f;
        }
    }

    public class DistanceTest : EqsTest
    {
        private Vector3 _origin;
        private float _desiredDistance;

        public DistanceTest(Vector3 origin, float desiredDistance)
        {
            _origin = origin;
            _desiredDistance = desiredDistance;
        }

        public override float GetScore(Vector3 point)
        {
            float distance = (_origin - point).Length;
            return 1.0f - MathF.Abs(_desiredDistance - distance) / _desiredDistance;
        }
    }

    public class FlatnessTest : EqsTest
    {
        public override float GetScore(Vector3 point)
        {
            var trace = Scene.CreateEditorScene().Trace.Ray(point, point - Vector3.Up * 10).Run();
            return trace.Hit && trace.Normal.Dot(Vector3.Up) > 0.9f ? 1.0f : 0.0f;
        }
    }
}
