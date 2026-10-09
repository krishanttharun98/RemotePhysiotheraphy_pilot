import { Loader2, Lock, Stethoscope } from 'lucide-react'
import { useState } from 'react'

const THERAPIST_ID = '0000'
const THERAPIST_PASSWORD = '0000'

export default function LoginScreen({ onLogin, onBack }) {
  const [therapistId, setTherapistId] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(event) {
    event.preventDefault()
    if (therapistId.trim() !== THERAPIST_ID || password !== THERAPIST_PASSWORD) {
      setError('Wrong therapist ID or password.')
      return
    }
    setBusy(true)
    setError('')
    try {
      await onLogin(THERAPIST_ID)
    } catch (err) {
      setError(err.message || 'Sign-in failed.')
      setBusy(false)
    }
  }

  return (
    <div className="flex min-h-full items-center justify-center bg-slate-950 px-6">
      <form
        onSubmit={submit}
        className="w-full max-w-md rounded-2xl border border-slate-800 bg-slate-900 p-8 shadow-2xl"
      >
        <div className="mb-8 flex items-center gap-3">
          <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-cyan-500/15 text-cyan-300">
            <Stethoscope className="h-6 w-6" />
          </div>
          <div>
            <p className="text-xs uppercase tracking-widest text-cyan-400">MIRA</p>
            <h1 className="text-xl font-semibold text-slate-50">Therapist Console</h1>
          </div>
        </div>

        <label className="mb-4 block">
          <span className="mb-1.5 block text-sm text-slate-400">Therapist ID</span>
          <input
            value={therapistId}
            onChange={(e) => setTherapistId(e.target.value)}
            autoComplete="username"
            autoCapitalize="none"
            placeholder="0000"
            className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2.5 text-slate-100 outline-none ring-cyan-400/40 focus:ring-2"
          />
        </label>

        <label className="mb-6 block">
          <span className="mb-1.5 block text-sm text-slate-400">Password</span>
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
            placeholder="0000"
            className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2.5 text-slate-100 outline-none ring-cyan-400/40 focus:ring-2"
          />
        </label>

        {error ? <p className="mb-4 text-sm text-rose-400">{error}</p> : null}

        <button
          type="submit"
          disabled={busy}
          className="flex w-full items-center justify-center gap-2 rounded-lg bg-cyan-500 px-4 py-2.5 font-medium text-slate-950 hover:bg-cyan-400 disabled:opacity-60"
        >
          {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Lock className="h-4 w-4" />}
          {busy ? 'Signing in…' : 'Sign in'}
        </button>

        <p className="mt-5 text-center text-xs text-slate-500">Therapist ID 0000 · password 0000</p>
        {onBack ? (
          <button
            type="button"
            onClick={onBack}
            className="mt-4 w-full text-center text-sm text-slate-400 hover:text-slate-200"
          >
            Back to MIRA
          </button>
        ) : null}
      </form>
    </div>
  )
}
