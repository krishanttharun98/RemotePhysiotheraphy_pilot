import { LogOut, RefreshCw, Upload } from 'lucide-react'
import { useEffect, useMemo, useRef, useState } from 'react'
import { fetchCloudSessions } from '../api/cloudSave.js'
import { saveImportedDump } from '../lib/importedDump.js'
import { PHASE_META, phaseTitle, rsaFromHemisphere } from '../lib/rwsGeometry.js'
import { ugsSession } from '../api/ugs.js'
import { directoryFromCloud, parseCloudSaveDump } from '../lib/sessionAdapter.js'
import {
  applyLocalRemovals,
  deletePatientRecord,
  deleteSessionRecord,
} from '../lib/therapistRecords.js'
import { sessionDemographics } from '../lib/phaseMetrics.js'
import ConfirmDialog from './ConfirmDialog.jsx'
import HistoryPanel from './HistoryPanel.jsx'
import PatientDirectory from './PatientDirectory.jsx'
import PatientProfile from './PatientProfile.jsx'
import PhaseAnalytics from './PhaseAnalytics.jsx'
import PhaseCompare from './PhaseCompare.jsx'
import SessionViewport from './SessionViewport.jsx'

export default function Dashboard({ therapistId, onLogout }) {
  const [patients, setPatients] = useState([])
  const [cloudMeta, setCloudMeta] = useState(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(true)
  const [activeUserId, setActiveUserId] = useState(null)
  const [activeStorageKey, setActiveStorageKey] = useState(null)
  const [activeHistoryIndex, setActiveHistoryIndex] = useState(null)
  const [consoleView, setConsoleView] = useState('snapshots')
  const [pendingDelete, setPendingDelete] = useState(null)
  const [version, setVersion] = useState('game')
  const [compareMode, setCompareMode] = useState(false)
  const importRef = useRef(null)

  const patient = useMemo(
    () => patients.find((p) => p.userId === activeUserId) || null,
    [patients, activeUserId],
  )
  const session = useMemo(
    () => patient?.sessions.find((s) => s.storageKey === activeStorageKey) || patient?.sessions[0] || null,
    [patient, activeStorageKey],
  )
  const ver =
    session && version === 'game' && !session.hasGame && session.hasPlain
      ? 'plain'
      : session && version === 'plain' && !session.hasPlain && session.hasGame
        ? 'game'
        : version
  const viewSession = useMemo(
    () => (session ? { ...session, phases: (ver === 'plain' ? session.plainPhases : session.phases) || [] } : null),
    [session, ver],
  )
  const phaseSnap = activeHistoryIndex && viewSession ? viewSession.phases[activeHistoryIndex - 1] : null
  const gamePhase = activeHistoryIndex && session ? session.phases?.[activeHistoryIndex - 1] : null
  const plainPhase = activeHistoryIndex && session ? session.plainPhases?.[activeHistoryIndex - 1] : null
  const canCompare = Boolean(gamePhase && plainPhase)
  const phaseMeta = PHASE_META.find((p) => p.historyIndex === activeHistoryIndex)
  const rsa = phaseSnap ? Math.round(rsaFromHemisphere(phaseSnap.hemisphere) * 100) : session?.score ?? 0
  const demo = sessionDemographics(session)

  function applyPatients(nextPatients, preferredUserId = activeUserId, preferredSessionKey = activeStorageKey) {
    const filtered = applyLocalRemovals(nextPatients)
    setPatients(filtered)
    const keepUser = filtered.some((p) => p.userId === preferredUserId)
    const nextUser = keepUser ? preferredUserId : filtered[0]?.userId || null
    const nextPatient = filtered.find((p) => p.userId === nextUser)
    const keepSession = nextPatient?.sessions.some((s) => s.storageKey === preferredSessionKey)
    setActiveUserId(nextUser)
    setActiveStorageKey(keepSession ? preferredSessionKey : nextPatient?.sessions[0]?.storageKey || null)
    if (!keepSession) setActiveHistoryIndex(null)
    if (!nextUser) setConsoleView('snapshots')
  }

  async function importExportFile(file) {
    if (!file) return
    setBusy(true)
    setError('')
    try {
      const text = await file.text()
      const payload = parseCloudSaveDump(text)
      payload.source = 'import'
      await saveImportedDump(payload)
      const { patients: nextPatients } = directoryFromCloud(payload)
      setCloudMeta({
        playerId: payload.playerId,
        source: payload.source,
        keys: (payload.keys || []).filter((k) => k !== 'rws_session_index'),
      })
      applyPatients(nextPatients)
    } catch (err) {
      setError(err.message)
    } finally {
      setBusy(false)
    }
  }

  async function loadCloud() {
    setBusy(true)
    setError('')
    try {
      const payload = await fetchCloudSessions()
      const { patients: nextPatients } = directoryFromCloud(payload)
      setCloudMeta({
        playerId: payload.playerId,
        source: payload.source,
        keys: (payload.keys || []).filter((k) => k !== 'rws_session_index'),
      })
      applyPatients(nextPatients)
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
    setConsoleView('snapshots')
  }

  function confirmPendingDelete() {
    if (!pendingDelete) return
    if (pendingDelete.type === 'patient') {
      deletePatientRecord(
        pendingDelete.userId,
        (pendingDelete.sessions || []).map((item) => item.storageKey),
      )
      applyPatients(patients, pendingDelete.userId === activeUserId ? null : activeUserId)
    } else if (pendingDelete.type === 'session') {
      deleteSessionRecord(pendingDelete.storageKey)
      applyPatients(patients)
    }
    setPendingDelete(null)
  }

  return (
    <div className="flex h-full min-h-0 flex-col bg-slate-950 text-slate-100">
      <header className="flex items-center justify-between border-b border-slate-800 bg-slate-900 px-5 py-3">
        <div>
          <p className="text-xs uppercase tracking-widest text-cyan-400">MIRA Therapist Console</p>
          <h1 className="text-base font-semibold">
            {consoleView === 'profile' && activeUserId
              ? `${activeUserId} · profile`
              : session
                ? session.userId
                : busy
                  ? 'Loading cloud sessions…'
                  : 'No cloud User ID yet'}
            {consoleView === 'snapshots' && phaseMeta
              ? ` · ${phaseTitle(phaseMeta, ver)}`
              : consoleView === 'snapshots' && session
                ? ' · standing avatar'
                : ''}
          </h1>
          <p className="text-xs text-slate-500">
            {cloudMeta?.source === 'unity-cloud' ? 'Unity Cloud Save (live)' : 'Local export'}
            {' · '}Player {cloudMeta?.playerId || ugsSession()?.userId || '—'}
            {cloudMeta?.keys?.length ? ` · ${cloudMeta.keys.length} sessions` : ''}
            {demo.length ? ` · ${demo.join(' · ')}` : ''}
          </p>
        </div>
        <div className="flex items-center gap-6 text-sm">
          {session ? (
            <div className="flex items-center gap-2">
              <div className="flex rounded-lg border border-slate-700 p-0.5 text-xs">
                {[
                  ['game', 'Game', session.hasGame],
                  ['plain', 'Non-game', session.hasPlain],
                ].map(([key, label, has]) => (
                  <button
                    key={key}
                    type="button"
                    disabled={!has}
                    onClick={() => {
                      setVersion(key)
                      setCompareMode(false)
                    }}
                    className={`rounded-md px-3 py-1 ${
                      ver === key && !compareMode
                        ? 'bg-cyan-500 text-slate-950'
                        : has
                          ? 'text-slate-300 hover:bg-slate-800'
                          : 'text-slate-600'
                    }`}
                  >
                    {label}
                  </button>
                ))}
              </div>
              <button
                type="button"
                disabled={!canCompare}
                onClick={() => setCompareMode((on) => !on)}
                className={`rounded-md border px-3 py-1 text-xs ${
                  compareMode
                    ? 'border-amber-400/60 bg-amber-500/15 text-amber-100'
                    : canCompare
                      ? 'border-slate-700 text-slate-300 hover:bg-slate-800'
                      : 'border-slate-800 text-slate-600'
                }`}
              >
                Compare
              </button>
            </div>
          ) : null}
          <Metric label="Score" value={`${rsa}%`} />
          <Metric label="Saved" value={session?.date || '—'} />
          <Metric label="Therapist" value={therapistId} />
          <input
            ref={importRef}
            type="file"
            accept="application/json,.json"
            className="hidden"
            onChange={(e) => {
              const file = e.target.files?.[0]
              e.target.value = ''
              importExportFile(file)
            }}
          />
          <button
            type="button"
            onClick={() => importRef.current?.click()}
            disabled={busy}
            className="inline-flex items-center gap-2 rounded-lg border border-slate-700 px-3 py-1.5 text-slate-300 hover:bg-slate-800 disabled:opacity-50"
          >
            <Upload className="h-4 w-4" />
            Import
          </button>
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
          onViewProfile={(userId) => {
            setActiveUserId(userId)
            setConsoleView('profile')
          }}
          onDelete={(item) =>
            setPendingDelete({
              type: 'patient',
              userId: item.userId,
              sessions: item.sessions,
            })
          }
        />
        {consoleView === 'profile' && activeUserId ? (
          <PatientProfile
            userId={activeUserId}
            sessionHint={session}
            onBack={() => setConsoleView('snapshots')}
          />
        ) : (
          <>
            <main className="relative flex min-h-0 min-w-0 flex-1 flex-col gap-3 p-4">
              <div className="min-h-0 flex-1">
                <SessionViewport phaseSnap={phaseSnap} />
              </div>
              {activeHistoryIndex && (phaseSnap || (compareMode && canCompare)) ? (
                <div className="max-h-[42%] overflow-y-auto rounded-xl border border-slate-800 bg-slate-950/90 p-4">
                  {compareMode && canCompare ? (
                    <PhaseCompare
                      gamePhase={gamePhase}
                      plainPhase={plainPhase}
                      title={phaseTitle(phaseMeta, 'game')}
                      who={session?.userId}
                      when={session?.savedAtUtc}
                    />
                  ) : (
                    <PhaseAnalytics phase={phaseSnap} title={phaseTitle(phaseMeta, ver)} />
                  )}
                </div>
              ) : null}
              {!busy && !patients.length ? (
                <div className="absolute inset-4 flex items-center justify-center">
                  <div className="max-w-lg rounded-2xl border border-slate-700 bg-slate-950/90 p-6 text-sm text-slate-200">
                    <p className="font-semibold text-slate-50">No User IDs loaded yet</p>
                    <p className="mt-2 text-slate-400">
                      Unity export writes a local file this website cannot see by itself. Click Import and choose
                      <span className="text-slate-200"> rws-cloud-save.json </span>
                      from the project folder
                      <span className="text-slate-200"> dashboard/public</span>.
                    </p>
                    <button
                      type="button"
                      onClick={() => importRef.current?.click()}
                      className="mt-4 inline-flex items-center gap-2 rounded-lg bg-cyan-500 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-cyan-400"
                    >
                      <Upload className="h-4 w-4" />
                      Import rws-cloud-save.json
                    </button>
                  </div>
                </div>
              ) : null}
            </main>
            <HistoryPanel
              patient={patient}
              session={viewSession}
              version={ver}
              activeHistoryIndex={activeHistoryIndex}
              onSelectSession={(key) => {
                setActiveStorageKey(key)
                setActiveHistoryIndex(null)
              }}
              onSelectPhase={(index) => {
                setActiveHistoryIndex(index)
                setCompareMode(false)
              }}
              onViewProfile={(userId) => {
                setActiveUserId(userId)
                setConsoleView('profile')
              }}
              onDeleteSession={(item) =>
                setPendingDelete({
                  type: 'session',
                  storageKey: item.storageKey,
                  label: item.sessionLabel || item.storageKey,
                })
              }
            />
          </>
        )}
      </div>

      {pendingDelete?.type === 'patient' ? (
        <ConfirmDialog
          title="Delete patient data?"
          body={`This removes User ID ${pendingDelete.userId} and all of their sessions from this therapist console, including any profile you filled in. It does not change the headset Cloud Save export.`}
          onCancel={() => setPendingDelete(null)}
          onConfirm={confirmPendingDelete}
        />
      ) : null}
      {pendingDelete?.type === 'session' ? (
        <ConfirmDialog
          title="Delete this session?"
          body={`This removes ${pendingDelete.label} from this therapist console. It does not change the headset Cloud Save export.`}
          onCancel={() => setPendingDelete(null)}
          onConfirm={confirmPendingDelete}
        />
      ) : null}
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

