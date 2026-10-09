import { useState } from 'react'
import { toast } from 'sonner'
import {
  useChangePurchaseCategory,
  usePurchaseCategories,
  type PurchaseCategory,
  type PurchaseCategoryChange,
} from '@/api/cardPurchases'
import { cardPurchaseTexts } from '@/components/cardPurchases/texts'
import { messageOf } from '@/components/loans/messageOf'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { PageHeader } from '@/pages/PageHeader'

const t = cardPurchaseTexts.categories

/** Settings → Purchase categories: add, rename, hide, show and delete unused ones (installments FR-10). */
export function PurchaseCategoriesSettingsPage() {
  const { data: categories, isPending, isError } = usePurchaseCategories(true)
  const change = useChangePurchaseCategory()
  const [name, setName] = useState('')
  const [error, setError] = useState<string | null>(null)

  async function add(event: React.FormEvent) {
    event.preventDefault()
    setError(null)
    try {
      await change.mutateAsync({ action: 'create', name })
      setName('')
      toast.success(t.saved)
    } catch (e) {
      setError(messageOf(e))
    }
  }

  return (
    <>
      <PageHeader title={t.section} description={t.description} />
      <div className="flex max-w-2xl flex-col gap-4">
        <form onSubmit={add} className="flex flex-col gap-1.5">
          <Label htmlFor="new-purchase-category">{t.newName}</Label>
          <div className="flex gap-2">
            <Input
              id="new-purchase-category"
              value={name}
              maxLength={50}
              onChange={(e) => setName(e.target.value)}
            />
            <Button type="submit" disabled={change.isPending}>
              {t.add}
            </Button>
          </div>
          {error && <p className="text-sm text-destructive">{error}</p>}
        </form>
        <p className="text-xs text-muted-foreground">{t.usedHint}</p>
        {isPending && <p className="text-sm text-muted-foreground">Loading…</p>}
        {isError && <p className="text-sm text-destructive">Categories could not be loaded.</p>}
        <div className="flex flex-col gap-2">
          {categories?.map((category) => (
            <CategoryRow key={category.id} category={category} />
          ))}
        </div>
      </div>
    </>
  )
}

function CategoryRow({ category }: { category: PurchaseCategory }) {
  const change = useChangePurchaseCategory()
  const [editing, setEditing] = useState(false)
  const [name, setName] = useState(category.name)
  const [error, setError] = useState<string | null>(null)

  async function run(action: PurchaseCategoryChange, done?: () => void) {
    setError(null)
    try {
      await change.mutateAsync(action)
      done?.()
      toast.success(t.saved)
    } catch (e) {
      setError(messageOf(e))
    }
  }

  return (
    <div className="flex flex-col gap-1 rounded-lg border p-3">
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
            disabled={change.isPending}
            onClick={() =>
              run({ action: 'rename', id: category.id, name }, () => setEditing(false))
            }
          >
            {cardPurchaseTexts.actions.save}
          </Button>
          <Button size="sm" variant="ghost" onClick={() => setEditing(false)}>
            {cardPurchaseTexts.actions.cancel}
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
              disabled={change.isPending}
              onClick={() => run({ action: category.isHidden ? 'show' : 'hide', id: category.id })}
            >
              {category.isHidden ? t.show : t.hide}
            </Button>
            <Button
              size="sm"
              variant="ghost"
              disabled={change.isPending}
              onClick={() => run({ action: 'delete', id: category.id })}
            >
              {t.delete}
            </Button>
          </span>
        </div>
      )}
      {error && <p className="text-sm text-destructive">{error}</p>}
    </div>
  )
}
