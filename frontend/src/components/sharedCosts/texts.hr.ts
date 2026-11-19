import { plural, type Translation } from '@/i18n/texts'
import { formatCents } from '@/lib/money'
import type { SharedCostTextsEn } from './texts'

/** Croatian shared cost labels and messages. */
export const sharedCostTextsHr: Translation<SharedCostTextsEn> = {
  share: {
    shared: 'Zajednički',
    sharedHint: 'Dijeli se na jednake udjele s jednim sudionikom, koji vraća svoj dio.',
    participant: 'Sudionik',
    chooseParticipant: 'Odaberite sudionika',
    addNew: 'Dodaj novog…',
    newName: 'Ime novog sudionika',
    add: 'Dodaj',
    shares: 'Udjeli',
    participantShares: 'Sudionik plaća',
    sharesOption: (count: number) =>
      `${count} ${plural(count, { one: 'udio', few: 'udjela', other: 'udjela' })}`,
    participantSharesOption: (count: number, of: number) => `${count} od ${of}`,
    contributionAccount: 'Doprinosi se uplaćuju na',
    contributionAccountDefault: 'Račun s kojeg se plaća',
    summary: (name: string, percentage: number) => `${name} plaća ${percentage} %`,
    parts: (participantCents: number, ownCents: number) =>
      `Njihov dio ${formatCents(participantCents)}, vaš dio ${formatCents(ownCents)}.`,
    participantRequired: 'Odaberite tko dijeli trošak.',
    sharedMarker: 'Zajednički',
    changedMarker: 'Udio promijenjen',
    ownPart: (cents: number) => `Vaš dio ${formatCents(cents)}`,
    ownPartTotal: 'Vaš dio',
    keepChanged: (count: number) =>
      `Zadrži ručno postavljen udio na ${count} ${plural(count, {
        one: 'očekivanoj rati',
        few: 'očekivane rate',
        other: 'očekivanih rata',
      })}`,
  },
  payment: {
    title: 'Dijeli ovu ratu',
    description:
      'Dijeli samo ovu ratu ili je prestaje dijeliti, bez obzira na udio kredita. Ostale rate ostaju kakve jesu.',
    followLoan: 'Prati udio kredita',
    save: 'Spremi',
    cancel: 'Odustani',
    saved: 'Udio je spremljen.',
  },
  contribution: {
    title: 'Doprinos',
    of: (cost: string) => `Za ${cost}`,
    part: (cents: number) => `Dio ${formatCents(cents)}`,
    edited: 'Uređeno',
    needsAccount: 'Još nema računa',
    resetToPart: 'Vrati na dio',
    reset: 'Doprinos je vraćen na dio.',
    noDelete: 'Doprinos nestaje kada se njegov račun ili rata kredita više ne dijeli.',
    received: 'Primljeno',
    differs: (receivedCents: number, partCents: number) =>
      `Primljeno ${formatCents(receivedCents)}, dio je sada ${formatCents(partCents)} (razlika ${formatCents(partCents - receivedCents)}).`,
    group: (count: number) =>
      `${count} ${plural(count, { one: 'doprinos', few: 'doprinosa', other: 'doprinosa' })}`,
    groupReceived: (name: string) => `Primljeno: svi doprinosi sudionika ${name}`,
    groupPartly: (received: number, count: number) => `${received} od ${count} primljeno`,
    groupNeedsAccount: (count: number) =>
      `${count} ${plural(count, {
        one: 'doprinos još nema račun; označite ga zasebno da odaberete račun.',
        few: 'doprinosa još nemaju račun; označite ih zasebno da odaberete račun.',
        other: 'doprinosa još nema račun; označite ih zasebno da odaberete račun.',
      })}`,
    showSingle: (count: number) => `Prikaži ${count}`,
    hideSingle: 'Sakrij',
    expectedOn: (date: string) => `Očekivano ${date}`,
    receivedOn: (date: string) => `Primljeno ${date}`,
    amount: 'Iznos',
    expectedDate: 'Očekivano',
    receivedDate: 'Primljeno',
    account: 'Uplaćeno na račun',
    amountInvalid: 'Unesite iznos veći od nule.',
    accountRequired: 'Odaberite račun na koji se uplaćuje doprinos.',
    receivedChanges: (count: number) =>
      count === 1
        ? 'Primljeni doprinos se time ne mijenja; razliku riješite sa sudionikom.'
        : `${count} ${plural(count, {
            one: 'primljeni doprinos se time ne mijenja',
            few: 'primljena doprinosa se time ne mijenjaju',
            other: 'primljenih doprinosa se time ne mijenja',
          })}; razlike riješite sa sudionikom.`,
  },
  screen: {
    title: 'Zajednički troškovi',
    description: 'Što svaki sudionik dijeli, njihovi dijelovi i doprinosi koji još nisu primljeni.',
    participants: 'Sudionici',
    addParticipant: 'Dodaj sudionika',
    editParticipant: 'Uredi sudionika',
    noParticipants: 'Još nema sudionika. Dodajte jednog za dijeljenje računa i kredita.',
    showArchived: 'Prikaži arhivirane',
    archived: 'Arhiviran',
    archive: 'Arhiviraj',
    restore: 'Vrati',
    delete: 'Obriši',
    deleteConfirm: (name: string) => `Obrisati ${name}?`,
    deleteDescription: 'Sudionik ne dijeli nijedan trošak; njegov uplatitelj i doprinosi ostaju.',
    edit: 'Uredi',
    saved: 'Sudionik je spremljen.',
    costs: 'Udjeli',
    noCosts: 'Ne dijeli nijedan ponavljajući račun ni kredit.',
    recurringBill: 'Ponavljajući račun',
    loan: 'Kredit',
    monthParts: 'Dijelovi ovaj mjesec',
    monthReceived: 'Primljeno ovaj mjesec',
    outstanding: 'Nenaplaćeno',
    outstandingList: 'Nenaplaćeni doprinosi',
    nothingOutstanding: 'Ništa nije nenaplaćeno.',
    monthContributions: 'Doprinosi ovaj mjesec',
    noMonthContributions: 'Ovaj mjesec nema doprinosa.',
    overdue: 'Dospjelo',
    tickReceived: 'Primljeno',
    expected: 'Očekivano',
    sharedCount: (count: number) =>
      `${count} ${plural(count, { one: 'zajednički trošak', few: 'zajednička troška', other: 'zajedničkih troškova' })}`,
    receivedOf: (received: string, parts: string) =>
      `${received} od ${parts} primljeno ovaj mjesec`,
    receivedAndOutstanding: 'Primljeno i nenaplaćeno po sudioniku',
    name: 'Ime',
    description_: 'Opis',
    save: 'Spremi',
    cancel: 'Odustani',
  },
  report: {
    title: 'Zajednički troškovi',
    total: 'Zajednički računi i rate kredita',
    participantsPart: 'Dijelovi sudionika',
    ownPart: 'Vaš dio',
    received: 'Primljeni doprinosi',
    outstanding: 'Nenaplaćeni doprinosi',
  },
  errors: {
    nameRequired: 'Ime je obavezno.',
    nameTooLong: 'Ime može imati najviše 50 znakova.',
    descriptionTooLong: 'Opis može imati najviše 200 znakova.',
    participantSharesInvalid: 'Sudionik plaća barem jedan udio i manje od svih.',
  },
}
