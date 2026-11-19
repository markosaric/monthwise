import { plural, type Translation } from '@/i18n/texts'
import type { DashboardTextsEn } from './texts'

/** Croatian texts of the Dashboard. */
export const dashboardTextsHr: Translation<DashboardTextsEn> = {
  subtitle: 'Kako ide ovaj mjesec?',
  quickActions: {
    addIncome: 'Dodaj uplatu',
    addBill: 'Dodaj račun',
    addExpense: 'Dodaj trošak',
    cashWithdrawal: 'Podizanje gotovine',
    addPurchase: 'Dodaj kupnju',
    payRevolving: 'Plati revolving karticu',
  },
  keyFigures: 'Ključni pokazatelji',
  inputs: 'Uplate',
  outputs: 'Isplate',
  monthResult: 'Rezultat mjeseca',
  stillToPay: 'Preostalo za platiti',
  overallBalance: {
    label: 'Ukupno stanje',
    primaryAccounts: (count: number) =>
      `${count} ${plural(count, { one: 'tekući račun', few: 'tekuća računa', other: 'tekućih računa' })}`,
    withoutAccount: (count: number) =>
      `${count} ${plural(count, { one: 'isplata', few: 'isplate', other: 'isplata' })} bez računa`,
    missingStartingBalance: 'nedostaje početni saldo',
    noPrimary: 'Označite tekući račun',
  },
  received: (amount: string) => `${amount} primljeno`,
  paid: (amount: string) => `${amount} plaćeno`,
  items: (count: number) =>
    `${count} ${plural(count, { one: 'stavka', few: 'stavke', other: 'stavki' })}`,
  overdueItems: (count: number) => `${count} dospjelo`,
  highlight: {
    overdue: (count: number, amount: string) =>
      `${count} ${plural(count, {
        one: 'račun ili rata je dospio',
        few: 'računa ili rate su dospjela',
        other: 'računa ili rata je dospjelo',
      })}: ${amount}`,
    overdueHint: 'Otvorite zaslon Mjesec da ih platite.',
    next: (name: string, amount: string, date: string) =>
      `Sljedeće: ${name} ${amount} dana ${date}`,
    more: (count: number) =>
      count === 0
        ? 'Ovaj mjesec ništa drugo ne dospijeva.'
        : `Još ${count} ${plural(count, {
            one: 'račun ili rata dospijeva',
            few: 'računa ili rate dospijevaju',
            other: 'računa ili rata dospijeva',
          })} ovaj mjesec.`,
    allPaid: (month: string) => `Svi računi i rate za ${month} su plaćeni`,
    allPaidHint: 'Ovaj mjesec ništa nije preostalo za platiti.',
    empty: 'Dodajte uplate i račune ovog mjeseca',
    emptyHint: 'Počnite na zaslonu Mjesec.',
    openMonth: 'Otvori Mjesec',
  },
  comingUp: 'Uskoro · sljedećih 7 dana',
  nothingComingUp: 'U sljedećih 7 dana ništa ne dospijeva.',
  openMonth: 'Otvori Mjesec',
  cashFlow: {
    title: 'Novčani tok',
    chart: 'Uplate i isplate u zadnjih 6 mjeseci',
  },
  small: {
    loans: (amount: string) => `${amount} / mjesečno`,
    loansDetail: (amount: string) => `Krediti · ${amount} za otplatiti`,
    cardPayments: 'Plaćanja kartica ovaj mjesec',
    cardsWithInstallments: (count: number) =>
      `${count} ${plural(count, { one: 'kartica', few: 'kartice', other: 'kartica' })} s ratama`,
    sharedCosts: 'Nenaplaćeni zajednički troškovi',
    owing: (count: number) =>
      `${count} ${plural(count, { one: 'sudionik duguje', few: 'sudionika duguju', other: 'sudionika duguje' })}`,
    recurring: (count: number) =>
      `${count} ${plural(count, { one: 'ponavljajući račun', few: 'ponavljajuća računa', other: 'ponavljajućih računa' })}`,
    notIssued: (count: number) => `${count} nije izdano ovaj mjesec`,
  },
  loadError: 'Pregled se nije mogao učitati.',
}
