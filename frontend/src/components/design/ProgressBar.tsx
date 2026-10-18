/** A thin bar showing how much of a whole is done, e.g. payments made of all payments. */
export function ProgressBar({
  value,
  max,
  color,
  label,
}: {
  value: number
  max: number
  color: string
  label: string
}) {
  const percent = max <= 0 ? 0 : Math.min(100, Math.max(0, (value / max) * 100))
  return (
    <div
      role="progressbar"
      aria-label={label}
      aria-valuemin={0}
      aria-valuemax={max}
      aria-valuenow={value}
      className="h-2 w-full overflow-hidden rounded-full bg-muted"
    >
      <div className="h-full rounded-full" style={{ width: `${percent}%`, background: color }} />
    </div>
  )
}
