import { useState } from 'react'
import { toast } from 'sonner'
import {
  useCheckImport,
  useExportData,
  useImportData,
  useRestoreSafetyCopy,
  type DataSummary,
  type ImportCheck,
} from '@/api/dataTransfer'
import { ApiError } from '@/api/http'
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { formatDateTime } from './format'
import { dataTexts as t } from './texts'

const minPasswordLength = 6

function errorText(error: unknown): string {
  if (error instanceof ApiError) {
    return Object.values(error.fieldErrors)[0]?.[0] ?? error.message
  }
  return 'Something went wrong. Please try again.'
}

function ErrorLine({ error }: { error: string | null }) {
  return error ? (
    <p role="alert" className="text-sm text-destructive">
      {error}
    </p>
  ) : null
}

/** Password twice, then the file is downloaded (FR-2, FR-3). */
export function ExportDialog({
  open,
  onOpenChange,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const [password, setPassword] = useState('')
  const [repeat, setRepeat] = useState('')
  const [error, setError] = useState<string | null>(null)
  const exportData = useExportData()

  function close(next: boolean) {
    if (!next) {
      setPassword('')
      setRepeat('')
      setError(null)
    }
    onOpenChange(next)
  }

  async function submit() {
    if (password.length < minPasswordLength) return setError(t.export.tooShort)
    if (password !== repeat) return setError(t.export.mismatch)
    setError(null)
    try {
      const fileName = await exportData.mutateAsync(password)
      toast.success(t.export.done(fileName))
      close(false)
    } catch (e) {
      setError(errorText(e))
    }
  }

  return (
    <AlertDialog open={open} onOpenChange={close}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t.export.title}</AlertDialogTitle>
          <AlertDialogDescription>{t.export.description}</AlertDialogDescription>
        </AlertDialogHeader>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="export-password">{t.export.password}</Label>
          <Input
            id="export-password"
            type="password"
            autoComplete="new-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="export-repeat">{t.export.repeat}</Label>
          <Input
            id="export-repeat"
            type="password"
            autoComplete="new-password"
            value={repeat}
            onChange={(e) => setRepeat(e.target.value)}
          />
        </div>
        <p className="text-sm text-muted-foreground">{t.keepSafe}</p>
        <ErrorLine error={error} />
        <AlertDialogFooter>
          <AlertDialogCancel disabled={exportData.isPending}>{t.cancel}</AlertDialogCancel>
          <Button onClick={submit} disabled={exportData.isPending}>
            {t.export.confirm}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

const summaryRows: { key: keyof DataSummary; label: string }[] = [
  { key: 'cards', label: t.summary.cards },
  { key: 'loans', label: t.summary.loans },
  { key: 'inputs', label: t.summary.inputs },
  { key: 'cardPurchases', label: t.summary.cardPurchases },
  { key: 'bills', label: t.summary.bills },
  { key: 'expenses', label: t.summary.expenses },
]

/** What is in the file next to what is in Monthwise now (FR-5). */
function SummaryTable({ file, current }: { file: DataSummary; current: DataSummary }) {
  return (
    <table className="w-full text-sm">
      <thead>
        <tr className="text-left text-muted-foreground">
          <th className="py-1 font-medium" />
          <th className="py-1 text-right font-medium">{t.import.inFile}</th>
          <th className="py-1 text-right font-medium">{t.import.now}</th>
        </tr>
      </thead>
      <tbody>
        {summaryRows.map((row) => (
          <tr key={row.key} className="border-t">
            <th scope="row" className="py-1 text-left font-normal">
              {row.label}
            </th>
            <td className="py-1 text-right tabular-nums">{file[row.key]}</td>
            <td className="py-1 text-right tabular-nums">{current[row.key]}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

/** File and password → check → compare and type REPLACE → import (FR-4, FR-5). */
export function ImportDialog({
  open,
  onOpenChange,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const [file, setFile] = useState<File | null>(null)
  const [password, setPassword] = useState('')
  const [checked, setChecked] = useState<ImportCheck | null>(null)
  const [word, setWord] = useState('')
  const [error, setError] = useState<string | null>(null)
  const check = useCheckImport()
  const importData = useImportData()
  const busy = check.isPending || importData.isPending

  function close(next: boolean) {
    if (!next) {
      setFile(null)
      setPassword('')
      setChecked(null)
      setWord('')
      setError(null)
    }
    onOpenChange(next)
  }

  async function submit() {
    if (!file) return
    setError(null)
    try {
      if (!checked) {
        setChecked(await check.mutateAsync({ file, password }))
        return
      }
      await importData.mutateAsync({ file, password, confirmation: word.trim() })
      toast.success(t.import.done)
      close(false)
    } catch (e) {
      setError(errorText(e))
    }
  }

  return (
    <AlertDialog open={open} onOpenChange={close}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{checked ? t.import.compareTitle : t.import.title}</AlertDialogTitle>
          <AlertDialogDescription>
            {checked ? t.import.replaceWarning : t.import.description}
          </AlertDialogDescription>
        </AlertDialogHeader>
        {checked ? (
          <>
            <p className="text-sm">
              {t.import.made(formatDateTime(checked.fileCreatedAt), checked.fileRelease)}
            </p>
            <SummaryTable file={checked.file} current={checked.current} />
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="import-word">{t.import.typeWord(t.confirmationWord)}</Label>
              <Input
                id="import-word"
                autoComplete="off"
                value={word}
                onChange={(e) => setWord(e.target.value)}
              />
            </div>
          </>
        ) : (
          <>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="import-file">{t.import.file}</Label>
              <Input
                id="import-file"
                type="file"
                accept=".monthwise,application/octet-stream"
                onChange={(e) => setFile(e.target.files?.[0] ?? null)}
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="import-password">{t.import.password}</Label>
              <Input
                id="import-password"
                type="password"
                autoComplete="off"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
              />
            </div>
          </>
        )}
        <ErrorLine error={error} />
        <AlertDialogFooter>
          <AlertDialogCancel disabled={busy}>{t.cancel}</AlertDialogCancel>
          {checked ? (
            <Button
              variant="destructive"
              onClick={submit}
              disabled={busy || word.trim() !== t.confirmationWord}
            >
              {t.import.confirm}
            </Button>
          ) : (
            <Button onClick={submit} disabled={busy || !file || password === ''}>
              {t.import.check}
            </Button>
          )}
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

/** Brings back the data from before the last import, after typing REPLACE (FR-7). */
export function RestoreSafetyCopyDialog({
  open,
  onOpenChange,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const [word, setWord] = useState('')
  const [error, setError] = useState<string | null>(null)
  const restore = useRestoreSafetyCopy()

  function close(next: boolean) {
    if (!next) {
      setWord('')
      setError(null)
    }
    onOpenChange(next)
  }

  async function submit() {
    setError(null)
    try {
      await restore.mutateAsync(word.trim())
      toast.success(t.safetyCopy.done)
      close(false)
    } catch (e) {
      setError(errorText(e))
    }
  }

  return (
    <AlertDialog open={open} onOpenChange={close}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t.safetyCopy.confirmTitle}</AlertDialogTitle>
          <AlertDialogDescription>{t.safetyCopy.confirmDescription}</AlertDialogDescription>
        </AlertDialogHeader>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="restore-word">{t.import.typeWord(t.confirmationWord)}</Label>
          <Input
            id="restore-word"
            autoComplete="off"
            value={word}
            onChange={(e) => setWord(e.target.value)}
          />
        </div>
        <ErrorLine error={error} />
        <AlertDialogFooter>
          <AlertDialogCancel disabled={restore.isPending}>{t.cancel}</AlertDialogCancel>
          <Button
            variant="destructive"
            onClick={submit}
            disabled={restore.isPending || word.trim() !== t.confirmationWord}
          >
            {t.safetyCopy.action}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
