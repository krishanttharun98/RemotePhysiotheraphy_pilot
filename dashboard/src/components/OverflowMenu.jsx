import { MoreVertical } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'

export default function OverflowMenu({ items }) {
  const [open, setOpen] = useState(false)
  const rootRef = useRef(null)

  useEffect(() => {
    if (!open) return undefined
    function onPointerDown(event) {
      if (!rootRef.current?.contains(event.target)) setOpen(false)
    }
    function onKey(event) {
      if (event.key === 'Escape') setOpen(false)
    }
    document.addEventListener('pointerdown', onPointerDown)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('pointerdown', onPointerDown)
      document.removeEventListener('keydown', onKey)
    }
  }, [open])

  return (
    <div className="relative shrink-0" ref={rootRef}>
      <button
        type="button"
        aria-label="More actions"
        onClick={(event) => {
          event.stopPropagation()
          setOpen((value) => !value)
        }}
        className="rounded-md p-1 text-slate-400 hover:bg-slate-800 hover:text-slate-100"
      >
        <MoreVertical className="h-4 w-4" />
      </button>
      {open ? (
        <div className="absolute right-0 z-30 mt-1 min-w-44 rounded-lg border border-slate-700 bg-slate-900 py-1 shadow-xl">
          {items.map((item) => (
            <button
              key={item.label}
              type="button"
              onClick={(event) => {
                event.stopPropagation()
                setOpen(false)
                item.onClick()
              }}
              className={`block w-full px-3 py-2 text-left text-sm ${
                item.danger ? 'text-rose-300 hover:bg-rose-950/40' : 'text-slate-200 hover:bg-slate-800'
              }`}
            >
              {item.label}
            </button>
          ))}
        </div>
      ) : null}
    </div>
  )
}
