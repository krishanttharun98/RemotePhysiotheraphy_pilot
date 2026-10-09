import { PHASE_META, SIDE_NAMES, gridReach, rsaFromHemisphere } from '../lib/rwsGeometry.js'
import { sessionDemographics } from '../lib/phaseMetrics.js'
import OverflowMenu from './OverflowMenu.jsx'

export default function HistoryPanel({
  patient,
  session,
  version = 'game',
  activeHistoryIndex,
  onSelectSession,
  onSelectPhase,
  onViewProfile,
  onDeleteSession,
}) {
  if (!patient) {
    return (
      <aside className="flex h-full w-72 shrink-0 flex-col border-l border-slate-800 bg-slate-950 p-4 text-sm text-slate-500">
        Select a cloud User ID to load historical snapshots.
      </aside>
    )
  }

  return (
    <aside className="flex h-full w-80 shrink-0 flex-col border-l border-slate-800 bg-slate-950">
      <div className="border-b border-slate-800 px-4 py-4">
        <p className="text-xs uppercase tracking-widest text-slate-500">Historical data</p>
        <h2 className="mt-1 text-sm font-semibold text-slate-100">{patient.userId}</h2>
        <p className="mt-1 text-xs text-slate-500">
          {patient.sessionCount} cloud session{patient.sessionCount === 1 ? '' : 's'}
        </p>
        {sessionDemographics(session).length ? (
          <p className="mt-2 text-xs text-slate-400">{sessionDemographics(session).join(' · ')}</p>
        ) : null}
      </div>

      {patient.sessions.length ? (
        <div className="border-b border-slate-800 p-3">
          <p className="mb-2 text-xs uppercase tracking-wider text-slate-500">Sessions</p>
          <div className="flex flex-col gap-2">
            {patient.sessions.map((item) => {
              const active = session?.storageKey === item.storageKey
              return (
                <div
                  key={item.storageKey}
                  className={`flex items-start gap-1 rounded-lg border ${
                    active
                      ? 'border-cyan-400/50 bg-cyan-500/10 text-cyan-100'
                      : 'border-slate-800 bg-slate-900 text-slate-300'
                  }`}
                >
                  <button
                    type="button"
                    onClick={() => onSelectSession(item.storageKey)}
                    className="min-w-0 flex-1 px-3 py-2 text-left text-xs"
                  >
                    <span className="block font-medium">{item.sessionLabel || item.userId}</span>
                    <span className="block break-all text-slate-500">{item.storageKey}</span>
                    <span className="text-slate-500">{item.date || item.savedAtUtc || 'unsaved time'}</span>
                    <span className="mt-1 block text-slate-500">
                      {[item.guest ? 'Guest' : null, item.playMode, item.hasGame ? 'Game' : null, item.hasPlain ? 'Non-game' : null]
                        .filter(Boolean)
                        .join(' · ')}
                    </span>
                  </button>
                  <div className="pt-1 pr-1">
                    <OverflowMenu
                      items={[
                        { label: 'View profile', onClick: () => onViewProfile(patient.userId) },
                        { label: 'Delete session', danger: true, onClick: () => onDeleteSession(item) },
                      ]}
                    />
                  </div>
                </div>
              )
            })}
          </div>
        </div>
      ) : null}

      <div className="flex min-h-0 flex-1 flex-col gap-2 overflow-y-auto p-3">
        <p className="text-xs text-slate-500">{session?.storageKey || 'No snapshot loaded'}</p>
        {PHASE_META.map((meta) => {
          const snap = session?.phases?.[meta.historyIndex - 1]
          const rsa = Math.round(rsaFromHemisphere(snap?.hemisphere) * 100)
          const popped = snap?.hemisphere?.quadrantPopped?.reduce((a, b) => a + b, 0) ?? 0
          const total = snap?.hemisphere?.quadrantTotal?.reduce((a, b) => a + b, 0) ?? 0
          const active = activeHistoryIndex === meta.historyIndex
          const disabled = !snap
          return (
            <button
              key={meta.historyIndex}
              type="button"
              disabled={disabled}
              onClick={() => onSelectPhase(meta.historyIndex)}
              className={`rounded-xl border px-3 py-3 text-left ${
                disabled
                  ? 'cursor-not-allowed border-slate-900 bg-slate-950 text-slate-600'
                  : active
                    ? 'border-cyan-400/50 bg-cyan-500/15 text-cyan-100'
                    : 'border-slate-800 bg-slate-900 text-slate-200 hover:border-slate-700'
              }`}
            >
              <span className="flex items-center justify-between">
                <span className="text-sm font-semibold">
                  {meta.label}
                  <span className="block text-xs font-normal text-slate-400">
                    {version === 'plain' ? meta.plain : meta.game}
                  </span>
                </span>
                <span className="rounded-md bg-slate-950 px-2 py-0.5 text-xs text-slate-400">{meta.short}</span>
              </span>
              <span className="mt-2 block text-xs text-slate-400">
                {disabled
                  ? 'No cloud data for this phase'
                  : `RSA ${rsa}% · pops ${popped}/${total}${
                      snap?.metrics?.trialCount
                        ? ` · Fitts ${Number(snap.metrics.fittsVsBaseline || 0).toFixed(2)}x · ${Number(snap.metrics.throughputBitsPerSec || 0).toFixed(2)} bits/s`
                        : ''
                    }${
                      snap?.metrics?.meanSpeedMetresPerSec
                        ? ` · ${Number(snap.metrics.meanSpeedMetresPerSec).toFixed(2)} m/s`
                        : ''
                    }${
                      snap?.metrics?.wobbleRmsMetres
                        ? ` · wobble ${(snap.metrics.wobbleRmsMetres * 1000).toFixed(1)} mm`
                        : ''
                    }${
                      snap?.metrics?.elapsedSeconds
                        ? ` · ${formatPhaseTime(snap.metrics.elapsedSeconds)}`
                        : ''
                    }`}
              </span>
              {snap ? <ArmLine snap={snap} /> : null}
            </button>
          )
        })}
      </div>
    </aside>
  )
}

function formatPhaseTime(seconds) {
  const total = Math.max(0, Math.round(Number(seconds) || 0))
  const m = String(Math.floor(total / 60)).padStart(2, '0')
  const s = String(total % 60).padStart(2, '0')
  return `${m}:${s}`
}

function ArmLine({ snap }) {
  const side = SIDE_NAMES[snap.handSide] || snap.metrics?.exerciseHand || ''
  const main = Math.round(gridReach(snap.grid) * 100)
  const other = snap.otherArm
  if (!side && !other) return null
  const otherSide = SIDE_NAMES[other?.handSide] || ''
  const otherReach = other ? Math.round(gridReach(other.grid) * 100) : null
  return (
    <span className="mt-1.5 block text-xs">
      <span className="text-rose-300">Affected{side ? ` (${side})` : ''} {main}%</span>
      {other ? (
        <span className="text-emerald-300">
          {' · '}Unaffected{otherSide ? ` (${otherSide})` : ''} {otherReach}%
        </span>
      ) : null}
    </span>
  )
}
