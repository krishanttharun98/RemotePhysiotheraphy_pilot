import { Activity, Eye, Headset, Laptop, Lock, Stethoscope } from 'lucide-react'

const pillars = [
  {
    icon: Headset,
    title: 'Patient in mixed reality',
    body: 'A seated reaching assessment in a headset. Virtual targets map how far the arm can move through the space around the body.',
  },
  {
    icon: Eye,
    title: 'Therapist, remotely present',
    body: 'A clinician can join from another headset as a guiding avatar — same session, without being in the same room.',
  },
  {
    icon: Laptop,
    title: 'Clinic review on a laptop',
    body: 'When a headset is not needed, the same session opens on a desktop console: history, coverage maps, and 3D replay.',
  },
]

const outcomes = [
  { label: 'Standardised reach', detail: 'Shoulder and elbow movement captured as reachable workspace, not a subjective note.' },
  { label: 'Remote supervision', detail: 'Live guidance or later review — built for tele-rehabilitation workflows.' },
  { label: 'Comparable over time', detail: 'Session snapshots so clinicians can see change across visits, not only the last one.' },
  { label: 'Ready for clinic pilots', detail: 'Research-grade protocol, packaged as a product clinicians can actually run.' },
]

export default function LandingPage({ onSignIn }) {
  return (
    <div className="min-h-full overflow-y-auto bg-slate-950 text-slate-200">
      <header className="sticky top-0 z-20 border-b border-slate-800/80 bg-slate-950/85 backdrop-blur">
        <div className="mx-auto flex h-16 max-w-6xl items-center justify-between px-6">
          <a href="#about" className="flex items-center gap-2.5 text-slate-50">
            <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-cyan-500/15 text-cyan-300">
              <Stethoscope className="h-5 w-5" />
            </span>
            <span>
              <span className="block text-[10px] font-medium uppercase tracking-[0.2em] text-cyan-400">MIRA</span>
              <span className="block text-sm font-semibold leading-none">Reachable workspace, remotely</span>
            </span>
          </a>
          <nav className="hidden items-center gap-8 text-sm text-slate-400 md:flex">
            <a href="#about" className="hover:text-slate-100">
              Product
            </a>
            <a href="#how" className="hover:text-slate-100">
              How it works
            </a>
            <a href="#console" className="hover:text-slate-100">
              Clinician console
            </a>
          </nav>
          <button
            type="button"
            onClick={onSignIn}
            className="inline-flex items-center gap-2 rounded-lg bg-cyan-500 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-cyan-400"
          >
            <Lock className="h-4 w-4" />
            Sign in
          </button>
        </div>
      </header>

      <main>
        <section className="mx-auto grid max-w-6xl gap-12 px-6 py-20 md:grid-cols-[1.15fr_0.85fr] md:items-center md:py-28">
          <div>
            <p className="mb-4 text-xs font-semibold uppercase tracking-[0.22em] text-cyan-400">
              Mixed-reality tele-assessment
            </p>
            <h1 className="text-4xl font-semibold leading-tight text-slate-50 md:text-5xl">
              Measure upper-limb reach at home. Review it like a clinic product.
            </h1>
            <p className="mt-6 max-w-xl text-base leading-7 text-slate-400">
              MIRA turns reachable workspace assessment into a remote session: the patient reaches toward
              virtual targets in a headset, while the therapist supervises live or studies the 3D record later.
              Built from clinical research. Designed to go to market.
            </p>
            <div className="mt-8 flex flex-wrap items-center gap-3">
              <button
                type="button"
                onClick={onSignIn}
                className="inline-flex items-center gap-2 rounded-lg bg-cyan-500 px-5 py-2.5 text-sm font-semibold text-slate-950 hover:bg-cyan-400"
              >
                <Lock className="h-4 w-4" />
                Sign in to clinician console
              </button>
              <a
                href="#how"
                className="inline-flex items-center rounded-lg border border-slate-700 px-5 py-2.5 text-sm text-slate-200 hover:border-slate-500"
              >
                See how it works
              </a>
            </div>
          </div>

          <aside className="rounded-2xl border border-slate-800 bg-gradient-to-b from-slate-900 to-slate-950 p-6 shadow-2xl">
            <div className="mb-5 flex items-center gap-2 text-cyan-300">
              <Activity className="h-5 w-5" />
              <p className="text-sm font-medium text-slate-100">What a session produces</p>
            </div>
            <ul className="space-y-4 text-sm leading-6 text-slate-400">
              <li>Coverage of the reachable workspace, by region — not a single pass/fail score.</li>
              <li>A 3D snapshot the therapist can rotate, compare, and keep in the patient record.</li>
              <li>The same protocol every time, so visits can be compared.</li>
            </ul>
            <p className="mt-6 border-t border-slate-800 pt-5 text-xs leading-5 text-slate-500">
              First markets: paediatric and adult neurorehabilitation, neuromuscular clinics, and telehealth
              services that need a structured upper-limb measure.
            </p>
          </aside>
        </section>

        <section id="about" className="border-y border-slate-800 bg-slate-900/40">
          <div className="mx-auto grid max-w-6xl gap-10 px-6 py-16 md:grid-cols-2 md:items-start">
            <div>
              <h2 className="text-2xl font-semibold text-slate-50">The problem we sell against</h2>
              <p className="mt-4 text-sm leading-7 text-slate-400">
                How far someone can reach is a core sign of arm function. In most services it is still judged by
                watching in clinic, or it needs a motion-capture lab. Neither scales to the home, and neither
                gives a therapist a clean 3D record they can share or trend.
              </p>
            </div>
            <div>
              <h2 className="text-2xl font-semibold text-slate-50">The product</h2>
              <p className="mt-4 text-sm leading-7 text-slate-400">
                MIRA is a clinician-supervised digital assessment. It is not an unsupervised exercise game.
                The measurement stays aligned with published reachable-workspace methods. The interface is
                split on purpose: the patient is immersed; the clinician chooses a headset or a laptop.
              </p>
            </div>
          </div>
        </section>

        <section id="how" className="mx-auto max-w-6xl px-6 py-16">
          <h2 className="text-2xl font-semibold text-slate-50">How it works</h2>
          <p className="mt-3 max-w-2xl text-sm leading-6 text-slate-400">
            One assessment. Two clinician surfaces. The patient never has to learn the analysis tools.
          </p>
          <div className="mt-10 grid gap-5 md:grid-cols-3">
            {pillars.map((item) => (
              <article key={item.title} className="rounded-2xl border border-slate-800 bg-slate-900/50 p-6">
                <item.icon className="mb-4 h-6 w-6 text-cyan-300" />
                <h3 className="text-base font-semibold text-slate-50">{item.title}</h3>
                <p className="mt-2 text-sm leading-6 text-slate-400">{item.body}</p>
              </article>
            ))}
          </div>
        </section>

        <section className="border-y border-slate-800 bg-slate-900/30">
          <div className="mx-auto max-w-6xl px-6 py-16">
            <h2 className="text-2xl font-semibold text-slate-50">Why teams adopt it</h2>
            <div className="mt-8 grid gap-6 sm:grid-cols-2">
              {outcomes.map((item) => (
                <div key={item.label} className="border-l-2 border-cyan-500/70 pl-4">
                  <p className="text-sm font-semibold text-slate-100">{item.label}</p>
                  <p className="mt-1 text-sm leading-6 text-slate-400">{item.detail}</p>
                </div>
              ))}
            </div>
          </div>
        </section>

        <section id="console" className="mx-auto max-w-6xl px-6 py-20">
          <div className="rounded-2xl border border-slate-800 bg-slate-900 px-6 py-10 md:flex md:items-center md:justify-between md:px-10">
            <div className="max-w-xl">
              <h2 className="text-2xl font-semibold text-slate-50">Clinician console</h2>
              <p className="mt-3 text-sm leading-6 text-slate-400">
                Sign in to open the therapist workspace: patient directory, phase snapshots, and the 3D
                reachable-workspace view. Pilot access is for authorised clinicians only.
              </p>
            </div>
            <button
              type="button"
              onClick={onSignIn}
              className="mt-6 inline-flex items-center gap-2 rounded-lg bg-cyan-500 px-5 py-2.5 text-sm font-semibold text-slate-950 hover:bg-cyan-400 md:mt-0"
            >
              <Lock className="h-4 w-4" />
              Sign in
            </button>
          </div>
        </section>
      </main>

      <footer className="border-t border-slate-800 py-8">
        <div className="mx-auto flex max-w-6xl flex-col gap-2 px-6 text-xs text-slate-500 md:flex-row md:items-center md:justify-between">
          <p>MIRA — mixed-reality reachable workspace assessment</p>
          <p>Research-informed product. Preparing for clinical and commercial pilots.</p>
        </div>
      </footer>
    </div>
  )
}
