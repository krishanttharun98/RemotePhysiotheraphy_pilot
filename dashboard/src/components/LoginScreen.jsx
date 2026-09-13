import { Lock, Stethoscope } from 'lucide-react'
import { useState } from 'react'

export default function LoginScreen({ onLogin, onBack }) {
  const [therapistId, setTherapistId] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')

  function submit(event) {
    event.preventDefault()
    if (therapistId === '0000' && password === '0000') {
      onLogin(therapistId)
      return
    }
    setError('Invalid therapist ID or password.')
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
          <span className="mb-1.5 block text-sm text-slate-400">Therapist login ID</span>
          <input
            value={therapistId}
            onChange={(e) => setTherapistId(e.target.value)}
            inputMode="numeric"
            maxLength={4}
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
            maxLength={4}
            placeholder="0000"
            className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2.5 text-slate-100 outline-none ring-cyan-400/40 focus:ring-2"
          />
        </label>

        {error ? <p className="mb-4 text-sm text-rose-400">{error}</p> : null}

        <button
          type="submit"
          className="flex w-full items-center justify-center gap-2 rounded-lg bg-cyan-500 px-4 py-2.5 font-medium text-slate-950 hover:bg-cyan-400"
        >
          <Lock className="h-4 w-4" />
          Sign in
        </button>

        <p className="mt-5 text-center text-xs text-slate-500">Pilot credentials: ID 0000 / password 0000</p>
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
