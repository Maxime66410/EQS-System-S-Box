using System;
using Sandbox;
using System.Collections.Generic;
using Sandbox.Citizen;

namespace Sandbox;

public sealed class Eqsquery : Component
{
    [Property] public bool bDebugMode { get; set; } = false;
    [Property] public Vector3 Origin { get; set; }
    [Property] public Vector3 NewLocationToMove { get; set; }

    [Property, Range(0.0f, 500.0f)] public float Radius { get; set; }
    [Property] public List<EqsTest> Tests { get; set; } = new List<EqsTest>();

    // Private members
    private NavMeshAgent _agent;
    private CitizenAnimationHelper _animationHelper;

    public List<(Vector3 Point, float Score)> Execute()
    {
        List<Vector3> potentialPoints = GeneratePoints();
        return EvaluatePointsWithScores(potentialPoints);
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

    private List<(Vector3 Point, float Score)> EvaluatePointsWithScores(List<Vector3> points)
    {
        var scoredPoints = new List<(Vector3 Point, float Score)>();

        foreach (var point in points)
        {
            float totalScore = 0.0f;

            // Évaluation des tests
            foreach (var test in Tests)
            {
                totalScore += test.GetScore(point);
            }

            scoredPoints.Add((point, totalScore));
        }

        // Normaliser les scores pour les ramener entre 0 et 1
        NormalizeScores(scoredPoints);

        return scoredPoints;
    }

    private void NormalizeScores(List<(Vector3 Point, float Score)> scoredPoints)
    {
        float maxScore = scoredPoints.Max(p => p.Score);

        for (int i = 0; i < scoredPoints.Count; i++)
        {
            var (point, score) = scoredPoints[i];
            scoredPoints[i] = (point, maxScore > 0 ? score / maxScore : 0.0f); // Éviter la division par zéro
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

        // Si rien n'est sélectionné (rare), retourne un point aléatoire
        return scoredPoints[0].Point;
    }

    protected override void OnAwake()
    {
        Origin = this.WorldPosition;
        _agent = GetComponent<NavMeshAgent>();
        _animationHelper = GetComponent<CitizenAnimationHelper>();

        var query = this;

        // Ajouter les tests (exemple avec un test de visibilité)
        query.Tests.Add(new VisibilityTest(Origin));
        query.Tests.Add(new DistanceTest(Origin, 100.0f)); // Exemple de test de distance

        var results = query.Execute();
        var selectedPoint = SelectPointBasedOnProbability(results);

        if (bDebugMode)
        {
            foreach (var (point, score) in results)
            {
                // Représentation des scores en débogage
                Color pointColor = Color.Lerp(Color.Red, Color.Green, score); // Plus le score est élevé, plus la couleur est verte
                DebugOverlay.Sphere(Sphere.Unit, pointColor, 5.0f, new Transform(point), true);
                
                // Print score in 3D
                DebugOverlay.Text(point + new Vector3(0, 0, 10), $"Score : {(score)}", 5.0f, 0.0f, Color.Magenta,5.0f);
                DebugOverlay.Line(point, point + new Vector3(0, 0, 8), Color.White, 5.0f);
            }
        }

        // Déplacement de l'agent
        if (_agent != null && selectedPoint != null)
        {
            Log.Info("Moving agent to point: " + selectedPoint);
            NewLocationToMove = selectedPoint;
            _agent.MoveTo(selectedPoint);
            _animationHelper.MoveStyle = CitizenAnimationHelper.MoveStyles.Walk;
            _animationHelper.WithVelocity(_agent.Velocity);
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

        if (_agent.IsValid() && _animationHelper.IsValid())
        {
            _agent.MoveTo(NewLocationToMove);
            _animationHelper.MoveStyle = CitizenAnimationHelper.MoveStyles.Walk;
            _animationHelper.WithVelocity(_agent.Velocity);
        }
    }
}

public abstract class EqsTest
{
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
        return Scene.CreateEditorScene().Trace.Ray(_origin, point).Run().Hit ? 0.0f : 1.0f; // 1 si visible, sinon 0
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
        return 1.0f - MathF.Abs(_desiredDistance - distance) / _desiredDistance; // Score basé sur la proximité de la distance désirée
    }
}
