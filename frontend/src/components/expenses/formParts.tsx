import type { ExpenseCategory } from '@/api/expenses'
import { Label } from '@/components/ui/label'
import { NativeSelect } from '@/components/ui/native-select'
import { expenseTexts } from './texts'

/** A labelled form field with its hint or error. */
export function Field({
  id,
  label,
  hint,
  error,
  children,
}: {
  id: string
  label: string
  hint?: string
  error?: string
  children: React.ReactNode
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      {children}
      {hint && !error && <p className="text-xs text-muted-foreground">{hint}</p>}
      {error && <p className="text-sm text-destructive">{error}</p>}
    </div>
  )
}

/**
 * The expense categories to choose from (FR-1): not hidden ones, plus the entry's own, and never Cash
 * withdrawal, which only cash withdrawals use.
 */
export function CategorySelect({
  categories,
  current,
  invalid,
  ...props
}: {
  categories: ExpenseCategory[]
  current: string
  invalid?: boolean
} & React.ComponentProps<typeof NativeSelect>) {
  const options = categories.filter((c) => !c.isFixed && (!c.isHidden || c.id === current))
  return (
    <NativeSelect aria-invalid={invalid} {...props}>
      <option value="">{expenseTexts.fields.chooseCategory}</option>
      {options.map((category) => (
        <option key={category.id} value={category.id}>
          {category.name}
        </option>
      ))}
    </NativeSelect>
  )
}
