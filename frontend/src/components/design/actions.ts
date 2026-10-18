import { Banknote, ReceiptText, Repeat, ShoppingBag, ShoppingCart, Wallet } from 'lucide-react'
import { meaningColors, sectionColors } from './colors'

/**
 * Icons and colours of the add actions (design refresh FR-14), shared by the Dashboard quick
 * actions and the add buttons in the Month sections so adding looks the same everywhere.
 */
export const addActions = {
  income: { icon: Wallet, color: meaningColors.moneyIn },
  bill: { icon: ReceiptText, color: meaningColors.moneyOut },
  expense: { icon: ShoppingCart, color: meaningColors.expense },
  cash: { icon: Banknote, color: sectionColors.cashflow },
  purchase: { icon: ShoppingBag, color: sectionColors.cards },
  recurring: { icon: Repeat, color: sectionColors.month },
} as const
