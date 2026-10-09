import { z } from 'zod'
import { cashWithdrawalCategoryId, type Expense, type ExpenseRequest } from '@/api/expenses'
import { centsToEuroInput, parseEuroInput } from '@/lib/money'
import { expenseTexts } from './texts'

const t = expenseTexts
const isoDate = /^\d{4}-\d{2}-\d{2}$/

/** The form values of an expense or a cash withdrawal (FR-7, FR-8). */
export function expenseFormSchema(cashWithdrawal: boolean) {
  return z
    .object({
      date: z.string().regex(isoDate, t.errors.dateRequired),
      amount: z.string(),
      categoryId: cashWithdrawal ? z.string() : z.string().min(1, t.errors.categoryRequired),
      paidFromId: z
        .string()
        .min(1, cashWithdrawal ? t.errors.accountRequired : t.errors.paidFromRequired),
      description: z.string().max(100),
    })
    .superRefine((values, ctx) => {
      const cents = parseEuroInput(values.amount)
      if (cents === null || cents <= 0) {
        ctx.addIssue({ code: 'custom', path: ['amount'], message: t.errors.amountInvalid })
      }
    })
}

export type ExpenseFormValues = z.infer<ReturnType<typeof expenseFormSchema>>

/** A new expense dated today, paid with what was used last or the only primary account (E1, E4). */
export function emptyExpenseFormValues(today: string, paidFromId: string): ExpenseFormValues {
  return { date: today, amount: '', categoryId: '', paidFromId, description: '' }
}

export function expenseFormValuesOf(expense: Expense): ExpenseFormValues {
  return {
    date: expense.date,
    amount: centsToEuroInput(expense.amountCents),
    categoryId: expense.category.id,
    paidFromId: expense.paidFrom.id,
    description: expense.description ?? '',
  }
}

export function toExpenseRequest(
  values: ExpenseFormValues,
  cashWithdrawal: boolean,
): ExpenseRequest {
  return {
    date: values.date,
    amountCents: parseEuroInput(values.amount) ?? 0,
    categoryId: cashWithdrawal ? cashWithdrawalCategoryId : values.categoryId,
    paidFromId: values.paidFromId,
    description: values.description.trim() === '' ? null : values.description.trim(),
  }
}
