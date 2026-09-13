import { hydrateGrid, mapBubbleGridToLocal, getStandardQuadrantIndex, rsaFromHemisphere } from './rwsGeometry.js'

export function unwrapJson(value) {
  if (value == null) return null
  if (typeof value === 'string') {
    try {
      return unwrapJson(JSON.parse(value))
    } catch {
      return null
    }
  }
  if (typeof value === 'object' && !Array.isArray(value)) {
    const keys = Object.keys(value)
    if (keys.length === 1 && keys[0] === 'value') return unwrapJson(value.value)
  }
  return value
}

function hasPhaseBody(phase) {
  if (!phase || typeof phase !== 'object') return false
  return Boolean(
    phase.grid ||
      phase.hemisphere ||
      (Array.isArray(phase.markers) && phase.markers.length) ||
      (Array.isArray(phase.shells) && phase.shells.length),
  )
}

function markersFromGrid(phaseSnap) {
  const grid = hydrateGrid(phaseSnap?.grid)
  if (!grid || !grid.dim0 || !grid.dim1) return phaseSnap?.markers || []
  if (phaseSnap?.markers?.length) return phaseSnap.markers

  const markers = []
  if (grid.isPhase4) {
    for (let ring = 0; ring < grid.ringCount; ring++) {
      for (let lon = 0; lon < grid.lonCount; lon++) {
        if (!grid.popped[ring]?.[lon]) continue
        const radius = grid.radii[ring] || 0.3
        const pos = mapBubbleGridToLocal(grid.phase, grid.isRear, lon, 0, radius, ring)
        const quadrant = getStandardQuadrantIndex(lon / grid.lonCount, 0.5)
        markers.push({ px: pos[0], py: pos[1], pz: pos[2], quadrant, scale: 0.045 })
      }
    }
  } else {
    for (let lon = 0; lon < grid.lonCount; lon++) {
      for (let lat = 0; lat < 13; lat++) {
        if (!grid.popped[lon]?.[lat]) continue
        const radius = grid.radii[0] || 0.5
        const pos = mapBubbleGridToLocal(grid.phase, grid.isRear, lon, lat, radius)
        const quadrant = getStandardQuadrantIndex(lon / (grid.lonSegments || 24), lat / 12)
        markers.push({ px: pos[0], py: pos[1], pz: pos[2], quadrant, scale: 0.045 })
      }
    }
  }
  return markers
}

export function adaptSnapshot(raw, storageKey = '') {
  const snap = unwrapJson(raw) || raw
  if (!snap || typeof snap !== 'object') return null

  const phasesIn = Array.isArray(snap.phases) ? snap.phases : []
  const phases = [null, null, null, null, null]
  phasesIn.forEach((phase, i) => {
    if (!hasPhaseBody(phase)) return
    const slot = phase.historyIndex >= 1 && phase.historyIndex <= 5 ? phase.historyIndex - 1 : i
    if (slot < 0 || slot > 4) return
    phases[slot] = {
      ...phase,
      historyIndex: slot + 1,
      markers: markersFromGrid(phase),
    }
  })

  const scorePhase = phases.find((p) => p?.hemisphere) || phases[1] || phases[0]
  const saved = snap.savedAtUtc || ''
  return {
    userId: snap.userId || '',
    sessionLabel: snap.sessionLabel || storageKey.replace(/^rws_session_/, ''),
    savedAtUtc: saved,
    date: saved ? saved.slice(0, 10) : '',
    elapsedTimeSeconds: snap.elapsedTimeSeconds || 0,
    storageKey,
    phases,
    score: Math.round(rsaFromHemisphere(scorePhase?.hemisphere) * 100),
  }
}

export function directoryFromCloud(payload) {
  const sessionsByKey = {}
  const index = unwrapJson(payload.index) || payload.index || { entries: [] }
  const rawSessions = payload.sessions || {}

  for (const [key, value] of Object.entries(rawSessions)) {
    if (key === 'rws_session_index') continue
    if (!key.startsWith('rws_session_')) continue
    const adapted = adaptSnapshot(value, key)
    if (adapted) sessionsByKey[key] = adapted
  }

  const entries = Array.isArray(index.entries) ? index.entries.filter(Boolean) : []
  for (const entry of entries) {
    if (!entry.storageKey || sessionsByKey[entry.storageKey]) continue
    const adapted = adaptSnapshot(rawSessions[entry.storageKey], entry.storageKey)
    if (adapted) {
      adapted.userId = adapted.userId || entry.userId
      adapted.sessionLabel = adapted.sessionLabel || entry.sessionLabel
      adapted.savedAtUtc = adapted.savedAtUtc || entry.savedAtUtc
      sessionsByKey[entry.storageKey] = adapted
    }
  }

  const byUser = new Map()
  const add = (userId, session) => {
    const id = userId || session?.sessionLabel || payload.playerId
    if (!id || !session) return
    session.userId = session.userId || id
    if (!byUser.has(id)) byUser.set(id, [])
    byUser.get(id).push(session)
  }

  for (const session of Object.values(sessionsByKey)) add(session.userId, session)
  for (const entry of entries) {
    if (!sessionsByKey[entry.storageKey]) {
      add(entry.userId, {
        userId: entry.userId,
        sessionLabel: entry.sessionLabel,
        storageKey: entry.storageKey,
        savedAtUtc: entry.savedAtUtc,
        date: (entry.savedAtUtc || '').slice(0, 10),
        elapsedTimeSeconds: 0,
        phases: [null, null, null, null, null],
        score: 0,
        missingBody: true,
      })
    }
  }

  const patients = [...byUser.entries()]
    .map(([userId, sessions]) => {
      sessions.sort((a, b) => String(b.savedAtUtc).localeCompare(String(a.savedAtUtc)))
      return {
        userId,
        displayName: `Patient ${userId}`,
        sessionCount: sessions.length,
        lastSessionUtc: sessions[0]?.savedAtUtc || '',
        sessions,
      }
    })
    .sort((a, b) => String(b.lastSessionUtc).localeCompare(String(a.lastSessionUtc)))

  return { patients, sessionsByKey }
}

export function parseCloudSaveDump(input, playerId = '') {
  const parsed = typeof input === 'string' ? JSON.parse(input) : input
  if (!parsed || typeof parsed !== 'object') throw new Error('Cloud Save dump was empty.')

  const sessions = {}
  const ingest = (key, value) => {
    if (!key || typeof key !== 'string') return
    if (key !== 'rws_session_index' && !key.startsWith('rws_session_')) return
    sessions[key] = unwrapJson(value) ?? value
  }

  if (Array.isArray(parsed.results)) {
    parsed.results.forEach((item) => ingest(item.key, item.value))
  }
  if (parsed.sessions && typeof parsed.sessions === 'object' && !Array.isArray(parsed.sessions)) {
    Object.entries(parsed.sessions).forEach(([key, value]) => ingest(key, value))
  }
  Object.entries(parsed).forEach(([key, value]) => ingest(key, value))

  if (!Object.keys(sessions).length && (parsed.userId || parsed.sessionLabel || parsed.phases)) {
    const label = parsed.sessionLabel || parsed.userId || 'imported'
    ingest(`rws_session_${label}`, parsed)
  }

  if (!Object.keys(sessions).length) {
    throw new Error('No rws_session_index or rws_session_* keys found in the pasted JSON.')
  }

  return {
    playerId: parsed.playerId || playerId,
    projectId: parsed.projectId || '',
    environmentId: parsed.environmentId || '',
    source: 'import',
    keys: Object.keys(sessions),
    index: sessions.rws_session_index || parsed.index || { entries: [] },
    sessions,
  }
}
