import {
  LAT_ROWS,
  LON_SEGMENTS,
  PHASE4_RADII,
  PHASE_META,
  flattenBool2D,
  mapBubbleGridToLocal,
  getStandardQuadrantIndex,
} from '../lib/rwsGeometry.js'

function seeded(seed) {
  let s = seed >>> 0
  return () => {
    s = (s * 1664525 + 1013904223) >>> 0
    return s / 0xffffffff
  }
}

function makeGrid(historyIndex, popBias) {
  const meta = PHASE_META[historyIndex - 1]
  const isPhase4 = meta.phase === 4
  const lonCount = isPhase4 ? LON_SEGMENTS * 2 : LON_SEGMENTS
  const ringCount = isPhase4 ? PHASE4_RADII.length : 1
  const radii = isPhase4 ? [...PHASE4_RADII] : [historyIndex === 1 ? 0.42 : historyIndex === 2 ? 0.55 : 0.5]
  const dim0 = isPhase4 ? ringCount : lonCount
  const dim1 = isPhase4 ? lonCount : LAT_ROWS

  const spawned = Array.from({ length: dim0 }, () => Array(dim1).fill(true))
  const popped = Array.from({ length: dim0 }, () => Array(dim1).fill(false))
  const quadrantTotal = [0, 0, 0, 0]
  const quadrantPopped = [0, 0, 0, 0]
  const rand = seeded(9041 * 17 + historyIndex * 91)
  const markers = []
  const heat = new Array(LON_SEGMENTS * LAT_ROWS).fill(0)

  const visit = (lon, lat, ring) => {
    const lonP = isPhase4 ? lon / lonCount : lon / LON_SEGMENTS
    const latP = isPhase4 ? 0.5 : lat / 12
    const q = getStandardQuadrantIndex(lonP, latP)
    quadrantTotal[q]++
    const chance = popBias[q]
    if (rand() < chance) {
      if (isPhase4) popped[ring][lon] = true
      else popped[lon][lat] = true
      quadrantPopped[q]++
      const radius = isPhase4 ? radii[ring] : radii[0]
      const pos = mapBubbleGridToLocal(meta.phase, meta.isRear, lon, isPhase4 ? 0 : lat, radius, isPhase4 ? ring : -1)
      markers.push({ px: pos[0], py: pos[1], pz: pos[2], quadrant: q, scale: 0.045 })
      const lonBin = Math.min(LON_SEGMENTS - 1, Math.floor(lonP * LON_SEGMENTS))
      const latBin = Math.min(LAT_ROWS - 1, Math.floor(latP * (LAT_ROWS - 1)))
      const idx = lonBin * LAT_ROWS + latBin
      heat[idx] = Math.min(6, heat[idx] + 1.4 + rand() * 2.2)
    }
  }

  if (isPhase4) {
    for (let ring = 0; ring < ringCount; ring++) {
      for (let lon = 0; lon < lonCount; lon++) visit(lon, 0, ring)
    }
  } else {
    for (let lon = 0; lon < lonCount; lon++) {
      for (let lat = 0; lat < LAT_ROWS; lat++) visit(lon, lat, -1)
    }
  }

  const totalBubbles = quadrantTotal.reduce((a, b) => a + b, 0)
  const totalPopped = quadrantPopped.reduce((a, b) => a + b, 0)
  const quadrantRsa = quadrantTotal.map((t, q) => (t > 0 ? (quadrantPopped[q] / t) * 0.25 : 0))
  const totalRsa = quadrantRsa.reduce((a, b) => a + b, 0)

  return {
    historyIndex,
    grid: {
      phase: meta.phase,
      isRear: meta.isRear,
      isPhase4,
      lonSegments: LON_SEGMENTS,
      lonCount,
      ringCount,
      radii,
      quadrantTotal,
      quadrantPopped,
      dim0,
      dim1,
      spawnedFlags: flattenBool2D(spawned, dim0, dim1),
      poppedFlags: flattenBool2D(popped, dim0, dim1),
    },
    hemisphere: {
      historyIndex,
      quadrantTotal,
      quadrantPopped,
      quadrantRsa,
      totalRsa,
      totalPopPercent: totalBubbles > 0 ? totalPopped / totalBubbles : 0,
    },
    markers,
    shells: radii.map((radius) => ({ radius, heat: heat.map((h) => (h > 0 ? h * (0.7 + radius) : 0) ) })),
  }
}

function session(userId, date, biases, elapsed) {
  const phases = PHASE_META.map((meta, i) => makeGrid(meta.historyIndex, biases[i]))
  const last = phases[1]
  return {
    userId,
    sessionLabel: `${userId}_session1`,
    savedAtUtc: `${date}T04:12:00.000Z`,
    date,
    elapsedTimeSeconds: elapsed,
    phases,
    score: Math.round((last.hemisphere.totalRsa || 0) * 100),
  }
}

export const MOCK_PATIENTS = [
  {
    userId: 'PT-9041',
    displayName: 'Patient 9041',
    lastSessionUtc: '2026-07-15T04:12:00.000Z',
    sessionCount: 3,
  },
  {
    userId: 'PT-7720',
    displayName: 'Patient 7720',
    lastSessionUtc: '2026-07-11T09:40:00.000Z',
    sessionCount: 2,
  },
  {
    userId: 'PT-3312',
    displayName: 'Patient 3312',
    lastSessionUtc: '2026-07-08T14:02:00.000Z',
    sessionCount: 4,
  },
  {
    userId: 'PT-1108',
    displayName: 'Patient 1108',
    lastSessionUtc: '2026-06-29T11:18:00.000Z',
    sessionCount: 1,
  },
  {
    userId: 'PT-5564',
    displayName: 'Patient 5564',
    lastSessionUtc: '2026-06-21T16:55:00.000Z',
    sessionCount: 5,
  },
  {
    userId: 'PT-2287',
    displayName: 'Patient 2287',
    lastSessionUtc: '2026-06-18T08:07:00.000Z',
    sessionCount: 2,
  },
  {
    userId: 'PT-4419',
    displayName: 'Patient 4419',
    lastSessionUtc: '2026-06-12T13:33:00.000Z',
    sessionCount: 1,
  },
  {
    userId: 'PT-8093',
    displayName: 'Patient 8093',
    lastSessionUtc: '2026-06-04T10:21:00.000Z',
    sessionCount: 3,
  },
]

export const MOCK_SESSIONS = {
  'PT-9041': session(
    'PT-9041',
    '2026-07-15',
    [
      [0.82, 0.74, 0.88, 0.7],
      [0.86, 0.71, 0.9, 0.68],
      [0.78, 0.62, 0.84, 0.58],
      [0.55, 0.48, 0.6, 0.42],
      [0.7, 0.64, 0.76, 0.61],
    ],
    42,
  ),
  'PT-7720': session(
    'PT-7720',
    '2026-07-11',
    [
      [0.7, 0.66, 0.72, 0.6],
      [0.64, 0.58, 0.7, 0.55],
      [0.6, 0.5, 0.66, 0.48],
      [0.4, 0.36, 0.44, 0.32],
      [0.58, 0.5, 0.62, 0.47],
    ],
    51,
  ),
  'PT-3312': session(
    'PT-3312',
    '2026-07-08',
    [
      [0.9, 0.86, 0.92, 0.84],
      [0.88, 0.82, 0.9, 0.8],
      [0.84, 0.78, 0.86, 0.74],
      [0.72, 0.66, 0.76, 0.62],
      [0.8, 0.76, 0.84, 0.72],
    ],
    38,
  ),
  'PT-1108': session(
    'PT-1108',
    '2026-06-29',
    [
      [0.5, 0.44, 0.56, 0.4],
      [0.48, 0.4, 0.52, 0.36],
      [0.42, 0.34, 0.46, 0.3],
      [0.28, 0.22, 0.32, 0.2],
      [0.4, 0.34, 0.44, 0.3],
    ],
    60,
  ),
  'PT-5564': session(
    'PT-5564',
    '2026-06-21',
    [
      [0.76, 0.7, 0.8, 0.66],
      [0.74, 0.68, 0.78, 0.64],
      [0.7, 0.62, 0.74, 0.58],
      [0.5, 0.44, 0.54, 0.4],
      [0.66, 0.6, 0.7, 0.56],
    ],
    47,
  ),
  'PT-2287': session(
    'PT-2287',
    '2026-06-18',
    [
      [0.62, 0.58, 0.68, 0.52],
      [0.6, 0.54, 0.66, 0.5],
      [0.56, 0.48, 0.6, 0.44],
      [0.38, 0.32, 0.42, 0.28],
      [0.52, 0.46, 0.56, 0.42],
    ],
    55,
  ),
  'PT-4419': session(
    'PT-4419',
    '2026-06-12',
    [
      [0.8, 0.76, 0.84, 0.7],
      [0.78, 0.72, 0.82, 0.68],
      [0.72, 0.64, 0.76, 0.6],
      [0.48, 0.42, 0.52, 0.38],
      [0.64, 0.58, 0.7, 0.54],
    ],
    44,
  ),
  'PT-8093': session(
    'PT-8093',
    '2026-06-04',
    [
      [0.58, 0.52, 0.64, 0.48],
      [0.54, 0.48, 0.6, 0.44],
      [0.5, 0.42, 0.54, 0.38],
      [0.34, 0.28, 0.38, 0.24],
      [0.46, 0.4, 0.5, 0.36],
    ],
    58,
  ),
}

export function loadSessionIndex() {
  return MOCK_PATIENTS
}

export function loadSessionForUser(userId) {
  return MOCK_SESSIONS[userId] || null
}
