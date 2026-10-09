export function clock(seconds) {
  const total = Math.max(0, Math.round(Number(seconds) || 0))
  return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, '0')}`
}

export function rsaPercent(phase) {
  if (!phase) return null
  if (phase.metrics?.rsaPercent > 0) return phase.metrics.rsaPercent
  if (phase.hemisphere?.totalRsa != null) return phase.hemisphere.totalRsa * 100
  return null
}

export function quadrantPct(phase, q) {
  const tot = phase?.grid?.quadrantTotal?.[q]
  if (!tot) return null
  return (100 * (phase.grid.quadrantPopped[q] || 0)) / tot
}

export function reachedCount(phase) {
  if (!phase?.grid?.quadrantPopped) return null
  return phase.grid.quadrantPopped.reduce((a, b) => a + b, 0)
}

export function pickHand(metrics) {
  const a = metrics?.analytics
  if (!a) return null
  if (metrics.exerciseHand === 'Left') return a.left
  if (metrics.exerciseHand === 'Right') return a.right
  const left = a.left?.reaches || 0
  const right = a.right?.reaches || 0
  return right >= left ? a.right : a.left
}

export function fmt(value, digits = 2, empty = '—') {
  if (value == null || Number.isNaN(Number(value))) return empty
  return Number(value).toFixed(digits)
}

export function fmtPos(value, digits = 2, empty = '—') {
  if (value == null || Number.isNaN(Number(value)) || Number(value) <= 0) return empty
  return Number(value).toFixed(digits)
}

function signedDelta(a, b, digits, unit = '') {
  if (a == null || b == null) return '—'
  const d = b - a
  const sign = d >= 0 ? '+' : ''
  return `${sign}${d.toFixed(digits)}${unit}`
}

export function compareRows(gamePhase, plainPhase) {
  const g = gamePhase?.metrics
  const p = plainPhase?.metrics
  const hg = pickHand(g)
  const hp = pickHand(p)
  const row = (name, a, b, unit, digits) => ({
    name,
    game: a == null ? '—' : `${a.toFixed(digits)}${unit}`,
    plain: b == null ? '—' : `${b.toFixed(digits)}${unit}`,
    delta: signedDelta(a, b, digits, unit),
  })
  const pos = (m, fn) => {
    if (!m) return null
    const v = fn(m)
    return v > 0 ? v : null
  }
  const maybe = (v) => (v != null && v > 0 ? v : null)

  return [
    row('RSA (reachable surface)', rsaPercent(gamePhase), rsaPercent(plainPhase), '%', 0),
    ...['Upper-left', 'Lower-left', 'Upper-right', 'Lower-right'].map((label, q) =>
      row(`${label} reach`, quadrantPct(gamePhase, q), quadrantPct(plainPhase, q), '%', 0),
    ),
    row('Targets reached', reachedCount(gamePhase), reachedCount(plainPhase), '', 0),
    row('Active time', pos(g, (m) => m.elapsedSeconds), pos(p, (m) => m.elapsedSeconds), ' s', 0),
    row('Fitts trials', g?.trialCount ?? null, p?.trialCount ?? null, '', 0),
    row('Throughput', pos(g, (m) => m.throughputBitsPerSec), pos(p, (m) => m.throughputBitsPerSec), ' bits/s', 2),
    row('Mean movement time', pos(g, (m) => m.meanMovementTime), pos(p, (m) => m.meanMovementTime), ' s', 2),
    row('Mean index of difficulty', pos(g, (m) => m.meanFittsId), pos(p, (m) => m.meanFittsId), ' bits', 2),
    row('Mean hand speed', pos(g, (m) => m.meanSpeedMetresPerSec), pos(p, (m) => m.meanSpeedMetresPerSec), ' m/s', 2),
    row(
      'Wobble (steadiness)',
      pos(g, (m) => m.wobbleRmsMetres * 1000),
      pos(p, (m) => m.wobbleRmsMetres * 1000),
      ' mm',
      1,
    ),
    row('Smoothness (SPARC)', hg?.reaches > 0 ? hg.sparc : null, hp?.reaches > 0 ? hp.sparc : null, '', 2),
    row('Max reach', maybe(hg?.reachMax ? hg.reachMax * 100 : null), maybe(hp?.reachMax ? hp.reachMax * 100 : null), ' cm', 0),
  ]
}

export function sessionDemographics(session) {
  if (!session) return []
  const chips = []
  if (session.guest) chips.push('Guest')
  if (session.playMode) chips.push(session.playMode)
  if (session.patientAge) chips.push(`Age ${session.patientAge}`)
  if (session.patientGender) chips.push(session.patientGender)
  if (session.patientCondition) chips.push(session.patientCondition)
  return chips
}
