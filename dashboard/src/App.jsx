import { useState } from 'react'
import Dashboard from './components/Dashboard.jsx'
import LandingPage from './components/LandingPage.jsx'
import LoginScreen from './components/LoginScreen.jsx'
import { ugsSignOut } from './api/ugs.js'

const THERAPIST_KEY = 'mira_therapist_id'

function readTherapist() {
  try {
    return sessionStorage.getItem(THERAPIST_KEY)
  } catch {
    return null
  }
}

function writeTherapist(id) {
  try {
    if (id) sessionStorage.setItem(THERAPIST_KEY, id)
    else sessionStorage.removeItem(THERAPIST_KEY)
  } catch {
    // Private mode: keep the session in memory only.
  }
}

export default function App() {
  const [view, setView] = useState('home')
  const [therapistId, setTherapistId] = useState(() => readTherapist())

  function login(id) {
    writeTherapist(id)
    setTherapistId(id)
  }

  function logout() {
    ugsSignOut()
    writeTherapist(null)
    setTherapistId(null)
    setView('home')
  }

  if (therapistId) {
    return <Dashboard therapistId={therapistId} onLogout={logout} />
  }

  if (view === 'login') {
    return <LoginScreen onLogin={login} onBack={() => setView('home')} />
  }

  return <LandingPage onSignIn={() => setView('login')} />
}
