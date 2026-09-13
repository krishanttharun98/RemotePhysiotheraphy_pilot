import { Canvas } from '@react-three/fiber'
import { OrbitControls } from '@react-three/drei'
import StandingAvatar from './StandingAvatar.jsx'
import PhaseSnapshot from './PhaseSnapshot.jsx'
import { HEATMAP_STEPS } from '../lib/rwsGeometry.js'

export default function SessionViewport({ phaseSnap }) {
  return (
    <div className="relative h-full min-h-0 w-full overflow-hidden rounded-xl border border-slate-800 bg-slate-950">
      <Canvas
        camera={{ position: [1.35, 1.35, 2.15], fov: 42, near: 0.05, far: 40 }}
        gl={{ antialias: true, alpha: false, preserveDrawingBuffer: true }}
      >
        <color attach="background" args={['#07111f']} />
        <ambientLight intensity={1.1} />
        <hemisphereLight args={['#e2e8f0', '#1e293b', 0.7]} />
        <directionalLight position={[2.2, 4.2, 2.4]} intensity={1.6} />
        <directionalLight position={[-2, 1.6, -1]} intensity={0.55} color="#67e8f9" />

        <gridHelper args={[8, 16, '#22d3ee', '#334155']} />
        <StandingAvatar />
        {phaseSnap ? <PhaseSnapshot phaseSnap={phaseSnap} /> : null}

        <OrbitControls
          enablePan
          enableZoom
          target={[0, 1.05, 0]}
          minDistance={1.1}
          maxDistance={9}
          maxPolarAngle={Math.PI * 0.49}
        />
      </Canvas>

      <div className="pointer-events-none absolute left-4 top-4 rounded-md border border-slate-700 bg-slate-950/80 px-3 py-2 text-xs text-slate-300">
        Drag rotate · Right-click pan · Scroll zoom
      </div>
      {!phaseSnap ? (
        <div className="pointer-events-none absolute bottom-4 left-1/2 -translate-x-1/2 rounded-md border border-slate-700 bg-slate-950/80 px-3 py-2 text-xs text-slate-400">
          Select a phase snapshot to overlay voxel grid and heatmap
        </div>
      ) : (
        <div className="pointer-events-none absolute bottom-4 left-4 rounded-md border border-slate-700 bg-slate-950/85 px-3 py-2 text-xs text-slate-300">
          {(phaseSnap.hemisphere?.quadrantPopped || []).map((popped, q) => {
            const total = phaseSnap.hemisphere.quadrantTotal[q] || 0
            const pct = total ? Math.round((popped / total) * 100) : 0
            const labels = ['TL', 'BL', 'TR', 'BR']
            return (
              <p key={q}>
                {labels[q]} {popped}/{total} ({pct}%)
              </p>
            )
          })}
          <div className="mt-2 flex flex-wrap gap-2 border-t border-slate-800 pt-2 text-[10px] text-slate-400">
            <span>Uncovered transparent</span>
            {HEATMAP_STEPS.map((step) => (
              <span key={step.pops} className="inline-flex items-center gap-1">
                <span
                  className="inline-block h-2.5 w-2.5 rounded-sm"
                  style={{
                    background: `rgb(${Math.round(step.color[0] * 255)} ${Math.round(step.color[1] * 255)} ${Math.round(step.color[2] * 255)})`,
                  }}
                />
                {step.label}
              </span>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
