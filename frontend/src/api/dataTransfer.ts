import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef } from 'react'
import { api, type Schemas } from './client'
import { useHealth } from './health'
import { ApiError, unwrap } from './http'

export type DataOverview = Schemas['DataOverviewResponse']
export type DataTransfer = Schemas['DataTransferResponse']
export type DataTransferKind = Schemas['DataTransferKind']
export type DataSummary = Schemas['DataSummaryResponse']
export type BackupReminder = Schemas['BackupReminderResponse']
export type ImportCheck = Schemas['ImportCheckResponse']
export type ImportResult = Schemas['ImportResponse']

export const dataQueryKey = ['data-transfer'] as const

/** The last export and import, the safety copy and the reminder (backup and restore FR-1, FR-9). */
export function useDataOverview() {
  return useQuery({
    queryKey: [...dataQueryKey, 'overview'],
    queryFn: async () => unwrap(await api.GET('/api/data')),
  })
}

export function useDataHistory(page: number) {
  return useQuery({
    queryKey: [...dataQueryKey, 'history', page],
    queryFn: async () =>
      unwrap(await api.GET('/api/data/history', { params: { query: { page } } })),
  })
}

/** Same origin as the typed client; multipart and file downloads use fetch directly. */
const url = (path: string) => new URL(path, window.location.origin)

/** Throws an `ApiError` from a failed response that carries problem details. */
async function throwProblem(response: Response): Promise<never> {
  const problem = (await response.json().catch(() => ({}))) as {
    title?: string
    errors?: Record<string, string[]>
  }
  throw new ApiError(response.status, problem.title, problem.errors)
}

/** Saves a downloaded file on the user's device, on a phone as on a desktop. */
function saveFile(blob: Blob, fileName: string) {
  const href = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = href
  link.download = fileName
  document.body.append(link)
  link.click()
  link.remove()
  setTimeout(() => URL.revokeObjectURL(href), 1000)
}

/** The file name the API gives in Content-Disposition, or a dated fallback. */
function fileNameOf(response: Response): string {
  const header = response.headers.get('Content-Disposition') ?? ''
  const star = /filename\*=UTF-8''([^;]+)/i.exec(header)
  if (star?.[1]) return decodeURIComponent(star[1])
  const plain = /filename="?([^";]+)"?/i.exec(header)
  return plain?.[1] ?? 'monthwise-export.monthwise'
}

/** Exports all data with a password and downloads the file (FR-2, FR-3). */
export function useExportData() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (password: string) => {
      const response = await globalThis.fetch(url('/api/data/export'), {
        method: 'POST',
        credentials: 'same-origin',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ password }),
      })
      if (!response.ok) await throwProblem(response)
      const fileName = fileNameOf(response)
      saveFile(await response.blob(), fileName)
      return fileName
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: dataQueryKey }),
  })
}

function importForm(file: File, password: string, confirmation?: string): FormData {
  const form = new FormData()
  form.append('file', file)
  form.append('password', password)
  if (confirmation !== undefined) form.append('confirmation', confirmation)
  return form
}

async function postForm<T>(path: string, form: FormData): Promise<T> {
  const response = await globalThis.fetch(url(path), {
    method: 'POST',
    credentials: 'same-origin',
    body: form,
  })
  if (!response.ok) await throwProblem(response)
  return (await response.json()) as T
}

/** Reads a file and shows what it holds; changes nothing (FR-4). */
export function useCheckImport() {
  return useMutation({
    mutationFn: ({ file, password }: { file: File; password: string }) =>
      postForm<ImportCheck>('/api/data/import/check', importForm(file, password)),
  })
}

/** Every screen shows the imported data at once (FR-7). */
function useReloadEverything() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries()
}

/** Replaces all data with the file's data (FR-5, FR-7). */
export function useImportData() {
  const reload = useReloadEverything()
  return useMutation({
    mutationFn: ({
      file,
      password,
      confirmation,
    }: {
      file: File
      password: string
      confirmation: string
    }) => postForm<ImportResult>('/api/data/import', importForm(file, password, confirmation)),
    onSettled: reload,
  })
}

/** Brings back the data from before the newest import (FR-7). */
export function useRestoreSafetyCopy() {
  const reload = useReloadEverything()
  return useMutation({
    mutationFn: async (confirmation: string) =>
      unwrap(await api.POST('/api/data/safety-copy/restore', { body: { confirmation } })),
    onSettled: reload,
  })
}

export function useUpdateReminder() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (body: { enabled: boolean; intervalDays: number }) =>
      unwrap(await api.PUT('/api/data/reminder', { body })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: dataQueryKey }),
  })
}

export function useDismissReminder() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async () => unwrap(await api.POST('/api/data/reminder/dismiss')),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: dataQueryKey }),
  })
}

/**
 * Reloads every screen when data was imported elsewhere, e.g. on another device (FR-7). The health
 * check runs every 30 seconds and carries the time of the last import.
 */
export function useReloadAfterImport() {
  const { data } = useHealth()
  const queryClient = useQueryClient()
  const seen = useRef<string | null | undefined>(undefined)
  const importedAt = data?.dataImportedAt
  useEffect(() => {
    if (importedAt === undefined) return
    if (seen.current !== undefined && seen.current !== importedAt) {
      void queryClient.invalidateQueries({
        predicate: (query) => query.queryKey[0] !== 'health',
      })
    }
    seen.current = importedAt
  }, [importedAt, queryClient])
}
