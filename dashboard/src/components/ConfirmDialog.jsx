export default function ConfirmDialog({ title, body, confirmLabel = 'Delete', onConfirm, onCancel }) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 px-4">
      <div className="w-full max-w-md rounded-2xl border border-slate-700 bg-slate-900 p-6 shadow-2xl">
        <h2 className="text-lg font-semibold text-slate-50">{title}</h2>
        <p className="mt-3 text-sm leading-6 text-slate-400">{body}</p>
        <div className="mt-6 flex justify-end gap-2">
          <button
            type="button"
            onClick={onCancel}
            className="rounded-lg border border-slate-700 px-4 py-2 text-sm text-slate-200 hover:bg-slate-800"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={onConfirm}
            className="rounded-lg bg-rose-500 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-rose-400"
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  )
}
