import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { cardsQueryKey } from './cards'
import { api, type Schemas } from './client'
import { ensureOk, unwrap } from './http'

export type InputEntry = Schemas['InputEntryResponse']
export type InputRequest = Schemas['InputRequest']
export type MonthInputs = Schemas['MonthInputsResponse']
export type InputTotals = Schemas['InputTotalsResponse']
export type RecurrenceFrequency = NonNullable<Schemas['RecurrenceFrequency']>
export type RecurringInput = Schemas['RecurringInputResponse']
export type RecurringInputRequest = Schemas['RecurringInputRequest']
export type Payer = Schemas['PayerResponse']
export type InputCategory = Schemas['InputCategoryResponse']
export type BalanceEntry = Schemas['BalanceEntryResponse']
export type BalanceEntryKind = NonNullable<Schemas['BalanceEntryKind']>
export type BalanceEntryRequest = Schemas['BalanceEntryRequest']
export type AccountBalance = Schemas['AccountBalanceResponse']
export type AccountBalanceDetails = Schemas['AccountBalanceDetailsResponse']
export type ChartGranularity = NonNullable<Schemas['ChartGranularity']>
export type CashFlowChart = Schemas['CashFlowChartResponse']
export type MonthlyReport = Schemas['MonthlyReportResponse']

export const frequencies: RecurrenceFrequency[] = ['Weekly', 'Monthly', 'Quarterly', 'Yearly']

/** Every query that shows inputs or balances; one invalidation refreshes the Month screen, Dashboard, chart and report. */
export const cashFlowQueryKey = ['cash-flow'] as const
const payersKey = ['payers'] as const
const categoriesKey = ['input-categories'] as const

/** Refreshes everything an input change can affect (acceptance criterion 8). */
function useInvalidateCashFlow() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: cashFlowQueryKey }),
      queryClient.invalidateQueries({ queryKey: cardsQueryKey }),
      queryClient.invalidateQueries({ queryKey: payersKey }),
    ])
}

export function useMonthInputs(month: string) {
  return useQuery({
    queryKey: [...cashFlowQueryKey, 'month', month],
    queryFn: async () => unwrap(await api.GET('/api/inputs', { params: { query: { month } } })),
  })
}

export function useInput(id: string | undefined) {
  return useQuery({
    queryKey: [...cashFlowQueryKey, 'input', id],
    queryFn: async () =>
      unwrap(await api.GET('/api/inputs/{id}', { params: { path: { id: id! } } })),
    enabled: id !== undefined,
  })
}

export function useRecurringInput(id: string | undefined) {
  return useQuery({
    queryKey: [...cashFlowQueryKey, 'recurring', id],
    queryFn: async () =>
      unwrap(await api.GET('/api/recurring-inputs/{id}', { params: { path: { id: id! } } })),
    enabled: id !== undefined,
  })
}

/** Identifies a list entry: a stored input, or a calculated occurrence of a recurring input. */
export type EntryRef =
  { kind: 'input'; id: string } | { kind: 'occurrence'; recurringInputId: string; date: string }

export function entryRef(entry: InputEntry): EntryRef {
  return entry.id !== null
    ? { kind: 'input', id: entry.id }
    : { kind: 'occurrence', recurringInputId: entry.recurringInputId!, date: entry.occurrenceDate! }
}

/** Where the edit page of an entry is: a stored input or one calculated occurrence. */
export function entryEditPath(month: string, entry: InputEntry): string {
  const ref = entryRef(entry)
  return ref.kind === 'input'
    ? `/month/${month}/inputs/${ref.id}/edit`
    : `/month/${month}/recurring-inputs/${ref.recurringInputId}/occurrences/${ref.date}/edit`
}

export function useCreateInput() {
  const invalidate = useInvalidateCashFlow()
  return useMutation({
    mutationFn: async (request: InputRequest) =>
      unwrap(await api.POST('/api/inputs', { body: request })),
    onSuccess: invalidate,
  })
}

/** Saves an edited stored input, or stores a changed calculated occurrence. */
export function useSaveEntry() {
  const invalidate = useInvalidateCashFlow()
  return useMutation({
    mutationFn: async ({ ref, request }: { ref: EntryRef; request: InputRequest }) =>
      ref.kind === 'input'
        ? unwrap(
            await api.PUT('/api/inputs/{id}', { params: { path: { id: ref.id } }, body: request }),
          )
        : unwrap(
            await api.PUT('/api/recurring-inputs/{id}/occurrences/{date}', {
              params: { path: { id: ref.recurringInputId, date: ref.date } },
              body: request,
            }),
          ),
    onSuccess: invalidate,
  })
}

/** Ticks or unticks Received (FR-3). A calculated occurrence can only be ticked. */
export function useSetReceived() {
  const invalidate = useInvalidateCashFlow()
  return useMutation({
    mutationFn: async ({ ref, received }: { ref: EntryRef; received: boolean }) => {
      if (ref.kind === 'occurrence') {
        return unwrap(
          await api.POST('/api/recurring-inputs/{id}/occurrences/{date}/received', {
            params: { path: { id: ref.recurringInputId, date: ref.date } },
          }),
        )
      }
      const params = { path: { id: ref.id } }
      return received
        ? unwrap(await api.POST('/api/inputs/{id}/received', { params }))
        : unwrap(await api.DELETE('/api/inputs/{id}/received', { params }))
    },
    onSuccess: invalidate,
  })
}

export function useDeleteEntry() {
  const invalidate = useInvalidateCashFlow()
  return useMutation({
    mutationFn: async (ref: EntryRef) =>
      ensureOk(
        ref.kind === 'input'
          ? await api.DELETE('/api/inputs/{id}', { params: { path: { id: ref.id } } })
          : await api.DELETE('/api/recurring-inputs/{id}/occurrences/{date}', {
              params: { path: { id: ref.recurringInputId, date: ref.date } },
            }),
      ),
    onSuccess: invalidate,
  })
}

export function useUpdateRecurringInput(id: string) {
  const invalidate = useInvalidateCashFlow()
  return useMutation({
    mutationFn: async (request: RecurringInputRequest) =>
      unwrap(
        await api.PUT('/api/recurring-inputs/{id}', { params: { path: { id } }, body: request }),
      ),
    onSuccess: invalidate,
  })
}

export function useStopRecurringInput(id: string) {
  const invalidate = useInvalidateCashFlow()
  return useMutation({
    mutationFn: async () =>
      unwrap(await api.POST('/api/recurring-inputs/{id}/stop', { params: { path: { id } } })),
    onSuccess: invalidate,
  })
}

export function usePayers() {
  return useQuery({
    queryKey: payersKey,
    queryFn: async () => unwrap(await api.GET('/api/payers')),
  })
}

export function useRenamePayer() {
  const invalidate = useInvalidateCashFlow()
  return useMutation({
    mutationFn: async ({ id, name }: { id: string; name: string }) =>
      unwrap(await api.PUT('/api/payers/{id}', { params: { path: { id } }, body: { name } })),
    onSuccess: invalidate,
  })
}

export function useRemovePayer() {
  const invalidate = useInvalidateCashFlow()
  return useMutation({
    mutationFn: async (id: string) =>
      ensureOk(await api.DELETE('/api/payers/{id}', { params: { path: { id } } })),
    onSuccess: invalidate,
  })
}

export function useInputCategories(includeHidden = false) {
  return useQuery({
    queryKey: [...categoriesKey, includeHidden],
    queryFn: async () =>
      unwrap(await api.GET('/api/input-categories', { params: { query: { includeHidden } } })),
  })
}

export type CategoryChange =
  | { action: 'create'; name: string }
  | { action: 'rename'; id: string; name: string }
  | { action: 'hide' | 'show' | 'delete'; id: string }

export function useChangeCategory() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (change: CategoryChange) => {
      switch (change.action) {
        case 'create':
          return unwrap(await api.POST('/api/input-categories', { body: { name: change.name } }))
        case 'rename':
          return unwrap(
            await api.PUT('/api/input-categories/{id}', {
              params: { path: { id: change.id } },
              body: { name: change.name },
            }),
          )
        case 'hide':
          return unwrap(
            await api.POST('/api/input-categories/{id}/hide', {
              params: { path: { id: change.id } },
            }),
          )
        case 'show':
          return unwrap(
            await api.POST('/api/input-categories/{id}/show', {
              params: { path: { id: change.id } },
            }),
          )
        case 'delete':
          return ensureOk(
            await api.DELETE('/api/input-categories/{id}', { params: { path: { id: change.id } } }),
          )
      }
    },
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: categoriesKey }),
        queryClient.invalidateQueries({ queryKey: cashFlowQueryKey }),
      ]),
  })
}

export function useAccountBalance(accountId: string) {
  return useQuery({
    queryKey: [...cashFlowQueryKey, 'balance', accountId],
    queryFn: async () =>
      unwrap(await api.GET('/api/cards/{id}/balance', { params: { path: { id: accountId } } })),
  })
}

export type BalanceChange =
  | { action: 'create'; request: BalanceEntryRequest }
  | { action: 'update'; id: string; request: BalanceEntryRequest }
  | { action: 'delete'; id: string }

export function useChangeBalanceEntry(accountId: string) {
  const invalidate = useInvalidateCashFlow()
  return useMutation({
    mutationFn: async (change: BalanceChange) => {
      switch (change.action) {
        case 'create':
          return unwrap(
            await api.POST('/api/cards/{id}/balance-entries', {
              params: { path: { id: accountId } },
              body: change.request,
            }),
          )
        case 'update':
          return unwrap(
            await api.PUT('/api/balance-entries/{id}', {
              params: { path: { id: change.id } },
              body: change.request,
            }),
          )
        case 'delete':
          return ensureOk(
            await api.DELETE('/api/balance-entries/{id}', { params: { path: { id: change.id } } }),
          )
      }
    },
    onSuccess: invalidate,
  })
}

export interface ChartQuery {
  from: string
  to: string
  granularity: ChartGranularity
  accountId?: string
  payerId?: string
  categoryId?: string
  loanCategoryId?: string
  cardId?: string
}

export function useCashFlowChart(query: ChartQuery) {
  return useQuery({
    queryKey: [...cashFlowQueryKey, 'chart', query],
    // Keep the previous chart while another period or filter loads, so it does not flash.
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { from, to, granularity, accountId, payerId, categoryId, loanCategoryId, cardId } =
        query
      return unwrap(
        await api.GET('/api/cash-flow/chart', {
          params: {
            query: {
              from,
              to,
              granularity,
              ...(accountId ? { accountId } : {}),
              ...(payerId ? { payerId } : {}),
              ...(categoryId ? { categoryId } : {}),
              ...(loanCategoryId ? { loanCategoryId } : {}),
              ...(cardId ? { cardId } : {}),
            },
          },
        }),
      )
    },
  })
}

export function useMonthlyReport(month: string) {
  return useQuery({
    queryKey: [...cashFlowQueryKey, 'report', month],
    queryFn: async () =>
      unwrap(await api.GET('/api/cash-flow/report', { params: { query: { month } } })),
  })
}
