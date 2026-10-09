import { useState } from 'react'
import Dashboard from './components/Dashboard.jsx'
import LandingPage from './components/LandingPage.jsx'
import LoginScreen from './components/LoginScreen.jsx'
import { ugsSession, ugsSignOut } from './api/ugs.js'

export default function App() {
  const [view, setView] = useState('home')
  const [therapistId, setTherapistId] = useState(() => ugsSession()?.username || null)

  if (therapistId) {
    return (
      <Dashboard
        therapistId={therapistId}
        onLogout={() => {
          ugsSignOut()
          setTherapistId(null)
          setView('home')
        }}
      />
    )
  }

  if (view === 'login') {
    return <LoginScreen onLogin={setTherapistId} onBack={() => setView('home')} />
  }

  return <LandingPage onSignIn={() => setView('login')} />
}
