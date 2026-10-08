import { useState } from 'react'
import { toast } from 'sonner'
import {
  useChangeLoanCategory,
  useLoanCategories,
  type LoanCategory,
  type LoanCategoryChange,
  type LoanSubcategory,
} from '@/api/loans'
import { messageOf } from '@/components/loans/messageOf'
import { loanTexts } from '@/components/loans/texts'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { PageHeader } from '@/pages/PageHeader'

const t = loanTexts.categories

/** Runs one category change and keeps its error next to the row that caused it. */
function useRun() {
  const change = useChangeLoanCategory()
  const [error, setError] = useState<string | null>(null)
  async function run(action: LoanCategoryChange, done?: () => void) {
    setError(null)
    try {
      await change.mutateAsync(action)
      done?.()
      toast.success(t.saved)
    } catch (e) {
      setError(messageOf(e))
    }
  }
  return { run, error, isPending: change.isPending }
}

/** Settings → Loan categories: categories with their subcategories, managed like input categories (FR-8). */
export function LoanCategoriesSettingsPage() {
  const { data: categories, isPending, isError } = useLoanCategories(true)
  const { run, error, isPending: saving } = useRun()
  const [name, setName] = useState('')
  const [allows, setAllows] = useState(false)

  return (
    <>
      <PageHeader title={t.section} description={t.description} />
      <div className="flex max-w-2xl flex-col gap-4">
        <form
          onSubmit={(event) => {
            event.preventDefault()
            void run({ action: 'create', name, allowsResidualValue: allows }, () => {
              setName('')
              setAllows(false)
            })
          }}
          className="flex flex-col gap-1.5"
        >
          <Label htmlFor="new-loan-category">{t.newName}</Label>
          <div className="flex flex-wrap items-center gap-2">
            <Input
              id="new-loan-category"
              className="min-w-48 flex-1"
              value={name}
              maxLength={50}
              onChange={(e) => setName(e.target.value)}
            />
            <ResidualCheckbox
              id="new-loan-category-residual"
              checked={allows}
              onChange={setAllows}
            />
            <Button type="submit" disabled={saving}>
              {t.add}
            </Button>
          </div>
          {error && <p className="text-sm text-destructive">{error}</p>}
        </form>
        <p className="text-xs text-muted-foreground">{t.usedHint}</p>
        {isPending && <p className="text-sm text-muted-foreground">Loading…</p>}
        {isError && <p className="text-sm text-destructive">Categories could not be loaded.</p>}
        <div className="flex flex-col gap-3">
          {categories?.map((category) => (
            <CategoryCard key={category.id} category={category} />
          ))}
        </div>
      </div>
    </>
  )
}

function ResidualCheckbox({
  id,
  checked,
  onChange,
  disabled,
}: {
  id: string
  checked: boolean
  onChange: (checked: boolean) => void
  disabled?: boolean
}) {
  return (
    <span className="flex items-center gap-2">
      <input
        id={id}
        type="checkbox"
        className="size-4 accent-primary"
        checked={checked}
        disabled={disabled}
        onChange={(e) => onChange(e.target.checked)}
      />
      <Label htmlFor={id} className="text-sm font-normal">
        {t.allowsResidualValue}
      </Label>
    </span>
  )
}

function CategoryCard({ category }: { category: LoanCategory }) {
  const { run, error, isPending } = useRun()
  const [editing, setEditing] = useState(false)
  const [name, setName] = useState(category.name)
  const [subcategoryName, setSubcategoryName] = useState('')
  const update = (allowsResidualValue: boolean, newName = category.name) =>
    ({ action: 'update', id: category.id, name: newName, allowsResidualValue }) as const

  return (
    <div className="flex flex-col gap-2 rounded-lg border p-3">
      {editing ? (
        <div className="flex flex-wrap items-center gap-2">
          <Input
            aria-label={`${t.rename} ${category.name}`}
            className="min-w-48 flex-1"
            value={name}
            maxLength={50}
            onChange={(e) => setName(e.target.value)}
          />
          <Button
            size="sm"
            disabled={isPending}
            onClick={() => run(update(category.allowsResidualValue, name), () => setEditing(false))}
          >
            {loanTexts.actions.save}
          </Button>
          <Button size="sm" variant="ghost" onClick={() => setEditing(false)}>
            {loanTexts.actions.cancel}
          </Button>
        </div>
      ) : (
        <div className="flex flex-wrap items-center gap-2">
          <span className={category.isHidden ? 'text-muted-foreground' : 'font-medium'}>
            {category.name}
          </span>
          {category.isHidden && <Badge variant="outline">{t.hidden}</Badge>}
          <span className="ml-auto flex flex-wrap gap-1">
            <Button size="sm" variant="ghost" onClick={() => setEditing(true)}>
              {t.rename}
            </Button>
            <Button
              size="sm"
              variant="ghost"
              disabled={isPending}
              onClick={() => run({ action: category.isHidden ? 'show' : 'hide', id: category.id })}
            >
              {category.isHidden ? t.show : t.hide}
            </Button>
            <Button
              size="sm"
              variant="ghost"
              disabled={isPending}
              onClick={() => run({ action: 'delete', id: category.id })}
            >
              {t.delete}
            </Button>
          </span>
        </div>
      )}
      <ResidualCheckbox
        id={`residual-${category.id}`}
        checked={category.allowsResidualValue}
        disabled={isPending}
        onChange={(checked) => run(update(checked))}
      />
      {error && <p className="text-sm text-destructive">{error}</p>}

      <div className="flex flex-col gap-1 border-t pt-2">
        <span className="text-xs text-muted-foreground">{t.subcategories}</span>
        <ul className="flex flex-col gap-1">
          {category.subcategories.map((subcategory) => (
            <SubcategoryRow key={subcategory.id} subcategory={subcategory} />
          ))}
        </ul>
        <form
          className="flex gap-2 pt-1"
          onSubmit={(event) => {
            event.preventDefault()
            void run(
              { action: 'createSubcategory', categoryId: category.id, name: subcategoryName },
              () => setSubcategoryName(''),
            )
          }}
        >
          <Input
            aria-label={`${t.newSubcategory}: ${category.name}`}
            placeholder={t.newSubcategory}
            value={subcategoryName}
            maxLength={50}
            onChange={(e) => setSubcategoryName(e.target.value)}
          />
          <Button type="submit" size="sm" variant="outline" disabled={isPending}>
            {t.addSubcategory}
          </Button>
        </form>
      </div>
    </div>
  )
}

function SubcategoryRow({ subcategory }: { subcategory: LoanSubcategory }) {
  const { run, error, isPending } = useRun()
  const [editing, setEditing] = useState(false)
  const [name, setName] = useState(subcategory.name)

  return (
    <li className="flex flex-col gap-1">
      {editing ? (
        <div className="flex flex-wrap items-center gap-2">
          <Input
            aria-label={`${t.rename} ${subcategory.name}`}
            className="min-w-48 flex-1"
            value={name}
            maxLength={50}
            onChange={(e) => setName(e.target.value)}
          />
          <Button
            size="xs"
            disabled={isPending}
            onClick={() =>
              run({ action: 'renameSubcategory', id: subcategory.id, name }, () =>
                setEditing(false),
              )
            }
          >
            {loanTexts.actions.save}
          </Button>
          <Button size="xs" variant="ghost" onClick={() => setEditing(false)}>
            {loanTexts.actions.cancel}
          </Button>
        </div>
      ) : (
        <div className="flex flex-wrap items-center gap-2 text-sm">
          <span className={subcategory.isHidden ? 'text-muted-foreground' : undefined}>
            {subcategory.name}
          </span>
          {subcategory.isHidden && <Badge variant="outline">{t.hidden}</Badge>}
          <span className="ml-auto flex flex-wrap gap-1">
            <Button size="xs" variant="ghost" onClick={() => setEditing(true)}>
              {t.rename}
            </Button>
            <Button
              size="xs"
              variant="ghost"
              disabled={isPending}
              onClick={() =>
                run({
                  action: subcategory.isHidden ? 'showSubcategory' : 'hideSubcategory',
                  id: subcategory.id,
                })
              }
            >
              {subcategory.isHidden ? t.show : t.hide}
            </Button>
            <Button
              size="xs"
              variant="ghost"
              disabled={isPending}
              onClick={() => run({ action: 'deleteSubcategory', id: subcategory.id })}
            >
              {t.delete}
            </Button>
          </span>
        </div>
      )}
      {error && <p className="text-sm text-destructive">{error}</p>}
    </li>
  )
}
