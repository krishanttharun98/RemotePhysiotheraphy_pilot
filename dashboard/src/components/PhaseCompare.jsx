import { compareRows } from '../lib/phaseMetrics.js'

export default function PhaseCompare({ gamePhase, plainPhase, title, who, when }) {
  const rows = compareRows(gamePhase, plainPhase)
  const hand = `${gamePhase?.metrics?.exerciseHand || '—'} / ${plainPhase?.metrics?.exerciseHand || '—'}`

  return (
    <div className="space-y-3 text-slate-200">
      <div>
        <p className="text-xs uppercase tracking-widest text-cyan-400">Game vs non-game</p>
        <h2 className="text-sm font-semibold text-slate-50">{title} — game vs non-game</h2>
        <p className="text-xs italic text-slate-500">
          {who || 'Session'}
          {when ? `  ·  ${when}` : ''}
        </p>
      </div>
      <div className="overflow-x-auto rounded-xl border border-slate-800">
        <table className="min-w-full text-left text-xs">
          <thead className="bg-slate-900 text-[11px] uppercase tracking-wider">
            <tr>
              <th className="px-3 py-2 font-semibold text-cyan-300">Metric</th>
              <th className="px-3 py-2 font-semibold text-amber-300">Game</th>
              <th className="px-3 py-2 font-semibold text-sky-300">Non-game</th>
              <th className="px-3 py-2 font-semibold text-slate-400">Difference (non-game − game)</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.name} className="border-t border-slate-800">
                <td className="px-3 py-1.5 text-slate-300">{row.name}</td>
                <td className="px-3 py-1.5 font-semibold text-slate-50">{row.game}</td>
                <td className="px-3 py-1.5 font-semibold text-slate-50">{row.plain}</td>
                <td className="px-3 py-1.5 text-slate-400">{row.delta}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="text-[11px] italic text-slate-500">Hand exercised (game / non-game): {hand}</p>
    </div>
  )
}
