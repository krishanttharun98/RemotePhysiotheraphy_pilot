import { Buffer } from 'node:buffer'
import dns from 'node:dns'
import { existsSync, readFileSync } from 'node:fs'
import https from 'node:https'
import { join } from 'node:path'
import { parseCloudSaveDump } from '../src/lib/sessionAdapter.js'

dns.setDefaultResultOrder('ipv4first')

const PROJECT_ID = '35a0f424-a2ce-45a4-b2f3-5a9e3b9d734b'
const ENVIRONMENT_ID = 'b2efe99c-adb3-43d6-ac05-fe9ad3802e83'
const ENVIRONMENT_NAME = 'production'
const DEFAULT_PLAYER_ID = '7EOyuU9AXzVZDR3vtQcDANgahjFD'
const INDEX_KEY = 'rws_session_index'
const WEBSITE_USERNAME = 'xrhandstherapist'
const WEBSITE_PASSWORD = 'XRHands-Pilot0000'

function json(res, status, body) {
  res.statusCode = status
  res.setHeader('Content-Type', 'application/json')
  res.end(JSON.stringify(body))
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    const chunks = []
    req.on('data', (c) => chunks.push(c))
    req.on('end', () => {
      if (!chunks.length) {
        resolve({})
        return
      }
      try {
        resolve(JSON.parse(Buffer.concat(chunks).toString('utf8')))
      } catch (err) {
        reject(err)
      }
    })
    req.on('error', reject)
  })
}

function credsFrom(req, env) {
  return {
    projectId: env.UGS_PROJECT_ID || PROJECT_ID,
    playerId: req.headers['x-ugs-player-id'] || env.UGS_PLAYER_ID || DEFAULT_PLAYER_ID,
    environmentId: req.headers['x-ugs-environment-id'] || env.UGS_ENVIRONMENT_ID || ENVIRONMENT_ID,
    environmentName: env.UGS_ENVIRONMENT_NAME || ENVIRONMENT_NAME,
    keyId: req.headers['x-ugs-key-id'] || env.UGS_KEY_ID || '',
    secretKey: req.headers['x-ugs-secret-key'] || env.UGS_SECRET_KEY || '',
  }
}

function basicAuth(keyId, secretKey) {
  return `Basic ${Buffer.from(`${keyId}:${secretKey}`).toString('base64')}`
}

export function unwrapCloudValue(value, depth = 0) {
  if (value == null || depth > 4) return value
  if (typeof value === 'string') {
    const trimmed = value.trim()
    if ((trimmed.startsWith('{') && trimmed.endsWith('}')) || (trimmed.startsWith('[') && trimmed.endsWith(']'))) {
      try {
        return unwrapCloudValue(JSON.parse(trimmed), depth + 1)
      } catch {
        return value
      }
    }
    return value
  }
  if (typeof value === 'object' && !Array.isArray(value)) {
    const keys = Object.keys(value)
    if (keys.length === 1 && keys[0] === 'value') return unwrapCloudValue(value.value, depth + 1)
  }
  return value
}

function itemMap(items) {
  const map = {}
  for (const item of items) {
    if (!item?.key) continue
    map[item.key] = unwrapCloudValue(item.value)
  }
  return map
}

function itemBases({ projectId, environmentId, playerId, visibility, admin }) {
  const suffix = visibility === 'public' ? '/public/items' : '/items'
  if (admin) {
    return [
      `https://services.api.unity.com/cloud-save/v1/data/projects/${projectId}/environments/${environmentId}/players/${playerId}${suffix}`,
    ]
  }
  return [`https://cloud-save.services.api.unity.com/v1/data/projects/${projectId}/players/${playerId}${suffix}`]
}

function requestJson(url, { method = 'GET', headers = {}, body } = {}) {
  return new Promise((resolve, reject) => {
    const target = new URL(url)
    const payload = body == null ? null : Buffer.from(typeof body === 'string' ? body : JSON.stringify(body))
    const req = https.request(
      {
        protocol: target.protocol,
        hostname: target.hostname,
        port: target.port || 443,
        path: `${target.pathname}${target.search}`,
        method,
        timeout: 45000,
        headers: {
          ...headers,
          ...(payload ? { 'Content-Length': String(payload.length) } : {}),
        },
      },
      (res) => {
        const chunks = []
        res.on('data', (chunk) => chunks.push(chunk))
        res.on('end', () => {
          const text = Buffer.concat(chunks).toString('utf8')
          let data = {}
          try {
            data = text ? JSON.parse(text) : {}
          } catch {
            data = { raw: text }
          }
          resolve({
            ok: res.statusCode >= 200 && res.statusCode < 300,
            status: res.statusCode,
            data,
          })
        })
      },
    )
    req.on('timeout', () => {
      req.destroy()
      reject(new Error(`Unity request timed out: ${target.hostname}`))
    })
    req.on('error', reject)
    if (payload) req.write(payload)
    req.end()
  })
}

async function tokenExchange({ keyId, secretKey, projectId, environmentId }) {
  const url = new URL('https://services.api.unity.com/auth/v1/token-exchange')
  url.searchParams.set('projectId', projectId)
  if (environmentId) url.searchParams.set('environmentId', environmentId)

  const bodies = [{ scopes: ['cloud_save.data.read', 'cloud_save.entities.list'] }, {}]
  let lastError = 'Token exchange failed'
  for (const body of bodies) {
    const res = await requestJson(url, {
      method: 'POST',
      headers: {
        Authorization: basicAuth(keyId, secretKey),
        'Content-Type': 'application/json',
      },
      body,
    })
    const data = res.data
    if (res.ok) {
      const token = data.accessToken || data.access_token
      if (token) return token
      lastError = 'Token exchange returned no accessToken'
      continue
    }
    lastError = data.detail || data.title || `Token exchange failed (${res.status})`
  }
  throw new Error(lastError)
}

async function usernamePasswordSignIn(projectId, environmentName) {
  const res = await requestJson('https://player-auth.services.api.unity.com/v1/authentication/usernamepassword/sign-in', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      ProjectId: projectId,
      UnityEnvironment: environmentName || ENVIRONMENT_NAME,
    },
    body: { username: WEBSITE_USERNAME, password: WEBSITE_PASSWORD },
  })
  if (!res.ok) {
    throw new Error(res.data.detail || res.data.title || `Username sign-in failed (${res.status})`)
  }
  const token = res.data.idToken || res.data.accessToken || res.data.access_token
  const userId = res.data.userId || res.data.user?.id
  if (!token) throw new Error('Username sign-in returned no token')
  return { token, userId }
}

async function anonymousPlayerToken(projectId, environmentName) {
  const attempts = [
    { UnityEnvironment: environmentName || ENVIRONMENT_NAME },
    {},
  ]
  let lastError = 'Anonymous Unity auth failed'
  for (const extra of attempts) {
    const res = await requestJson('https://player-auth.services.api.unity.com/v1/authentication/anonymous', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ProjectId: projectId,
        ...extra,
      },
      body: {},
    })
    const data = res.data
    if (res.ok) {
      const token = data.idToken || data.accessToken || data.access_token
      if (token) return token
      lastError = 'Anonymous auth returned no token'
      continue
    }
    lastError = data.detail || data.title || `Anonymous auth failed (${res.status})`
  }
  throw new Error(lastError)
}

async function fetchItemsPage({ token, base, after, keys, prefix }) {
  const url = new URL(base)
  if (after) url.searchParams.set('after', after)
  if (prefix) url.searchParams.set('prefix', prefix)
  if (keys?.length) keys.forEach((key) => url.searchParams.append('keys', key))
  const res = await requestJson(url, {
    headers: {
      Authorization: `Bearer ${token}`,
      Accept: 'application/json',
    },
  })
  const data = res.data
  if (!res.ok) {
    const err = new Error(data.detail || data.title || `Cloud Save ${res.status}`)
    err.status = res.status
    throw err
  }
  return data
}

async function listFromBase(args) {
  const items = []
  let after
  for (let page = 0; page < 25; page++) {
    const data = await fetchItemsPage({ ...args, after })
    const results = data.results || []
    items.push(...results)
    if (!results.length || !data.links?.next) break
    after = results[results.length - 1].key
  }
  return items
}

async function listPlayerItems(args) {
  const bases = itemBases(args)
  const prefixAttempts = args.prefix ? [args.prefix, undefined] : [undefined]
  let lastError = 'Cloud Save request failed'
  for (const base of bases) {
    for (const prefix of prefixAttempts) {
      try {
        return await listFromBase({ ...args, base, prefix })
      } catch (err) {
        lastError = err.message
        if (err.status && ![400, 401, 403, 404].includes(err.status)) throw err
      }
    }
  }
  throw new Error(lastError)
}

async function hydrateSessionValues(args) {
  const items = await listPlayerItems(args)
  const values = itemMap(items.filter((item) => !item.key || item.key.startsWith('rws_session')))
  if (!values[INDEX_KEY]) {
    try {
      const indexItems = await listPlayerItems({ ...args, keys: [INDEX_KEY], prefix: undefined })
      Object.assign(values, itemMap(indexItems))
    } catch {
      // Index key is optional if individual rws_session_* items already came back.
    }
  }

  const index = unwrapCloudValue(values[INDEX_KEY]) || { entries: [] }
  const missing = (Array.isArray(index.entries) ? index.entries : [])
    .map((entry) => entry?.storageKey)
    .filter((key) => key && !values[key])
  if (missing.length) {
    try {
      const extra = await listPlayerItems({ ...args, keys: missing, prefix: undefined })
      Object.assign(values, itemMap(extra))
    } catch {
      // Missing bodies still appear as index-only rows in the website directory.
    }
  }
  return values
}

function sessionPayload({ playerId, projectId, environmentId, values, source }) {
  const sessions = {}
  for (const [key, value] of Object.entries(values)) {
    if (key === INDEX_KEY || key.startsWith('rws_session_')) sessions[key] = value
  }
  return {
    playerId,
    projectId,
    environmentId,
    source,
    keys: Object.keys(sessions),
    index: sessions[INDEX_KEY] || { entries: [] },
    sessions,
  }
}

function readEditorDump(playerId) {
  const file = join(process.cwd(), 'public/rws-cloud-save.json')
  if (!existsSync(file)) return null
  try {
    return {
      ...parseCloudSaveDump(readFileSync(file, 'utf8'), playerId || DEFAULT_PLAYER_ID),
      source: 'unity-editor',
    }
  } catch {
    return null
  }
}

async function loadSessionsForPlayer(merged) {
  const values = {}
  let source = 'cloud'
  let playerId = merged.playerId || DEFAULT_PLAYER_ID

  if (merged.keyId && merged.secretKey) {
    try {
      const token = await tokenExchange(merged)
      Object.assign(
        values,
        await hydrateSessionValues({
          token,
          projectId: merged.projectId,
          environmentId: merged.environmentId,
          playerId,
          visibility: 'default',
          prefix: 'rws_session',
          admin: true,
        }),
      )
      source = 'service-account'
    } catch {
      // Fall through to player login / public reads.
    }
  }

  try {
    const signedIn = await usernamePasswordSignIn(merged.projectId, merged.environmentName)
    playerId = signedIn.userId || playerId
    Object.assign(
      values,
      await hydrateSessionValues({
        token: signedIn.token,
        projectId: merged.projectId,
        environmentId: merged.environmentId,
        playerId,
        visibility: 'default',
        prefix: 'rws_session',
        admin: false,
      }),
    )
    source = 'player'
  } catch {
    // Username is linked the first time the Quest app launches with the updated build.
  }

  try {
    const token = await anonymousPlayerToken(merged.projectId, merged.environmentName)
    const published = await hydrateSessionValues({
      token,
      projectId: merged.projectId,
      environmentId: merged.environmentId,
      playerId: DEFAULT_PLAYER_ID,
      visibility: 'public',
      prefix: 'rws_session',
      admin: false,
    })
    for (const [key, value] of Object.entries(published)) {
      if (!values[key]) values[key] = value
    }
    if (source === 'cloud') source = 'public'
  } catch {
    // Public copy is optional when player login already returned private snapshots.
  }

  if (!Object.keys(values).some((key) => key.startsWith('rws_session'))) {
    const dump = readEditorDump(playerId)
    if (dump) return dump
  }

  return sessionPayload({
    playerId,
    projectId: merged.projectId,
    environmentId: merged.environmentId,
    values,
    source,
  })
}

function attachRoutes(server, env) {
  server.middlewares.use(async (req, res, next) => {
    if (!req.url?.startsWith('/api/cloud-save')) return next()

    try {
      const url = new URL(req.url, 'http://localhost')
      const creds = credsFrom(req, env)

      if (req.method === 'GET' && url.pathname === '/api/cloud-save/defaults') {
        json(res, 200, {
          projectId: creds.projectId,
          playerId: creds.playerId,
          environmentId: creds.environmentId,
          hasServerCredentials: Boolean(env.UGS_KEY_ID && env.UGS_SECRET_KEY),
        })
        return
      }

      if (req.method === 'GET' && url.pathname === '/api/cloud-save/sessions') {
        try {
          const payload = await loadSessionsForPlayer({
            ...creds,
            environmentId: creds.environmentId || ENVIRONMENT_ID,
            environmentName: creds.environmentName || ENVIRONMENT_NAME,
            playerId: url.searchParams.get('playerId') || creds.playerId,
          })
          json(res, 200, payload)
        } catch (err) {
          const dump = readEditorDump(creds.playerId)
          if (dump) {
            json(res, 200, dump)
            return
          }
          throw err
        }
        return
      }

      json(res, 404, { error: 'Not found' })
        } catch (err) {
          const cause = err.cause?.message || err.cause?.code || ''
          json(res, 500, { error: [err.message, cause].filter(Boolean).join(': ') || 'Cloud Save proxy failed' })
        }
  })
}

export function cloudSavePlugin(env) {
  return {
    name: 'ugs-cloud-save-proxy',
    configureServer(server) {
      attachRoutes(server, env)
    },
    configurePreviewServer(server) {
      attachRoutes(server, env)
    },
  }
}
