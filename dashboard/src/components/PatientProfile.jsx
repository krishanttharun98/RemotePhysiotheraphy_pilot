import { ArrowLeft } from 'lucide-react'
import { useEffect, useState } from 'react'
import { emptyProfile, loadProfile, saveProfile } from '../lib/therapistRecords.js'

const inputClass =
  'w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2.5 text-sm text-slate-100 outline-none ring-cyan-400/40 focus:ring-2'

export default function PatientProfile({ userId, sessionHint, onBack }) {
  const [profile, setProfile] = useState(() => emptyProfile(userId))
  const [saved, setSaved] = useState(false)

  useEffect(() => {
    const stored = loadProfile(userId)
    setProfile({
      ...stored,
      age: stored.age || sessionHint?.patientAge || '',
      sex: stored.sex || sessionHint?.patientGender || '',
      diagnosis: stored.diagnosis || sessionHint?.patientCondition || '',
    })
    setSaved(false)
  }, [userId, sessionHint])

  function update(field, value) {
    setProfile((prev) => ({ ...prev, [field]: value }))
    setSaved(false)
  }

  function submit(event) {
    event.preventDefault()
    const next = saveProfile(userId, profile)
    setProfile(next)
    setSaved(true)
  }

  return (
    <div className="min-h-0 flex-1 overflow-y-auto p-6">
      <button
        type="button"
        onClick={onBack}
        className="inline-flex items-center gap-2 text-sm text-slate-400 hover:text-slate-100"
      >
        <ArrowLeft className="h-4 w-4" />
        Back to snapshots
      </button>

      <div className="mx-auto mt-6 max-w-2xl">
        <p className="text-xs uppercase tracking-widest text-cyan-400">Therapist record</p>
        <h2 className="mt-1 text-2xl font-semibold text-slate-50">Patient profile</h2>
        <p className="mt-2 text-sm leading-6 text-slate-400">
          Blank until you fill it. Saved on this computer for User ID {userId}, for this assessment.
        </p>

        <form onSubmit={submit} className="mt-8 space-y-4 rounded-2xl border border-slate-800 bg-slate-900 p-6">
          <label className="block">
            <span className="mb-1.5 block text-sm text-slate-400">User ID</span>
            <input value={userId} readOnly className={`${inputClass} text-slate-400`} />
          </label>

          <label className="block">
            <span className="mb-1.5 block text-sm text-slate-400">Patient name</span>
            <input
              value={profile.patientName}
              onChange={(e) => update('patientName', e.target.value)}
              className={inputClass}
            />
          </label>

          <div className="grid gap-4 sm:grid-cols-2">
            <label className="block">
              <span className="mb-1.5 block text-sm text-slate-400">Age</span>
              <input
                value={profile.age}
                onChange={(e) => update('age', e.target.value)}
                inputMode="numeric"
                className={inputClass}
              />
            </label>
            <label className="block">
              <span className="mb-1.5 block text-sm text-slate-400">Sex</span>
              <select
                value={profile.sex}
                onChange={(e) => update('sex', e.target.value)}
                className={inputClass}
              >
                <option value="" />
                <option value="Female">Female</option>
                <option value="Male">Male</option>
                <option value="Other">Other</option>
                <option value="Prefer not to say">Prefer not to say</option>
              </select>
            </label>
          </div>

          <label className="block">
            <span className="mb-1.5 block text-sm text-slate-400">Diagnosis / condition</span>
            <input
              value={profile.diagnosis}
              onChange={(e) => update('diagnosis', e.target.value)}
              placeholder="e.g. cerebral palsy"
              className={inputClass}
            />
          </label>

          <div className="grid gap-4 sm:grid-cols-2">
            <label className="block">
              <span className="mb-1.5 block text-sm text-slate-400">Affected limb</span>
              <select
                value={profile.affectedLimb}
                onChange={(e) => update('affectedLimb', e.target.value)}
                className={inputClass}
              >
                <option value="" />
                <option value="Left">Left</option>
                <option value="Right">Right</option>
                <option value="Both">Both</option>
              </select>
            </label>
            <label className="block">
              <span className="mb-1.5 block text-sm text-slate-400">Severity of limb movement</span>
              <select
                value={profile.movementSeverity}
                onChange={(e) => update('movementSeverity', e.target.value)}
                className={inputClass}
              >
                <option value="" />
                <option value="Mild">Mild</option>
                <option value="Moderate">Moderate</option>
                <option value="Severe">Severe</option>
                <option value="Profound">Profound</option>
              </select>
            </label>
          </div>

          <label className="block">
            <span className="mb-1.5 block text-sm text-slate-400">Dominant hand</span>
            <select
              value={profile.dominantHand}
              onChange={(e) => update('dominantHand', e.target.value)}
              className={inputClass}
            >
              <option value="" />
              <option value="Left">Left</option>
              <option value="Right">Right</option>
              <option value="Ambidextrous">Ambidextrous</option>
            </select>
          </label>

          <label className="block">
            <span className="mb-1.5 block text-sm text-slate-400">Notes for this assessment</span>
            <textarea
              value={profile.notes}
              onChange={(e) => update('notes', e.target.value)}
              rows={4}
              className={inputClass}
            />
          </label>

          <label className="block">
            <span className="mb-1.5 block text-sm text-slate-400">Therapist name</span>
            <input
              value={profile.therapistName}
              onChange={(e) => update('therapistName', e.target.value)}
              className={inputClass}
            />
          </label>

          <div className="flex items-center justify-between pt-2">
            <p className="text-xs text-slate-500">
              {profile.updatedAt
                ? `Last saved ${new Date(profile.updatedAt).toLocaleString()}`
                : 'Not saved yet'}
            </p>
            <button
              type="submit"
              className="rounded-lg bg-cyan-500 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-cyan-400"
            >
              Save profile
            </button>
          </div>
          {saved ? <p className="text-sm text-cyan-300">Saved for this User ID.</p> : null}
        </form>
      </div>
    </div>
  )
}
