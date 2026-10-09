import { parseCloudSaveDump } from '../lib/sessionAdapter.js'
import { fetchUgsSessions, ugsSession } from './ugs.js'

export const DEFAULT_PLAYER_ID = '7EOyuU9AXzVZDR3vtQcDANgahjFD'

const assetUrl = (path) => {
  const base = import.meta.env.BASE_URL.endsWith('/') ? import.meta.env.BASE_URL : `${import.meta.env.BASE_URL}/`
  return `${base}${path.replace(/^\//, '')}`
}

function looksLikeJson(text) {
  const trimmed = String(text || '').replace(/^\uFEFF/, '').trim()
  return trimmed.startsWith('{') || trimmed.startsWith('[')
}

function parseJsonSafe(text) {
  if (!looksLikeJson(text)) return null
  try {
    return JSON.parse(String(text).replace(/^\uFEFF/, ''))
  } catch {
    return null
  }
}

export async function fetchCloudSessions() {
  if (ugsSession()) {
    const raw = await fetchUgsSessions()
    const cloud = raw?.results?.length ? parseCloudSaveDump(raw, raw.playerId) : null
    if (cloud) {
      cloud.source = 'unity-cloud'
      const dump = import.meta.env.DEV ? await fetchEditorDump() : null
      return dump ? mergeSessionPayloads(dump, cloud) : cloud
    }
    throw new Error('Signed in to Unity, but this account has no saved sessions yet. Play a session on the headset, then Reload.')
  }
  const dump = await fetchEditorDump()
  const live = await fetchLiveSessions()
  if (dump && live) return mergeSessionPayloads(live, dump)
  if (dump) return dump
  if (live) return live
  throw new Error('No sessions loaded. Export Cloud Save from Unity (XRHands menu), then Reload.')
}

async function fetchLiveSessions() {
  try {
    const res = await fetch(assetUrl('api/cloud-save/sessions'))
    const text = await res.text()
    const live = parseJsonSafe(text)
    if (!res.ok || !live) return null
    if (!live.keys?.length && !live.sessions) return null
    return live
  } catch {
    return null
  }
}

async function fetchEditorDump() {
  const urls = [assetUrl('rws-cloud-save.json'), '/rws-cloud-save.json']
  for (const url of urls) {
    try {
      const res = await fetch(url, { cache: 'no-store' })
      if (!res.ok) continue
      const text = await res.text()
      if (!looksLikeJson(text)) continue
      return parseCloudSaveDump(text, DEFAULT_PLAYER_ID)
    } catch {
      // Try the next URL.
    }
  }
  return null
}

function mergeSessionPayloads(live, dump) {
  const sessions = { ...(live.sessions || {}), ...(dump.sessions || {}) }
  const liveIndex = live.index?.entries || []
  const dumpIndex = dump.index?.entries || []
  const byKey = new Map()
  ;[...liveIndex, ...dumpIndex].forEach((entry) => {
    if (entry?.storageKey) byKey.set(entry.storageKey, entry)
  })
  return {
    playerId: dump.playerId || live.playerId || DEFAULT_PLAYER_ID,
    projectId: dump.projectId || live.projectId || '',
    environmentId: dump.environmentId || live.environmentId || '',
    source: dump.source || live.source || 'merge',
    keys: Object.keys(sessions),
    index: { entries: [...byKey.values()] },
    sessions,
  }
}
