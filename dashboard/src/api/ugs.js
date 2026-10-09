// Live Unity Gaming Services access straight from the browser.
// Signs in with the same shared Unity username/password the headsets use
// (SharedCloudLogin.cs) and reads that player's Cloud Save items, so the
// hosted site needs no server, no service-account keys and no data dump.

export const UGS_PROJECT_ID = '35a0f424-a2ce-45a4-b2f3-5a9e3b9d734b'
export const UGS_ENVIRONMENT = 'production'

// Same shared Cloud Save account the headsets use (SharedCloudLogin.cs).
const SHARED_USERNAME = 'xrhandstherapist'
const SHARED_PASSWORD = 'XRHands-Pilot0000'

const AUTH_URL = 'https://player-auth.services.api.unity.com/v1/authentication/usernamepassword/sign-in'
const SAVE_BASE = 'https://cloud-save.services.api.unity.com'
const STORE_KEY = 'mira_ugs_session'

function readStored() {
  try {
    const raw = sessionStorage.getItem(STORE_KEY)
    if (!raw) return null
    const s = JSON.parse(raw)
    if (!s?.idToken || !s?.userId || Date.now() > (s.expiresAt || 0)) return null
    return s
  } catch {
    return null
  }
}

function store(s) {
  try {
    if (s) sessionStorage.setItem(STORE_KEY, JSON.stringify(s))
    else sessionStorage.removeItem(STORE_KEY)
  } catch {
    // Private mode: keep the session in memory only.
  }
}

let current = readStored()

export function ugsSession() {
  if (current && Date.now() > current.expiresAt) current = null
  return current
}

export function ugsSignOut() {
  current = null
  store(null)
}

export async function ugsSignInShared() {
  return ugsSession() || ugsSignIn(SHARED_USERNAME, SHARED_PASSWORD)
}

export async function ugsSignIn(username, password) {
  let res
  try {
    res = await fetch(AUTH_URL, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ProjectId: UGS_PROJECT_ID,
        UnityEnvironment: UGS_ENVIRONMENT,
      },
      body: JSON.stringify({ username: username.trim(), password }),
    })
  } catch {
    throw new Error('Could not reach Unity sign-in. Check the internet connection.')
  }
  const body = await res.json().catch(() => ({}))
  if (!res.ok || !body.idToken) {
    const detail = body?.detail || body?.title || `HTTP ${res.status}`
    throw new Error(
      res.status === 400 || res.status === 401 || res.status === 404
        ? 'Wrong Unity username or password.'
        : `Unity sign-in failed: ${detail}`,
    )
  }
  current = {
    idToken: body.idToken,
    userId: body.userId || body.user?.id,
    username: body.user?.username || username.trim(),
    expiresAt: Date.now() + Math.max(60, (body.expiresIn || 3600) - 60) * 1000,
  }
  store(current)
  return current
}

/** All rws_session_* items of the signed-in player, in the same shape as the editor dump. */
export async function fetchUgsSessions() {
  const s = ugsSession()
  if (!s) return null
  const results = []
  let after = ''
  for (let page = 0; page < 50; page++) {
    const url = new URL(`${SAVE_BASE}/v1/data/projects/${UGS_PROJECT_ID}/players/${s.userId}/items`)
    if (after) url.searchParams.set('after', after)
    const res = await fetch(url, { headers: { Authorization: `Bearer ${s.idToken}` } })
    if (res.status === 401) {
      ugsSignOut()
      throw new Error('Unity session expired. Sign in again.')
    }
    if (!res.ok) throw new Error(`Cloud Save read failed (HTTP ${res.status}).`)
    const body = await res.json()
    const items = body.results || []
    results.push(...items)
    const next = body.links?.next
    if (!next || !items.length) break
    after = new URL(next, SAVE_BASE).searchParams.get('after') || items[items.length - 1].key
  }
  return {
    playerId: s.userId,
    projectId: UGS_PROJECT_ID,
    environmentId: UGS_ENVIRONMENT,
    source: 'unity-cloud',
    results: results.filter((r) => r.key === 'rws_session_index' || String(r.key).startsWith('rws_session_')),
  }
}
