using UnityEngine;

// Clinical RWS bubble-task proxy scoring (RSA Q1–Q4).
// Each frontal quadrant contributes up to 0.25; total RSA Q1–Q4 ranges 0–1.0.
// Quadrant mapping matches SolidSphereFollower grid indices:
//   code 0 = Q3 upper-left, 1 = Q4 lower-left, 2 = Q1 upper-right, 3 = Q2 lower-right.
public static class RWSScoreCalculator
{
    public const float MaxQuadrantRsa = 0.25f;
    public const float MaxTotalRsaFrontal = 1.0f;

    static readonly string[] ClinicalQuadrantNames = { "Q3 UL", "Q4 LL", "Q1 UR", "Q2 LR" };

    public struct PhaseScore
    {
        public int phase;
        public bool isPhase3Rear;
        public int totalBubbles;
        public int totalPopped;
        public float totalRsa;
        public float totalPopPercent;
        public float[] quadrantRsa;
        public int[] quadrantPopped;
        public int[] quadrantTotal;
    }

    public static PhaseScore Calculate(SolidSphereFollower task, int historyIndex)
    {
        var score = new PhaseScore
        {
            phase = historyIndex == 4 ? 3 : (historyIndex == 5 ? 4 : historyIndex),
            isPhase3Rear = historyIndex == 4,
            quadrantRsa = new float[4],
            quadrantPopped = new int[4],
            quadrantTotal = new int[4]
        };

        if (task == null || historyIndex < 1 || historyIndex > 5) return score;

        for (int q = 0; q < 4; q++)
        {
            score.quadrantTotal[q] = task.GetQuadrantBubbleTotal(historyIndex, q);
            score.quadrantPopped[q] = task.GetQuadrantPoppedTotal(historyIndex, q);
            score.totalBubbles += score.quadrantTotal[q];
            score.totalPopped += score.quadrantPopped[q];

            float qPct = score.quadrantTotal[q] > 0
                ? (float)score.quadrantPopped[q] / score.quadrantTotal[q]
                : 0f;
            score.quadrantRsa[q] = qPct * MaxQuadrantRsa;
            score.totalRsa += score.quadrantRsa[q];
        }

        score.totalPopPercent = score.totalBubbles > 0
            ? (float)score.totalPopped / score.totalBubbles
            : 0f;

        return score;
    }

    public static string FormatLivePanel(in PhaseScore score, string phaseLabel)
    {
        return
            $"{phaseLabel}\n" +
            $"Total RSA: {score.totalRsa:0.000} / {MaxTotalRsaFrontal:0.00}  ({score.totalRsa * 100f:0}%)\n" +
            $"Bubbles: {score.totalPopped}/{score.totalBubbles}  ({score.totalPopPercent * 100f:0}%)\n" +
            $"{ClinicalQuadrantNames[2]} {score.quadrantRsa[2]:0.000}   {ClinicalQuadrantNames[3]} {score.quadrantRsa[3]:0.000}\n" +
            $"{ClinicalQuadrantNames[0]} {score.quadrantRsa[0]:0.000}   {ClinicalQuadrantNames[1]} {score.quadrantRsa[1]:0.000}";
    }

    public static string PhaseLabel(int phase, bool rear)
    {
        if (phase == 3) return rear ? "Phase 3 Rear" : "Phase 3 Front";
        if (phase == 4) return "Phase 4";
        return $"Phase {phase}";
    }
}
