using UnityEngine;

// Per-phase bubble grid tracked by the voxel observation station.
// Used for quadrant statistics and 2x2 composition heatmap on the hemisphere analytics panel.
public class VoxelPhaseGridData
{
    public const int LatRows = 13; // j = 0..12, matches live spawn loop

    public int phase;
    public bool isRear;
    public bool isPhase4;
    public int lonSegments;
    public int lonCount;   // standard = lonSegments; P4 = lonSegments * 2
    public int ringCount = 1;
    public float[] radii;

    // Standard phases: [lon, lat]. Phase 4: [ring, lon] via helpers below.
    public bool[,] spawned;
    public bool[,] popped;

    public readonly int[] quadrantTotal = new int[4];
    public readonly int[] quadrantPopped = new int[4];

    public static VoxelPhaseGridData CreateForPhase(int phase, bool rear, int lonSegments, float[] radii)
    {
        var data = new VoxelPhaseGridData
        {
            phase = phase,
            isRear = rear,
            isPhase4 = phase == 4,
            lonSegments = lonSegments,
            radii = radii != null ? (float[])radii.Clone() : new float[] { 0.5f }
        };

        if (data.isPhase4)
        {
            data.lonCount = lonSegments * 2;
            data.ringCount = data.radii.Length;
            data.spawned = new bool[data.ringCount, data.lonCount];
            data.popped = new bool[data.ringCount, data.lonCount];
        }
        else
        {
            data.lonCount = lonSegments;
            data.ringCount = 1;
            data.spawned = new bool[data.lonCount, LatRows];
            data.popped = new bool[data.lonCount, LatRows];
        }
        return data;
    }

    public VoxelPhaseGridData Clone()
    {
        var c = new VoxelPhaseGridData
        {
            phase = phase,
            isRear = isRear,
            isPhase4 = isPhase4,
            lonSegments = lonSegments,
            lonCount = lonCount,
            ringCount = ringCount,
            radii = radii != null ? (float[])radii.Clone() : new float[0]
        };
        System.Array.Copy(quadrantTotal, c.quadrantTotal, 4);
        System.Array.Copy(quadrantPopped, c.quadrantPopped, 4);
        if (spawned != null)
        {
            c.spawned = (bool[,])spawned.Clone();
            c.popped = (bool[,])popped.Clone();
        }
        return c;
    }

    public void RegisterSpawn(int lonIdx, int latIdx, int ringIdx, int quadrant)
    {
        if (quadrant < 0 || quadrant > 3) return;
        if (!MarkSpawned(lonIdx, latIdx, ringIdx)) return;
        quadrantTotal[quadrant]++;
    }

    public void RegisterPop(int lonIdx, int latIdx, int ringIdx, int quadrant)
    {
        if (quadrant < 0 || quadrant > 3) return;
        if (!IsSpawned(lonIdx, latIdx, ringIdx)) return;
        if (IsPopped(lonIdx, latIdx, ringIdx)) return;

        SetPopped(lonIdx, latIdx, ringIdx, true);
        quadrantPopped[quadrant]++;
    }

    bool MarkSpawned(int lon, int lat, int ring)
    {
        if (isPhase4)
        {
            if (ring < 0 || ring >= ringCount || lon < 0 || lon >= lonCount) return false;
            if (spawned[ring, lon]) return false;
            spawned[ring, lon] = true;
            return true;
        }
        if (lon < 0 || lon >= lonCount || lat < 0 || lat >= LatRows) return false;
        if (spawned[lon, lat]) return false;
        spawned[lon, lat] = true;
        return true;
    }

    public bool IsSpawned(int lon, int lat, int ring) =>
        isPhase4 ? (ring >= 0 && ring < ringCount && lon >= 0 && lon < lonCount && spawned[ring, lon])
                 : (lon >= 0 && lon < lonCount && lat >= 0 && lat < LatRows && spawned[lon, lat]);

    public bool IsPopped(int lon, int lat, int ring) =>
        isPhase4 ? (ring >= 0 && ring < ringCount && lon >= 0 && lon < lonCount && popped[ring, lon])
                 : (lon >= 0 && lon < lonCount && lat >= 0 && lat < LatRows && popped[lon, lat]);

    void SetPopped(int lon, int lat, int ring, bool value)
    {
        if (isPhase4) popped[ring, lon] = value;
        else popped[lon, lat] = value;
    }

    public float GetQuadrantPopPercent(int quadrant)
    {
        if (quadrant < 0 || quadrant > 3 || quadrantTotal[quadrant] <= 0) return 0f;
        return (float)quadrantPopped[quadrant] / quadrantTotal[quadrant];
    }

    // 2x2 block pop count (0..4). Returns false if fewer than 4 bubbles exist in the block.
    public bool TryGetBlockPopCount(int blockA, int blockB, out int popCount)
    {
        popCount = 0;
        int spawnCount = 0;

        if (isPhase4)
        {
            int br = blockA;
            int bl = blockB;
            if (br < 0 || br >= ringCount - 1 || bl < 0 || bl >= lonCount - 1) return false;
            int[,] offsets = { { 0, 0 }, { 1, 0 }, { 0, 1 }, { 1, 1 } };
            for (int k = 0; k < 4; k++)
            {
                int r = br + offsets[k, 0];
                int l = bl + offsets[k, 1];
                if (!IsSpawned(l, 0, r)) continue;
                spawnCount++;
                if (IsPopped(l, 0, r)) popCount++;
            }
        }
        else
        {
            int bl = blockA;
            int bt = blockB;
            if (bl < 0 || bl >= lonCount - 1 || bt < 0 || bt >= LatRows - 1) return false;
            int[,] offsets = { { 0, 0 }, { 1, 0 }, { 0, 1 }, { 1, 1 } };
            for (int k = 0; k < 4; k++)
            {
                int l = bl + offsets[k, 0];
                int t = bt + offsets[k, 1];
                if (!IsSpawned(l, t, -1)) continue;
                spawnCount++;
                if (IsPopped(l, t, -1)) popCount++;
            }
        }

        return spawnCount == 4;
    }
}
