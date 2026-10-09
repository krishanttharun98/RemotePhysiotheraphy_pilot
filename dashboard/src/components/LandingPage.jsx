import { Activity, Headset, Home, Laptop, Lock, Stethoscope } from 'lucide-react'

const childSteps = [
  {
    step: '1',
    title: 'Seated, in mixed reality',
    body: 'The child wears a headset while seated in a standard position. They can still see their real surroundings, which keeps the session comfortable and safe.',
  },
  {
    step: '2',
    title: 'Reach for virtual targets',
    body: 'Targets appear around them. They are guided to reach for each one as far as they comfortably can.',
  },
  {
    step: '3',
    title: 'The whole reach, not one joint',
    body: 'The task samples shoulder flexion, abduction, horizontal reach, and elbow extension — across front, rear, and trunk-level space.',
  },
  {
    step: '4',
    title: 'Game-like, still a real assessment',
    body: 'It is framed as a short, structured activity so children stay engaged and give their best effort, without changing what is being measured.',
  },
]

const reasons = [
  {
    label: 'Objective, not only observation',
    detail:
      'Measures the 3D space a child can actually reach, using a method already used in reachable-workspace research, rather than visual judgement alone.',
  },
  {
    label: 'Remote, with a clinician in the loop',
    detail:
      'A therapist supervises every session, live or in review. This is not an unsupervised app — it extends a clinician’s reach without removing them.',
  },
  {
    label: 'Fits how clinics already think',
    detail:
      'Sessions produce reachable-workspace coverage and region-specific results that can sit beside existing assessment routines.',
  },
  {
    label: 'Mixed reality, not a closed-off world',
    detail:
      'The child stays aware of the real room throughout, which matters for comfort, safety, and trust.',
  },
  {
    label: 'Designed for children to tolerate',
    detail:
      'The research asks whether children and families find it comfortable and easy to use, not only whether the numbers look clean.',
  },
  {
    label: 'Less travel for the same picture',
    detail:
      'Families may not need to come in for every assessment, while the therapist still gets a reviewable 3D record of function.',
  },
]

const conditions = [
  'Cerebral palsy',
  'Brachial plexus birth injury',
  'Traumatic brain injury',
  'Stroke',
  'Spinal cord injury',
]

export default function LandingPage({ onSignIn }) {
  return (
    <div className="min-h-full overflow-y-auto bg-slate-950 text-slate-200">
      <header className="sticky top-0 z-20 border-b border-slate-800/80 bg-slate-950/85 backdrop-blur">
        <div className="mx-auto flex h-16 max-w-6xl items-center justify-between px-6">
          <a href="#top" className="flex items-center gap-2.5 text-slate-50">
            <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-cyan-500/15 text-cyan-300">
              <Stethoscope className="h-5 w-5" />
            </span>
            <span>
              <span className="block text-[10px] font-medium uppercase tracking-[0.2em] text-cyan-400">MIRA</span>
              <span className="block text-sm font-semibold leading-none">Reachable workspace, remotely</span>
            </span>
          </a>
          <nav className="hidden items-center gap-8 text-sm text-slate-400 md:flex">
            <a href="#problem" className="hover:text-slate-100">
              Why it matters
            </a>
            <a href="#how" className="hover:text-slate-100">
              How it works
            </a>
            <a href="#who" className="hover:text-slate-100">
              Who it is for
            </a>
            <a href="#console" className="hover:text-slate-100">
              Pilot console
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

      <main id="top">
        <section className="mx-auto grid max-w-6xl gap-12 px-6 py-20 md:grid-cols-[1.15fr_0.85fr] md:items-center md:py-28">
          <div>
            <p className="mb-4 text-xs font-semibold uppercase tracking-[0.22em] text-cyan-400">
              Research project · mixed-reality assessment
            </p>
            <h1 className="text-4xl font-semibold leading-tight text-slate-50 md:text-5xl">
              Reachable workspace assessment, wherever the child is.
            </h1>
            <p className="mt-6 max-w-xl text-base leading-7 text-slate-400">
              A mixed-reality assessment that measures how far children can reach in 3D space — turning a
              clinical test into a short, game-like experience, supervised remotely by a therapist.
            </p>
            <div className="mt-8 flex flex-wrap items-center gap-3">
              <a
                href="#contact"
                className="inline-flex items-center rounded-lg bg-cyan-500 px-5 py-2.5 text-sm font-semibold text-slate-950 hover:bg-cyan-400"
              >
                Get in touch
              </a>
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
              <p className="text-sm font-medium text-slate-100">In one sentence</p>
            </div>
            <p className="text-sm leading-6 text-slate-400">
              The child reaches for virtual targets in a fun, structured task. The system records how far they
              reach, in every direction, and gives the therapist an objective 3D picture of upper-limb
              function — without needing to be in the same room.
            </p>
            <p className="mt-6 border-t border-slate-800 pt-5 text-xs leading-5 text-slate-500">
              MIRA is a research prototype with a path toward clinical use. It is not a commercial product
              yet.
            </p>
          </aside>
        </section>

        <section id="problem" className="border-y border-slate-800 bg-slate-900/40">
          <div className="mx-auto max-w-6xl px-6 py-16">
            <h2 className="text-2xl font-semibold text-slate-50">
              The problem with how upper-limb function is measured today
            </h2>
            <p className="mt-4 max-w-3xl text-sm leading-7 text-slate-400">
              Children with conditions like cerebral palsy, brachial plexus birth injury, traumatic brain
              injury, or stroke often have trouble reaching, grasping, and using their arms in daily life.
              Measuring how much movement they have regained — or lost — is harder than it sounds.
            </p>
            <p className="mt-6 text-sm font-medium text-slate-200">Most clinics still rely on:</p>
            <ul className="mt-3 grid gap-4 text-sm leading-6 text-slate-400 md:grid-cols-3">
              <li className="rounded-xl border border-slate-800 bg-slate-950/50 p-4">
                Isolated joint measurements that do not capture how a child actually reaches in 3D space.
              </li>
              <li className="rounded-xl border border-slate-800 bg-slate-950/50 p-4">
                Subjective observation, where results can vary between therapists and clinic visits.
              </li>
              <li className="rounded-xl border border-slate-800 bg-slate-950/50 p-4">
                In-person-only assessment, which is hard to access for families far from a specialist clinic.
              </li>
            </ul>
            <p className="mt-6 max-w-3xl text-sm leading-7 text-slate-400">
              The result is assessment that can be inconsistent, time-consuming for therapists, and often out
              of reach for families who need it most.
            </p>
          </div>
        </section>

        <section id="about" className="mx-auto max-w-6xl px-6 py-16">
          <h2 className="text-2xl font-semibold text-slate-50">
            A mixed-reality assessment that measures the whole reach, not just one joint
          </h2>
          <p className="mt-4 max-w-3xl text-sm leading-7 text-slate-400">
            MIRA uses reachable workspace (RWS) analysis — a method already used in clinical research — to
            measure the full 3D space a child’s hand can reach, not just isolated joint angles. We deliver it
            through a mixed-reality headset the child wears at home or in clinic, as a short, game-like
            reaching task, while a therapist supervises and reviews the results remotely.
          </p>
        </section>

        <section id="how" className="border-y border-slate-800 bg-slate-900/30">
          <div className="mx-auto max-w-6xl px-6 py-16">
            <h2 className="text-2xl font-semibold text-slate-50">How a session works</h2>
            <p className="mt-3 max-w-2xl text-sm leading-6 text-slate-400">
              One assessment. Two ways for the therapist to supervise. The child never has to learn the
              analysis tools.
            </p>

            <h3 className="mt-10 text-sm font-semibold uppercase tracking-wider text-cyan-400">For the child</h3>
            <div className="mt-4 grid gap-4 sm:grid-cols-2">
              {childSteps.map((item) => (
                <article key={item.step} className="rounded-2xl border border-slate-800 bg-slate-950/50 p-5">
                  <p className="text-xs font-semibold text-cyan-400">Step {item.step}</p>
                  <h4 className="mt-1 text-base font-semibold text-slate-50">{item.title}</h4>
                  <p className="mt-2 text-sm leading-6 text-slate-400">{item.body}</p>
                </article>
              ))}
            </div>

            <h3 className="mt-12 text-sm font-semibold uppercase tracking-wider text-cyan-400">
              For the therapist
            </h3>
            <p className="mt-2 max-w-3xl text-sm leading-6 text-slate-400">
              Two ways to supervise, same data either way. The therapist chooses the setup that fits — not
              the other way around.
            </p>
            <div className="mt-6 grid gap-5 md:grid-cols-2">
              <article className="rounded-2xl border border-slate-800 bg-slate-950/50 p-6">
                <Headset className="mb-4 h-6 w-6 text-cyan-300" />
                <h4 className="text-base font-semibold text-slate-50">Immersed mode</h4>
                <p className="mt-2 text-sm leading-6 text-slate-400">
                  The therapist joins the same shared mixed-reality space in their own headset — watching the
                  child reach in real time, guiding, and encouraging, from anywhere.
                </p>
              </article>
              <article className="rounded-2xl border border-slate-800 bg-slate-950/50 p-6">
                <Laptop className="mb-4 h-6 w-6 text-cyan-300" />
                <h4 className="text-base font-semibold text-slate-50">Laptop review</h4>
                <p className="mt-2 text-sm leading-6 text-slate-400">
                  No headset required. The therapist uses session controls, patient history, and 3D results —
                  a heatmap and grid showing which areas were reached and which were not.
                </p>
              </article>
            </div>
            <div className="mt-6 flex gap-3 rounded-2xl border border-slate-800 bg-slate-950/50 p-5 text-sm leading-6 text-slate-400">
              <Home className="mt-0.5 h-5 w-5 shrink-0 text-cyan-300" />
              <p>
                After the session, results are saved for later review, so progress can be compared across
                visits — something much harder to do consistently with in-person, observation-only
                assessment.
              </p>
            </div>
          </div>
        </section>

        <section className="mx-auto max-w-6xl px-6 py-16">
          <h2 className="text-2xl font-semibold text-slate-50">Why clinicians are interested in MIRA</h2>
          <div className="mt-8 grid gap-6 sm:grid-cols-2">
            {reasons.map((item) => (
              <div key={item.label} className="border-l-2 border-cyan-500/70 pl-4">
                <p className="text-sm font-semibold text-slate-100">{item.label}</p>
                <p className="mt-1 text-sm leading-6 text-slate-400">{item.detail}</p>
              </div>
            ))}
          </div>
        </section>

        <section className="border-y border-slate-800 bg-slate-900/40">
          <div className="mx-auto max-w-6xl px-6 py-16">
            <h2 className="text-2xl font-semibold text-slate-50">Built on established clinical methods</h2>
            <p className="mt-4 max-w-3xl text-sm leading-7 text-slate-400">
              MIRA is not a new, untested idea. It is built on reachable workspace analysis, already used in
              clinical research to measure upper-limb function in children and adults with neurological
              conditions. This project takes that approach and asks whether it can be delivered remotely,
              through mixed reality, in a format designed for children.
            </p>
          </div>
        </section>

        <section id="who" className="mx-auto max-w-6xl px-6 py-16">
          <h2 className="text-2xl font-semibold text-slate-50">Who it is for</h2>
          <p className="mt-4 max-w-3xl text-sm leading-7 text-slate-400">
            Children aged 4–17 with upper-limb movement impairment, delivered through outpatient
            rehabilitation services, with parent or guardian involvement in every session. Conditions under
            study include:
          </p>
          <ul className="mt-6 flex flex-wrap gap-2">
            {conditions.map((name) => (
              <li
                key={name}
                className="rounded-full border border-slate-700 bg-slate-900 px-3 py-1.5 text-sm text-slate-300"
              >
                {name}
              </li>
            ))}
          </ul>
        </section>

        <section id="contact" className="border-t border-slate-800 bg-slate-900/30">
          <div className="mx-auto max-w-6xl px-6 py-16">
            <h2 className="text-2xl font-semibold text-slate-50">
              Interested in bringing MIRA to your clinic or service?
            </h2>
            <p className="mt-4 max-w-2xl text-sm leading-7 text-slate-400">
              We are running a feasibility pilot with paediatric rehabilitation in mind. If you are a
              clinician, researcher, or care provider who wants to learn more, get in touch with the research
              team. If you already have pilot access, sign in to the clinician console.
            </p>
            <div className="mt-8 flex flex-wrap gap-3">
              <button
                type="button"
                onClick={onSignIn}
                className="inline-flex items-center gap-2 rounded-lg bg-cyan-500 px-5 py-2.5 text-sm font-semibold text-slate-950 hover:bg-cyan-400"
              >
                <Lock className="h-4 w-4" />
                Sign in to the pilot console
              </button>
            </div>
          </div>
        </section>

        <section id="console" className="mx-auto max-w-6xl px-6 py-12">
          <div className="rounded-2xl border border-slate-800 bg-slate-900 px-6 py-8 md:flex md:items-center md:justify-between md:px-10">
            <div className="max-w-xl">
              <h2 className="text-xl font-semibold text-slate-50">Pilot clinician console</h2>
              <p className="mt-3 text-sm leading-6 text-slate-400">
                Authorised users can open patient history and 3D reachable-workspace snapshots from the
                laptop review pathway.
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
          <p>A research project with potential for later clinical and commercial use.</p>
        </div>
      </footer>
    </div>
  )
}
