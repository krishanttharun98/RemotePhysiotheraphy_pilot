import { Search, UserRound } from 'lucide-react'
import { useMemo, useState } from 'react'

export default function PatientDirectory({ patients, activeUserId, onSelect }) {
  const [query, setQuery] = useState('')
  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase()
    if (!q) return patients
    return patients.filter(
      (p) => p.userId.toLowerCase().includes(q) || p.displayName.toLowerCase().includes(q),
    )
  }, [patients, query])

  return (
    <aside className="flex h-full min-h-0 w-72 shrink-0 flex-col border-r border-slate-800 bg-slate-950">
      <div className="border-b border-slate-800 px-4 py-4">
        <p className="text-xs uppercase tracking-widest text-slate-500">Patient directory</p>
        <h2 className="mt-1 text-sm font-semibold text-slate-100">Cloud User IDs</h2>
        <div className="relative mt-3">
          <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-slate-500" />
          <input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Search ID"
            className="w-full rounded-lg border border-slate-800 bg-slate-900 py-2 pl-9 pr-3 text-sm text-slate-100 outline-none focus:border-cyan-500"
          />
        </div>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto p-2">
        {!filtered.length ? (
          <p className="px-3 py-6 text-sm text-slate-500">
            Empty until you export Cloud Save from Unity (XRHands menu), then click Reload.
          </p>
        ) : null}
        {filtered.map((patient) => {
          const active = patient.userId === activeUserId
          return (
            <button
              key={patient.userId}
              type="button"
              onClick={() => onSelect(patient.userId)}
              className={`mb-1 flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-left ${
                active
                  ? 'bg-cyan-500/15 text-cyan-200 ring-1 ring-cyan-400/40'
                  : 'text-slate-300 hover:bg-slate-900'
              }`}
            >
              <span className={`flex h-8 w-8 items-center justify-center rounded-full ${active ? 'bg-cyan-500/20' : 'bg-slate-800'}`}>
                <UserRound className="h-4 w-4" />
              </span>
              <span className="min-w-0">
                <span className="block truncate font-medium">{patient.userId}</span>
        <span className="block truncate text-xs text-slate-500">
                  {patient.sessionCount} {patient.sessionCount === 1 ? 'session' : 'sessions'}
                </span>
              </span>
            </button>
          )
        })}
      </div>
    </aside>
  )
}
