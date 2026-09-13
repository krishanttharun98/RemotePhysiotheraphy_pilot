import { useEffect, useMemo } from 'react'
import * as THREE from 'three'
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
      })
    }
  }
  return bubbles
}

function VoxelBubbles({ phaseSnap }) {
  const bubbles = useMemo(() => {
    if (phaseSnap?.markers?.length) {
      return phaseSnap.markers.map((m) => ({
        position: [m.px, m.py, m.pz],
        quadrant: m.quadrant,
        scale: m.scale || 0.045,
      }))
    }
    return poppedBubblesFromGrid(phaseSnap)
  }, [phaseSnap])

  return bubbles.map((b, i) => {
    const color = QUADRANT_COLORS[b.quadrant] || '#22d3ee'
    return (
      <mesh key={i} position={b.position}>
        <sphereGeometry args={[Math.max(0.018, b.scale * 0.45), 12, 12]} />
        <meshStandardMaterial color={color} emissive={color} emissiveIntensity={0.45} />
      </mesh>
    )
  })
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
