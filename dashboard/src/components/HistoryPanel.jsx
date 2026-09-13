import { PHASE_META, rsaFromHemisphere } from '../lib/rwsGeometry.js'

export default function HistoryPanel({
  patient,
  session,
  activeHistoryIndex,
  onSelectSession,
  onSelectPhase,
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
      </div>

      {patient.sessions.length ? (
        <div className="border-b border-slate-800 p-3">
          <p className="mb-2 text-xs uppercase tracking-wider text-slate-500">Sessions</p>
          <div className="flex flex-col gap-2">
            {patient.sessions.map((item) => {
              const active = session?.storageKey === item.storageKey
              return (
                <button
                  key={item.storageKey}
                  type="button"
                  onClick={() => onSelectSession(item.storageKey)}
                  className={`rounded-lg border px-3 py-2 text-left text-xs ${
                    active
                      ? 'border-cyan-400/50 bg-cyan-500/10 text-cyan-100'
                      : 'border-slate-800 bg-slate-900 text-slate-300'
                  }`}
                >
                  <span className="block font-medium">{item.sessionLabel || item.userId}</span>
                  <span className="block break-all text-slate-500">{item.storageKey}</span>
                  <span className="text-slate-500">{item.date || item.savedAtUtc || 'unsaved time'}</span>
                </button>
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
                <span className="text-sm font-semibold">{meta.label}</span>
                <span className="rounded-md bg-slate-950 px-2 py-0.5 text-xs text-slate-400">{meta.short}</span>
              </span>
              <span className="mt-2 block text-xs text-slate-400">
                {disabled ? 'No cloud data for this phase' : `RSA ${rsa}% · pops ${popped}/${total}`}
              </span>
            </button>
          )
        })}
      </div>
    </aside>
  )
}
