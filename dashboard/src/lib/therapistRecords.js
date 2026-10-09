const STORAGE_KEY = 'mira-therapist-records-v1'

function emptyStore() {
  return {
    deletedUserIds: [],
    deletedSessionKeys: [],
    profiles: {},
  }
}

function readStore() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return emptyStore()
    const parsed = JSON.parse(raw)
    return {
      deletedUserIds: Array.isArray(parsed.deletedUserIds) ? parsed.deletedUserIds : [],
      deletedSessionKeys: Array.isArray(parsed.deletedSessionKeys) ? parsed.deletedSessionKeys : [],
      profiles: parsed.profiles && typeof parsed.profiles === 'object' ? parsed.profiles : {},
    }
  } catch {
    return emptyStore()
  }
}

function writeStore(next) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(next))
}

export function emptyProfile(userId = '') {
  return {
    userId,
    patientName: '',
    age: '',
    sex: '',
    diagnosis: '',
    affectedLimb: '',
    movementSeverity: '',
    dominantHand: '',
    notes: '',
    therapistName: '',
    updatedAt: '',
  }
}

export function loadProfile(userId) {
  const store = readStore()
  return { ...emptyProfile(userId), ...(store.profiles[userId] || {}) }
}

export function saveProfile(userId, profile) {
  const store = readStore()
  store.profiles[userId] = {
    ...emptyProfile(userId),
    ...profile,
    userId,
    updatedAt: new Date().toISOString(),
  }
  writeStore(store)
  return store.profiles[userId]
}

export function deletePatientRecord(userId, sessionKeys = []) {
  const store = readStore()
  if (!store.deletedUserIds.includes(userId)) store.deletedUserIds.push(userId)
  for (const key of sessionKeys) {
    if (key && !store.deletedSessionKeys.includes(key)) store.deletedSessionKeys.push(key)
  }
  delete store.profiles[userId]
  writeStore(store)
}

export function deleteSessionRecord(storageKey) {
  const store = readStore()
  if (storageKey && !store.deletedSessionKeys.includes(storageKey)) {
    store.deletedSessionKeys.push(storageKey)
  }
  writeStore(store)
}

export function applyLocalRemovals(patients) {
  const store = readStore()
  const hiddenUsers = new Set(store.deletedUserIds)
  const hiddenSessions = new Set(store.deletedSessionKeys)
  return patients
    .filter((patient) => !hiddenUsers.has(patient.userId))
    .map((patient) => {
      const sessions = (patient.sessions || []).filter((session) => !hiddenSessions.has(session.storageKey))
      return { ...patient, sessions, sessionCount: sessions.length }
    })
}
