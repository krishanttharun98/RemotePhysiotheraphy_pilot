import { LogOut, RefreshCw } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { fetchCloudSessions } from '../api/cloudSave.js'
import { PHASE_META, rsaFromHemisphere } from '../lib/rwsGeometry.js'
import { directoryFromCloud } from '../lib/sessionAdapter.js'
import HistoryPanel from './HistoryPanel.jsx'
import PatientDirectory from './PatientDirectory.jsx'
import SessionViewport from './SessionViewport.jsx'

export default function Dashboard({ therapistId, onLogout }) {
  const [patients, setPatients] = useState([])
  const [cloudMeta, setCloudMeta] = useState(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(true)
  const [activeUserId, setActiveUserId] = useState(null)
  const [activeStorageKey, setActiveStorageKey] = useState(null)
  const [activeHistoryIndex, setActiveHistoryIndex] = useState(null)

  const patient = useMemo(
    () => patients.find((p) => p.userId === activeUserId) || null,
    [patients, activeUserId],
  )
  const session = useMemo(
    () => patient?.sessions.find((s) => s.storageKey === activeStorageKey) || patient?.sessions[0] || null,
    [patient, activeStorageKey],
  )
  const phaseSnap = activeHistoryIndex && session ? session.phases[activeHistoryIndex - 1] : null
  const phaseMeta = PHASE_META.find((p) => p.historyIndex === activeHistoryIndex)
  const rsa = phaseSnap ? Math.round(rsaFromHemisphere(phaseSnap.hemisphere) * 100) : session?.score ?? 0

  async function loadCloud() {
    setBusy(true)
    setError('')
    try {
      const payload = await fetchCloudSessions()
      const { patients: nextPatients } = directoryFromCloud(payload)
      setCloudMeta({
        playerId: payload.playerId,
        keys: payload.keys || [],
      })
      setPatients(nextPatients)
      const keepUser = nextPatients.some((p) => p.userId === activeUserId)
      const nextUser = keepUser ? activeUserId : nextPatients[0]?.userId || null
      const nextPatient = nextPatients.find((p) => p.userId === nextUser)
      const keepSession = nextPatient?.sessions.some((s) => s.storageKey === activeStorageKey)
      setActiveUserId(nextUser)
      setActiveStorageKey(keepSession ? activeStorageKey : nextPatient?.sessions[0]?.storageKey || null)
      if (!keepSession) setActiveHistoryIndex(null)
    } catch (err) {
      setError(err.message)
    } finally {
      setBusy(false)
    }
  }

  useEffect(() => {
    loadCloud()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  function selectPatient(userId) {
    const next = patients.find((p) => p.userId === userId)
    setActiveUserId(userId)
    setActiveStorageKey(next?.sessions[0]?.storageKey || null)
    setActiveHistoryIndex(null)
  }

  return (
    <div className="flex h-full min-h-0 flex-col bg-slate-950 text-slate-100">
      <header className="flex items-center justify-between border-b border-slate-800 bg-slate-900 px-5 py-3">
        <div>
          <p className="text-xs uppercase tracking-widest text-cyan-400">MIRA Therapist Console</p>
          <h1 className="text-base font-semibold">
            {session ? session.userId : busy ? 'Loading cloud sessions…' : 'No cloud User ID yet'}
            {phaseMeta ? ` · ${phaseMeta.label}` : session ? ' · standing avatar' : ''}
          </h1>
          <p className="text-xs text-slate-500">
            Player {cloudMeta?.playerId || '7EOyuU9AXzVZDR3vtQcDANgahjFD'}
            {cloudMeta?.keys?.length ? ` · ${cloudMeta.keys.length} sessions` : ''}
          </p>
        </div>
        <div className="flex items-center gap-6 text-sm">
          <Metric label="Score" value={`${rsa}%`} />
          <Metric label="Saved" value={session?.date || '—'} />
          <Metric label="Therapist" value={therapistId} />
          <button
            type="button"
            onClick={loadCloud}
            disabled={busy}
            className="inline-flex items-center gap-2 rounded-lg border border-slate-700 px-3 py-1.5 text-slate-300 hover:bg-slate-800 disabled:opacity-50"
          >
            <RefreshCw className={`h-4 w-4 ${busy ? 'animate-spin' : ''}`} />
            Reload
          </button>
          <button
            type="button"
            onClick={onLogout}
            className="inline-flex items-center gap-2 rounded-lg border border-slate-700 px-3 py-1.5 text-slate-300 hover:bg-slate-800"
          >
            <LogOut className="h-4 w-4" />
            Sign out
          </button>
        </div>
      </header>

      {error ? (
        <div className="border-b border-amber-900 bg-amber-950/40 px-5 py-2 text-sm text-amber-200">{error}</div>
      ) : null}

      <div className="flex min-h-0 flex-1">
        <PatientDirectory
          patients={patients}
          activeUserId={activeUserId}
          onSelect={selectPatient}
        />
        <main className="relative min-h-0 min-w-0 flex-1 p-4">
          <SessionViewport phaseSnap={phaseSnap} />
          {!busy && !patients.length ? (
            <div className="pointer-events-none absolute inset-4 flex items-center justify-center">
              <div className="max-w-lg rounded-2xl border border-slate-700 bg-slate-950/90 p-6 text-sm text-slate-200">
                <p className="font-semibold text-slate-50">No User IDs loaded yet</p>
                <p className="mt-2 text-slate-400">
                  Cloud Save for this player is private to the headset. Export it from Unity on this laptop:
                </p>
                <ol className="mt-3 list-decimal space-y-1 pl-5 text-slate-300">
                  <li>Open this project in the Unity Editor and stay signed in (same account as Unity Dashboard).</li>
                  <li>Menu: <span className="text-cyan-200">XRHands → Export Cloud Save to Therapist Dashboard</span></li>
                  <li>Come back here and click Reload.</li>
                </ol>
              </div>
            </div>
          ) : null}
        </main>
        <HistoryPanel
          patient={patient}
          session={session}
          activeHistoryIndex={activeHistoryIndex}
          onSelectSession={(key) => {
            setActiveStorageKey(key)
            setActiveHistoryIndex(null)
          }}
          onSelectPhase={setActiveHistoryIndex}
        />
      </div>
    </div>
  )
}

function Metric({ label, value }) {
  return (
    <div className="text-right">
      <p className="text-xs uppercase tracking-wider text-slate-500">{label}</p>
      <p className="font-medium text-slate-100">{value}</p>
    </div>
  )
}
