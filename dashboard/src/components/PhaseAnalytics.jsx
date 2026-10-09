import { clock, fmt, fmtPos, pickHand, rsaPercent } from '../lib/phaseMetrics.js'

const QUAD_ORDER = [0, 2, 1, 3]
const QUAD_NAMES = ['UL', 'LL', 'UR', 'LR']

function Tile({ label, value, hint }) {
  return (
    <div className="rounded-xl border border-slate-800 bg-slate-950/80 px-3 py-2.5">
      <p className="text-[10px] uppercase tracking-wider text-slate-500">{label}</p>
      <p className="mt-1 text-lg font-semibold text-slate-50">{value}</p>
      {hint ? <p className="mt-0.5 text-[11px] text-slate-500">{hint}</p> : null}
    </div>
  )
}

function Section({ title, children }) {
  return (
    <section className="space-y-2">
      <h3 className="text-[11px] font-semibold uppercase tracking-[0.16em] text-cyan-400">{title}</h3>
      {children}
    </section>
  )
}

function Quadrants({ rsa, contact, total }) {
  return (
    <div className="grid grid-cols-2 gap-2">
      {QUAD_ORDER.map((q) => {
        const r = rsa?.[q] ?? 0
        const t = Math.min(1, r / 0.25)
        const bg = `color-mix(in srgb, rgb(229 77 64) ${Math.round((1 - t) * 55)}%, rgb(52 211 153) ${Math.round(t * 55)}%)`
        return (
          <div key={q} className="rounded-lg px-3 py-2 text-xs text-slate-100" style={{ background: bg }}>
            <span className="font-semibold">{QUAD_NAMES[q]}</span>
            {'  '}RSA {r.toFixed(2)}
            <span className="text-slate-300">/0.25</span>
            {contact ? <span className="ml-2 text-slate-200">contact {fmt(contact[q], 0)}%</span> : null}
          </div>
        )
      })}
      {total >= 0 ? <p className="col-span-2 text-[11px] text-slate-500">Total RSA {fmt(total, 2)}</p> : null}
    </div>
  )
}

function EndpointScatter({ targets }) {
  const hits = (targets || []).filter((t) => t.hit === 1 && t.w > 0).slice(0, 80)
  return (
    <svg viewBox="0 0 150 84" className="h-[84px] w-[150px] rounded-lg border border-slate-800 bg-slate-950/70">
      <text x="8" y="12" fill="#94a3b8" fontSize="9">
        endpoints / W
      </text>
      <circle cx="75" cy="70" r="22" fill="none" stroke="rgba(255,255,255,0.35)" />
      {hits.map((t, i) => {
        const px = 75 + Math.max(-1.5, Math.min(1.5, t.dAlong / t.w)) * 50
        const py = 70 - Math.max(0, Math.min(1.5, t.dAcross / t.w)) * 40
        return <circle key={i} cx={px} cy={py} r="2.2" fill={t.over > 0 ? '#fbbf24' : '#22d3ee'} />
      })}
    </svg>
  )
}

function Overview({ metrics, hemisphere }) {
  return (
    <>
      <p className="text-xs italic text-slate-500">
        Overview from the saved phase metrics. Full kinematic analysis is recorded for sessions played with the current
        headset build.
      </p>
      <div className="grid grid-cols-3 gap-2">
        <Tile label="RSA" value={fmtPos(metrics.rsaPercent, 0)} hint="%" />
        <Tile label="Completion" value={metrics.elapsedSeconds > 0 ? clock(metrics.elapsedSeconds) : '—'} hint="min:s" />
        <Tile
          label="Fitts trials"
          value={`${metrics.trialCount || 0}`}
          hint={metrics.fittsVsBaseline > 0 ? `${fmt(metrics.fittsVsBaseline, 2)}x baseline` : ''}
        />
        <Tile label="Mean speed" value={fmtPos(metrics.meanSpeedMetresPerSec, 2)} hint="m/s" />
        <Tile
          label="Wobble"
          value={metrics.wobbleRmsMetres > 0 ? fmt(metrics.wobbleRmsMetres * 1000, 1) : '—'}
          hint="mm RMS"
        />
        <Tile label="Throughput" value={fmtPos(metrics.throughputBitsPerSec, 2)} hint="bits/s" />
      </div>
      {hemisphere ? <Quadrants rsa={hemisphere.quadrantRsa} total={hemisphere.totalRsa} /> : null}
    </>
  )
}

export default function PhaseAnalytics({ phase, title }) {
  const metrics = phase?.metrics
  const a = metrics?.analytics
  const hand = metrics?.exerciseHand ? ` · ${metrics.exerciseHand} hand` : ''

  if (!phase) {
    return <p className="text-sm text-slate-500">Select a phase to see Fitts, SPARC, workspace and symmetry.</p>
  }

  return (
    <div className="space-y-4 text-slate-200">
      <div>
        <p className="text-xs uppercase tracking-widest text-cyan-400">Phase analysis</p>
        <h2 className="text-sm font-semibold text-slate-50">
          {title || `Phase ${phase.historyIndex || ''}`}
          {a?.game ? ` · ${a.game}` : hand}
        </h2>
      </div>

      {!a ? (
        metrics ? <Overview metrics={metrics} hemisphere={phase.hemisphere} /> : <p className="text-sm text-slate-500">No metrics saved for this phase.</p>
      ) : (
        <>
          <p className="text-xs text-slate-400">
            Applied game play · {clock(a.durationSec)} · {a.reached}/{a.offered} targets reached
          </p>
          {a.strategy ? <p className="text-[11px] italic text-slate-500">{a.strategy}</p> : null}
          <p className="text-[11px] text-slate-500">
            Reference: {a.refLabel || '—'} · {fmt(a.homeStartPct, 0)}% of reaches started there
          </p>

          <Section title="Fitts · applied (from play)">
            <div className="grid grid-cols-4 gap-2">
              <Tile
                label="Throughput"
                value={a.throughputEffective > 0 ? fmt(a.throughputEffective, 2) : fmtPos(a.throughputNominal, 2)}
                hint="bits/s"
              />
              <Tile label="Movement time" value={fmtPos(a.meanMT, 2)} hint={a.sdMT > 0 ? `± ${fmt(a.sdMT, 2)} s` : 's'} />
              <Tile label="Not reached" value={fmt(a.errorRatePct, 0)} hint="%" />
              <Tile label="Eff. width" value={a.weMetres > 0 ? fmt(a.weMetres * 100, 1) : '—'} hint="cm" />
            </div>
            <p className="text-[11px] text-slate-500">
              IDe {fmt(a.effectiveID, 1)} bits
              {a.slopeMsPerBit
                ? ` · MT = ${fmt(a.interceptMs, 0)} + ${fmt(a.slopeMsPerBit, 0)}·ID ms (R² ${fmt(a.r2, 2)})`
                : ' · MT–ID fit needs ≥ 2 blocks'}
              {' · '}
              {a.blocks?.length || 0} A×W blocks
            </p>
          </Section>

          <Section title="Endpoint">
            <div className="flex flex-wrap items-start gap-3">
              <div className="grid min-w-[240px] flex-1 grid-cols-2 gap-2">
                <Tile
                  label="Overshoot"
                  value={`${fmt(a.overshootPct, 0)}%`}
                  hint={a.overshootMean > 0 ? `${fmt(a.overshootMean * 100, 1)} cm past` : ''}
                />
                <Tile
                  label="Spread (SD)"
                  value={`${fmt(a.endpointSdAlong * 100, 1)} / ${fmt(a.endpointSdAcross * 100, 1)}`}
                  hint="cm along / across"
                />
              </div>
              <EndpointScatter targets={a.targets} />
            </div>
          </Section>

          <Section title={`RWS clinical · RSA ${fmt(a.totalRsa || rsaPercent(phase) / 100, 2)} / 1.00`}>
            <Quadrants rsa={a.quadrantRsa} contact={a.quadrantContactPct} total={-1} />
          </Section>

          <Section title="Kinematics · left | right">
            <div className="grid grid-cols-3 gap-2">
              <Tile
                label="Velocity mean"
                value={`${fmt(a.left?.meanSpeed, 2)} | ${fmt(a.right?.meanSpeed, 2)}`}
                hint={`m/s · peak ${fmt(a.left?.peakSpeed, 1)} | ${fmt(a.right?.peakSpeed, 1)}`}
              />
              <Tile
                label="Smoothness (SPARC)"
                value={`${fmt(a.left?.sparc, 1)} | ${fmt(a.right?.sparc, 1)}`}
                hint={`peaks/reach ${fmt(a.left?.peaksPerReach, 1)} | ${fmt(a.right?.peaksPerReach, 1)}`}
              />
              <Tile
                label="Symmetry (SI)"
                value={`${fmt(a.siSpeed, 0)}%`}
                hint={`smooth ${fmt(a.siSmoothness, 0)}% · reach ${fmt(a.siReach, 0)}%`}
              />
            </div>
          </Section>

          <Section title="Reachable workspace · left | right">
            <div className="grid grid-cols-3 gap-2">
              <Tile
                label="Covered area"
                value={`${fmt(a.left?.areaPct, 0)} | ${fmt(a.right?.areaPct, 0)}`}
                hint={`% hemi · ${fmt(a.left?.areaM2, 2)}|${fmt(a.right?.areaM2, 2)} m²`}
              />
              <Tile
                label="Reach distance"
                value={`${fmt((a.left?.reachMax || 0) * 100, 0)} | ${fmt((a.right?.reachMax || 0) * 100, 0)}`}
                hint={`cm max · mean ${fmt((a.left?.reachMean || 0) * 100, 0)} | ${fmt((a.right?.reachMean || 0) * 100, 0)}`}
              />
              <Tile
                label="Contact"
                value={`${a.left?.reaches || 0} | ${a.right?.reaches || 0}`}
                hint={`reaches · area SI ${fmt(a.siArea, 0)}%`}
              />
            </div>
          </Section>

          {pickHand(metrics)?.ldlj != null ? (
            <p className="text-[11px] text-slate-500">
              LDLJ {fmt(a.left?.ldlj, 2)} | {fmt(a.right?.ldlj, 2)} · closer to 0 SPARC / higher LDLJ = smoother
            </p>
          ) : null}
        </>
      )}
    </div>
  )
}
