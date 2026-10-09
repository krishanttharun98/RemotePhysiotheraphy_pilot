import { useEffect, useMemo, useState } from 'react'
import * as THREE from 'three'
import { Html, Line } from '@react-three/drei'
import {
  HEATMAP_STEPS,
  LAT_RES,
  LAT_ROWS,
  LON_SEGMENTS,
  OVERLAY_RADIUS_SCALE,
  QUADRANT_COLORS,
  heatmapWorldPosition,
  hydrateGrid,
  isPopped,
  mapBubbleGridToLocal,
  mapPhasePointFor,
  stationWorldPosition,
  tryGetBlockPopCount,
  getStandardQuadrantIndex,
} from '../lib/rwsGeometry.js'

function buildWorkspaceShell(phaseSnap) {
  const grid = hydrateGrid(phaseSnap.grid)
  if (!grid) return []
  const radii = grid.radii?.length ? grid.radii : [0.5]
  const buckets = [[], [], [], []]

  for (const radius of radii) {
    const r = radius * 0.98
    for (let i = 0; i < LON_SEGMENTS; i++) {
      for (let j = 0; j < LAT_RES; j++) {
        const lonP0 = i / LON_SEGMENTS
        const lonP1 = (i + 1) / LON_SEGMENTS
        const latP0 = j / LAT_RES
        const latP1 = (j + 1) / LAT_RES
        const p00 = mapPhasePointFor(grid.phase, grid.isRear, lonP0, latP0, r)
        const p10 = mapPhasePointFor(grid.phase, grid.isRear, lonP1, latP0, r)
        const p01 = mapPhasePointFor(grid.phase, grid.isRear, lonP0, latP1, r)
        const p11 = mapPhasePointFor(grid.phase, grid.isRear, lonP1, latP1, r)
        const q = getStandardQuadrantIndex((lonP0 + lonP1) * 0.5, (latP0 + latP1) * 0.5)
        buckets[q].push(p00, p01, p10, p10, p01, p11)
      }
    }
  }

  return buckets.map((verts) => {
    if (!verts.length) return null
    const geo = new THREE.BufferGeometry()
    geo.setAttribute('position', new THREE.Float32BufferAttribute(verts.flat(), 3))
    geo.computeVertexNormals()
    return geo
  })
}

function buildHeatmapGeometries(phaseSnap) {
  const grid = hydrateGrid(phaseSnap.grid)
  if (!grid) return []
  const buckets = { 1: [], 2: [], 3: [], 4: [] }

  const addPatch = (p00, p10, p01, p11, popCount) => {
    if (popCount <= 0 || !buckets[popCount]) return
    buckets[popCount].push(p00, p01, p10, p10, p01, p11)
  }

  if (grid.isPhase4) {
    for (let br = 0; br < grid.ringCount - 1; br++) {
      for (let bl = 0; bl < grid.lonCount - 1; bl++) {
        const popCount = tryGetBlockPopCount(grid, br, bl)
        if (popCount == null || popCount <= 0) continue
        const r00 = grid.radii[br] * OVERLAY_RADIUS_SCALE
        const r01 = grid.radii[br + 1] * OVERLAY_RADIUS_SCALE
        addPatch(
          mapBubbleGridToLocal(grid.phase, grid.isRear, bl, 0, r00, br),
          mapBubbleGridToLocal(grid.phase, grid.isRear, bl + 1, 0, r00, br),
          mapBubbleGridToLocal(grid.phase, grid.isRear, bl, 0, r01, br + 1),
          mapBubbleGridToLocal(grid.phase, grid.isRear, bl + 1, 0, r01, br + 1),
          popCount,
        )
      }
    }
  } else {
    const r = (grid.radii[0] || 0.5) * OVERLAY_RADIUS_SCALE
    for (let bl = 0; bl < grid.lonCount - 1; bl++) {
      for (let bt = 0; bt < LAT_ROWS - 1; bt++) {
        const popCount = tryGetBlockPopCount(grid, bl, bt)
        if (popCount == null || popCount <= 0) continue
        addPatch(
          mapBubbleGridToLocal(grid.phase, grid.isRear, bl, bt, r),
          mapBubbleGridToLocal(grid.phase, grid.isRear, bl + 1, bt, r),
          mapBubbleGridToLocal(grid.phase, grid.isRear, bl, bt + 1, r),
          mapBubbleGridToLocal(grid.phase, grid.isRear, bl + 1, bt + 1, r),
          popCount,
        )
      }
    }
  }

  return HEATMAP_STEPS.map((step) => {
    const verts = buckets[step.pops]
    if (!verts.length) return null
    const geo = new THREE.BufferGeometry()
    geo.setAttribute('position', new THREE.Float32BufferAttribute(verts.flat(), 3))
    geo.computeVertexNormals()
    return {
      geo,
      color: new THREE.Color(step.color[0], step.color[1], step.color[2]),
      opacity: step.color[3],
    }
  }).filter(Boolean)
}

function poppedBubblesFromGrid(phaseSnap) {
  const grid = hydrateGrid(phaseSnap?.grid)
  if (!grid) return []
  const bubbles = []

  if (grid.isPhase4) {
    for (let ring = 0; ring < grid.ringCount; ring++) {
      for (let lon = 0; lon < grid.lonCount; lon++) {
        if (!isPopped(grid, lon, 0, ring)) continue
        const radius = grid.radii[ring] || 0.3
        const pos = mapBubbleGridToLocal(grid.phase, grid.isRear, lon, 0, radius, ring)
        bubbles.push({
          position: pos,
          quadrant: getStandardQuadrantIndex(lon / grid.lonCount, 0.5),
          scale: 0.045,
          lon,
          lat: 0,
        })
      }
    }
    return bubbles
  }

  for (let lon = 0; lon < grid.lonCount; lon++) {
    for (let lat = 0; lat < LAT_ROWS; lat++) {
      if (!isPopped(grid, lon, lat, -1)) continue
      const radius = grid.radii[0] || 0.5
      const pos = mapBubbleGridToLocal(grid.phase, grid.isRear, lon, lat, radius)
      bubbles.push({
        position: pos,
        quadrant: getStandardQuadrantIndex(lon / (grid.lonSegments || LON_SEGMENTS), lat / 12),
        scale: 0.045,
        lon,
        lat,
      })
    }
  }
  return bubbles
}

// Reach path colours match ReachTraceVisualizer.cs: blue out, orange back, red wobble slashes.
const OUTBOUND = '#1e88f5'
const RETURN = '#fa850d'
const WOBBLE = '#f22633'
const WOBBLE_MIN = 0.007
const WOBBLE_WINDOW = 4
const SLASH_LEN = 0.032
const MAX_SPEED = 3

function tracePoint(t, i) {
  return [t.px[i], t.py[i], t.pz[i]]
}

function dist(a, b) {
  return Math.hypot(a[0] - b[0], a[1] - b[1], a[2] - b[2])
}

function splitIndex(t) {
  const n = t.px.length
  if (Number.isInteger(t.splitIndex) && t.splitIndex >= 1 && t.splitIndex <= n - 1) return t.splitIndex
  if (Number.isInteger(t.acquireIndex) && t.acquireIndex >= 1 && t.acquireIndex <= n - 1) return t.acquireIndex
  const a = tracePoint(t, 0)
  let best = Math.max(1, Math.floor(n / 2))
  let bestD = -1
  for (let i = 1; i < n; i++) {
    const d = dist(a, tracePoint(t, i))
    if (d > bestD) {
      bestD = d
      best = i
    }
  }
  return Math.min(Math.max(best, 1), n - 1)
}

function smoothPts(t) {
  const n = t.px.length
  const o = []
  for (let i = 0; i < n; i++) {
    if (i === 0 || i === n - 1) {
      o.push(tracePoint(t, i))
      continue
    }
    const a = tracePoint(t, i - 1)
    const b = tracePoint(t, i)
    const c = tracePoint(t, i + 1)
    o.push([(a[0] + 2 * b[0] + c[0]) * 0.25, (a[1] + 2 * b[1] + c[1]) * 0.25, (a[2] + 2 * b[2] + c[2]) * 0.25])
  }
  return o
}

function slashAt(pts, i, side) {
  const a = Math.max(0, i - 1)
  const b = Math.min(pts.length - 1, i + 1)
  const tangent = [pts[b][0] - pts[a][0], pts[b][1] - pts[a][1], pts[b][2] - pts[a][2]]
  const tlen = Math.hypot(...tangent)
  const tn = tlen > 1e-5 ? tangent.map((v) => v / tlen) : [1, 0, 0]
  const along = side[0] * tn[0] + side[1] * tn[1] + side[2] * tn[2]
  let across = [side[0] - tn[0] * along, side[1] - tn[1] * along, side[2] - tn[2] * along]
  if (Math.hypot(...across) < 1e-4) across = [tn[1], -tn[0], 0]
  if (Math.hypot(...across) < 1e-4) across = [1, 0, 0]
  const alen = Math.hypot(...across)
  across = across.map((v) => v / alen)
  const dir = [across[0] + tn[0] * 0.6, across[1] + tn[1] * 0.6, across[2] + tn[2] * 0.6]
  const dlen = Math.hypot(...dir) || 1
  const n = dir.map((v) => v / dlen)
  const p = pts[i]
  return [
    [p[0] - n[0] * SLASH_LEN * 0.5, p[1] - n[1] * SLASH_LEN * 0.5, p[2] - n[2] * SLASH_LEN * 0.5],
    [p[0] + n[0] * SLASH_LEN * 0.5, p[1] + n[1] * SLASH_LEN * 0.5, p[2] + n[2] * SLASH_LEN * 0.5],
  ]
}

function wobbleSlashes(t, pts) {
  const n = t.px.length
  if (n < WOBBLE_WINDOW * 2 + 3) return []
  const slashes = []
  let best = -1
  let bestDev = 0
  let bestSide = [0, 0, 0]
  for (let i = WOBBLE_WINDOW; i < n - WOBBLE_WINDOW; i++) {
    let avg = [0, 0, 0]
    for (let k = -WOBBLE_WINDOW; k <= WOBBLE_WINDOW; k++) {
      const p = tracePoint(t, i + k)
      avg = [avg[0] + p[0], avg[1] + p[1], avg[2] + p[2]]
    }
    const count = WOBBLE_WINDOW * 2 + 1
    avg = avg.map((v) => v / count)
    const far = tracePoint(t, i + WOBBLE_WINDOW)
    const near = tracePoint(t, i - WOBBLE_WINDOW)
    const tangent = [far[0] - near[0], far[1] - near[1], far[2] - near[2]]
    const tlen = Math.hypot(...tangent)
    if (tlen < 1e-4) continue
    const tn = tangent.map((v) => v / tlen)
    const raw = tracePoint(t, i)
    const dlt = [raw[0] - avg[0], raw[1] - avg[1], raw[2] - avg[2]]
    const along = dlt[0] * tn[0] + dlt[1] * tn[1] + dlt[2] * tn[2]
    const dev = [dlt[0] - tn[0] * along, dlt[1] - tn[1] * along, dlt[2] - tn[2] * along]
    const d = Math.hypot(...dev)
    if (d >= WOBBLE_MIN) {
      if (d > bestDev) {
        bestDev = d
        best = i
        bestSide = dev.map((v) => v / d)
      }
    } else if (best >= 0) {
      slashes.push(slashAt(pts, best, bestSide))
      best = -1
      bestDev = 0
    }
  }
  if (best >= 0) slashes.push(slashAt(pts, best, bestSide))
  return slashes
}

function legSpeed(t, from, to, recorded) {
  if (recorded > 0 && recorded < MAX_SPEED) return recorded
  let d = 0
  for (let i = from; i < to; i++) d += dist(tracePoint(t, i), tracePoint(t, i + 1))
  const dt = t.t && t.t.length > to ? t.t[to] - t.t[from] : 0
  const speed = dt > 1e-4 ? d / dt : 0
  return speed > 0 && speed < MAX_SPEED ? speed : 0
}

function buildReach(t) {
  if (!t?.px?.length || t.px.length < 2) return null
  const n = t.px.length
  const split = splitIndex(t)
  const pts = smoothPts(t)
  const outboundSpeed = legSpeed(t, 0, split, t.outboundSpeed)
  const backSpeed = legSpeed(t, split, n - 1, t.returnSpeed)
  return {
    outbound: pts.slice(0, split + 1),
    back: pts.slice(split),
    slashes: wobbleSlashes(t, pts),
    outboundSpeed,
    backSpeed,
    outboundMid: pts[Math.floor(split / 2)],
    backMid: pts[Math.floor((split + n - 1) / 2)],
  }
}

function SpeedTag({ position, color, text }) {
  return (
    <Html position={position} center distanceFactor={1.2} style={{ pointerEvents: 'none' }}>
      <div
        style={{
          whiteSpace: 'nowrap',
          font: '600 12px system-ui, sans-serif',
          color,
          background: 'rgba(8,12,20,0.78)',
          border: `1px solid ${color}`,
          borderRadius: 999,
          padding: '2px 8px',
        }}
      >
        {text}
      </div>
    </Html>
  )
}

function ReachPath({ reach }) {
  if (!reach) return null
  return (
    <>
      {reach.outbound.length > 1 ? <Line points={reach.outbound} color={OUTBOUND} lineWidth={2} /> : null}
      {reach.back.length > 1 ? <Line points={reach.back} color={RETURN} lineWidth={2} /> : null}
      {reach.slashes.map((pts, i) => (
        <Line key={`w${i}`} points={pts} color={WOBBLE} lineWidth={2} />
      ))}
      {reach.outboundSpeed > 0 ? (
        <SpeedTag
          position={[reach.outboundMid[0], reach.outboundMid[1] + 0.03, reach.outboundMid[2]]}
          color={OUTBOUND}
          text={`out ${reach.outboundSpeed.toFixed(2)} m/s`}
        />
      ) : null}
      {reach.backSpeed > 0 ? (
        <SpeedTag
          position={[reach.backMid[0], reach.backMid[1] - 0.03, reach.backMid[2]]}
          color={RETURN}
          text={`back ${reach.backSpeed.toFixed(2)} m/s`}
        />
      ) : null}
    </>
  )
}

function VoxelBubbles({ phaseSnap }) {
  const [selected, setSelected] = useState(-1)
  const traces = phaseSnap?.traces || []
  const bubbles = useMemo(() => {
    if (phaseSnap?.markers?.length) {
      return phaseSnap.markers.map((m) => ({
        position: [m.px, m.py, m.pz],
        quadrant: m.quadrant,
        scale: m.scale || 0.045,
        lon: m.lon,
        lat: m.lat,
      }))
    }
    return poppedBubblesFromGrid(phaseSnap)
  }, [phaseSnap])

  const reach = useMemo(() => {
    const bubble = bubbles[selected]
    if (!bubble) return null
    // Each item covers a block of grid points (lonSpan x latSpan, default 2x2); any of its markers selects it.
    const covers = (t) =>
      t &&
      bubble.lon >= t.lon &&
      bubble.lon < t.lon + Math.max(1, t.lonSpan || 1) &&
      bubble.lat >= t.lat &&
      bubble.lat < t.lat + Math.max(1, t.latSpan || 1)
    const match = traces.find(covers) || traces[selected]
    return buildReach(match)
  }, [bubbles, selected, traces])

  return (
    <>
      {bubbles.map((b, i) => {
        const color = QUADRANT_COLORS[b.quadrant] || '#22d3ee'
        const active = i === selected
        return (
          <mesh
            key={i}
            position={b.position}
            onClick={(e) => {
              e.stopPropagation()
              setSelected(i)
            }}
          >
            <sphereGeometry args={[Math.max(0.018, b.scale * (active ? 0.6 : 0.45)), 12, 12]} />
            <meshStandardMaterial color={color} emissive={color} emissiveIntensity={active ? 0.9 : 0.45} />
          </mesh>
        )
      })}
      <ReachPath reach={reach} />
    </>
  )
}

export default function PhaseSnapshot({ phaseSnap }) {
  const stationPos = useMemo(() => stationWorldPosition(phaseSnap), [phaseSnap])
  const heatPos = useMemo(() => heatmapWorldPosition(stationPos), [stationPos])
  const shellGeos = useMemo(() => buildWorkspaceShell(phaseSnap), [phaseSnap])
  const heatGeos = useMemo(() => buildHeatmapGeometries(phaseSnap), [phaseSnap])

  useEffect(() => {
    return () => {
      shellGeos.forEach((geo) => geo?.dispose())
      heatGeos.forEach((item) => item.geo.dispose())
    }
  }, [shellGeos, heatGeos])

  return (
    <>
      <group position={stationPos}>
        {shellGeos.map((geo, q) =>
          geo ? (
            <mesh key={`shell-${q}`} geometry={geo}>
              <meshStandardMaterial
                color={QUADRANT_COLORS[q]}
                transparent
                opacity={0.05}
                side={THREE.DoubleSide}
                depthWrite={false}
              />
            </mesh>
          ) : null,
        )}
        <VoxelBubbles phaseSnap={phaseSnap} />
      </group>

      <group position={heatPos}>
        {heatGeos.map((item, i) => (
          <mesh key={`heat-${i}`} geometry={item.geo}>
            <meshBasicMaterial
              color={item.color}
              transparent
              opacity={item.opacity}
              depthWrite={false}
              side={THREE.DoubleSide}
              toneMapped={false}
            />
          </mesh>
        ))}
      </group>
    </>
  )
}
