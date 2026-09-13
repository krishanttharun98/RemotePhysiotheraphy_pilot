import { parseCloudSaveDump } from '../lib/sessionAdapter.js'

export const DEFAULT_PLAYER_ID = '7EOyuU9AXzVZDR3vtQcDANgahjFD'

const assetUrl = (path) => {
  const base = import.meta.env.BASE_URL.endsWith('/') ? import.meta.env.BASE_URL : `${import.meta.env.BASE_URL}/`
  return `${base}${path.replace(/^\//, '')}`
}

export async function fetchCloudSessions() {
  let live = null
  try {
    const res = await fetch(assetUrl('api/cloud-save/sessions'))
    live = await res.json()
    if (res.ok && live.keys?.length) return live
    if (!res.ok) throw new Error(live.error || 'Cloud Save request failed')
  } catch (err) {
    const dump = await fetchEditorDump()
    if (dump) return dump
    throw err
  }

  const dump = await fetchEditorDump()
  return dump || live
}

async function fetchEditorDump() {
  const res = await fetch(assetUrl('rws-cloud-save.json'), { cache: 'no-store' })
  if (!res.ok) return null
  const data = await res.json()
  return parseCloudSaveDump(data, DEFAULT_PLAYER_ID)
}
