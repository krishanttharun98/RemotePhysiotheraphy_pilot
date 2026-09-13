/** Unity RWS sphere mapping — matches SolidSphereFollower.cs */

export const LON_SEGMENTS = 24
export const LAT_ROWS = 13
export const LAT_RES = 12
export const PHASE4_RADII = [0.15, 0.3, 0.45, 0.6]
export const OVERLAY_RADIUS_SCALE = 1.015
export const MAX_HEAT = 6
export const PHASE4_Y_OFFSET = -0.5
export const NOSE_FORWARD = 0.15
export const HEATMAP_SIDE_OFFSET = [1.2, 0, 0.15]
export const AVATAR_HEAD = [0, 1.58, 0]

export const PHASE_META = [
  { historyIndex: 1, phase: 1, isRear: false, label: 'Phase 1', short: 'P1' },
  { historyIndex: 2, phase: 2, isRear: false, label: 'Phase 2', short: 'P2' },
  { historyIndex: 3, phase: 3, isRear: false, label: 'Phase 3 Front', short: 'P3F' },
  { historyIndex: 4, phase: 3, isRear: true, label: 'Phase 3 Rear', short: 'P3R' },
  { historyIndex: 5, phase: 4, isRear: false, label: 'Phase 4', short: 'P4' },
]

export const QUADRANT_LABELS = ['Top-Left', 'Bottom-Left', 'Top-Right', 'Bottom-Right']

export const QUADRANT_COLORS = [
  '#22d3ee',
  '#4ade80',
  '#fb923c',
  '#c084fc',
]

export function calculateFullSpherePoint(lonP, latP, r) {
  const hAngle = lonP * Math.PI * 2
  const vAngle = (latP - 0.5) * Math.PI
  return [
    r * Math.cos(vAngle) * Math.sin(hAngle),
    r * Math.sin(vAngle),
    r * Math.cos(vAngle) * Math.cos(hAngle),
  ]
}

export function calculateFrontalPoint(lonP, latP, r) {
  const hAngle = (lonP - 0.5) * Math.PI
  const vAngle = (latP - 0.5) * Math.PI * 0.8
  return [
    r * Math.sin(hAngle),
    r * Math.sin(vAngle),
    r * Math.cos(hAngle) * Math.cos(vAngle),
  ]
}

export function mapPhasePointFor(phase, rear, lonP, latP, r) {
  if (phase === 3) {
    const lonAdj = (rear ? lonP * 0.5 + 0.25 : lonP * 0.5 + 0.75) % 1
    return calculateFullSpherePoint(lonAdj, latP, r)
  }
  if (phase === 4) return calculateFullSpherePoint(lonP, latP, r)
  return calculateFrontalPoint(lonP, latP, r)
}

export function mapBubbleGridToLocal(phase, rear, lonIdx, latIdx, radius, ringIdx = -1) {
  if (phase === 4 && ringIdx >= 0) {
    const lonP = lonIdx / (LON_SEGMENTS * 2)
    return mapPhasePointFor(phase, rear, lonP, 0.5, radius)
  }
  return mapPhasePointFor(phase, rear, lonIdx / LON_SEGMENTS, latIdx / 12, radius)
}

export function getStandardQuadrantIndex(lonP, latP) {
  return latP > 0.5 ? (lonP > 0.5 ? 2 : 0) : lonP > 0.5 ? 3 : 1
}

export function cellLocalPos(flatIndex, radius, longitudeSegments = LON_SEGMENTS, latitudeSegments = LAT_ROWS) {
  const lon = Math.floor(flatIndex / latitudeSegments)
  const lat = flatIndex % latitudeSegments
  const lonP = lon / longitudeSegments
  const latP = lat / (latitudeSegments - 1)
  return calculateFullSpherePoint(lonP, latP, radius)
}

export function flattenBool2D(src, dim0, dim1) {
  const flat = new Array(dim0 * dim1).fill(0)
  for (let a = 0; a < dim0; a++) {
    for (let b = 0; b < dim1; b++) {
      flat[a * dim1 + b] = src[a][b] ? 1 : 0
    }
  }
  return flat
}

export function unflattenBool2D(flat, dim0, dim1) {
  const arr = Array.from({ length: dim0 }, () => Array(dim1).fill(false))
  if (!flat) return arr
  const len = Math.min(flat.length, dim0 * dim1)
  for (let i = 0; i < len; i++) arr[Math.floor(i / dim1)][i % dim1] = flat[i] !== 0
  return arr
}

export function isSpawned(grid, lon, lat, ring) {
  if (grid.isPhase4) {
    return ring >= 0 && ring < grid.ringCount && lon >= 0 && lon < grid.lonCount && grid.spawned[ring][lon]
  }
  return lon >= 0 && lon < grid.lonCount && lat >= 0 && lat < LAT_ROWS && grid.spawned[lon][lat]
}

export function isPopped(grid, lon, lat, ring) {
  if (grid.isPhase4) {
    return ring >= 0 && ring < grid.ringCount && lon >= 0 && lon < grid.lonCount && grid.popped[ring][lon]
  }
  return lon >= 0 && lon < grid.lonCount && lat >= 0 && lat < LAT_ROWS && grid.popped[lon][lat]
}

export function tryGetBlockPopCount(grid, blockA, blockB) {
  let popCount = 0
  let spawnCount = 0
  const offsets = [
    [0, 0],
    [1, 0],
    [0, 1],
    [1, 1],
  ]

  if (grid.isPhase4) {
    const br = blockA
    const bl = blockB
    if (br < 0 || br >= grid.ringCount - 1 || bl < 0 || bl >= grid.lonCount - 1) return null
    for (const [da, db] of offsets) {
      const r = br + da
      const l = bl + db
      if (!isSpawned(grid, l, 0, r)) continue
      spawnCount++
      if (isPopped(grid, l, 0, r)) popCount++
    }
  } else {
    const bl = blockA
    const bt = blockB
    if (bl < 0 || bl >= grid.lonCount - 1 || bt < 0 || bt >= LAT_ROWS - 1) return null
    for (const [da, db] of offsets) {
      const l = bl + da
      const t = bt + db
      if (!isSpawned(grid, l, t, -1)) continue
      spawnCount++
      if (isPopped(grid, l, t, -1)) popCount++
    }
  }

  return spawnCount === 4 ? popCount : null
}

export const HEATMAP_STEPS = [
  { pops: 1, label: '1/4', color: [0.12, 0.82, 1.0, 0.7] },
  { pops: 2, label: '2/4', color: [1.0, 0.86, 0.12, 0.82] },
  { pops: 3, label: '3/4', color: [1.0, 0.38, 0.05, 0.92] },
  { pops: 4, label: '4/4', color: [0.9, 0.06, 0.05, 1] },
]

export function heatmapColor(darkness) {
  const pops = Math.round(Math.min(1, Math.max(0, darkness)) * 4)
  if (pops <= 0) return [0, 0, 0, 0]
  return HEATMAP_STEPS[pops - 1].color
}

export function stationWorldPosition(phaseSnap, head = AVATAR_HEAD) {
  const grid = hydrateGrid(phaseSnap?.grid)
  const historyIndex = phaseSnap?.historyIndex
  const isPhase4 = !!(grid?.isPhase4 || grid?.phase === 4 || historyIndex === 5)
  const isPhase3 = !!(grid?.phase === 3 || historyIndex === 3 || historyIndex === 4)
  return [
    head[0],
    head[1] + (isPhase4 ? PHASE4_Y_OFFSET : 0),
    head[2] + (isPhase3 ? NOSE_FORWARD : 0),
  ]
}

export function heatmapWorldPosition(stationPos) {
  return [
    stationPos[0] + HEATMAP_SIDE_OFFSET[0],
    stationPos[1] + HEATMAP_SIDE_OFFSET[1],
    stationPos[2] + HEATMAP_SIDE_OFFSET[2],
  ]
}

export function hydrateGrid(snap) {
  if (!snap) return null
  const grid = {
    phase: snap.phase,
    isRear: !!snap.isRear,
    isPhase4: !!snap.isPhase4,
    lonSegments: snap.lonSegments || LON_SEGMENTS,
    lonCount: snap.lonCount,
    ringCount: snap.ringCount || 1,
    radii: snap.radii?.length ? snap.radii : [0.5],
    dim0: snap.dim0,
    dim1: snap.dim1,
    quadrantTotal: snap.quadrantTotal || [0, 0, 0, 0],
    quadrantPopped: snap.quadrantPopped || [0, 0, 0, 0],
  }
  grid.spawned = unflattenBool2D(snap.spawnedFlags, snap.dim0, snap.dim1)
  grid.popped = unflattenBool2D(snap.poppedFlags, snap.dim0, snap.dim1)
  return grid
}

export function rsaFromHemisphere(hemi) {
  if (!hemi) return 0
  if (typeof hemi.totalRsa === 'number') return hemi.totalRsa
  const rsa = hemi.quadrantRsa || [0, 0, 0, 0]
  return rsa.reduce((s, v) => s + v, 0)
}
