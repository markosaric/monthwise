import { plural, type Translation } from '@/i18n/texts'
import { maxImportFileMegabytes, minPasswordLength } from './limits'
import type { DataTextsEn } from './texts'

/** Croatian texts of the Data and backups section. */
export const dataTextsHr: Translation<DataTextsEn> = {
  section: 'Podaci i sigurnosne kopije',
  description: 'Izvezite sve podatke u datoteku i ponovno ih uvezite.',
  group: 'Podaci',
  noExport: 'Još nema izvoza',
  lastExport: 'Zadnji izvoz',
  lastImport: 'Zadnji uvoz',
  never: 'Nikad',
  dataFrom: 'podaci od',
  keepSafe:
    'Datoteka sadrži vaše osobne financijske podatke. Čuvajte je na sigurnom mjestu, a njezinu lozinku u upravitelju lozinki: datoteku čija je lozinka izgubljena nitko ne može uvesti.',
  readOnly: 'Samo administrator kućanstva može izvoziti i uvoziti podatke.',
  export: {
    action: 'Izvezi podatke',
    title: 'Izvoz svih podataka',
    description:
      'Odaberite lozinku za datoteku. Trebat će vam za ponovni uvoz datoteke; Monthwise je ne čuva.',
    password: 'Lozinka',
    repeat: 'Ponovite lozinku',
    hint: `Najmanje ${minPasswordLength} znakova. Fraza od nekoliko nepovezanih riječi lako se pamti, a teško pogađa.`,
    missing: (count: number) =>
      `Potrebno je još ${count} ${plural(count, { one: 'znak', few: 'znaka', other: 'znakova' })}`,
    longEnough: 'Dovoljno dugo',
    tooShort: `Lozinka mora imati najmanje ${minPasswordLength} znakova.`,
    mismatch: 'Lozinke nisu iste.',
    confirm: 'Izvezi',
    done: (fileName: string) => `Izvezeno u ${fileName}`,
  },
  import: {
    action: 'Uvezi podatke',
    title: 'Uvoz podataka',
    description:
      'Odaberite datoteku izvoza i unesite njezinu lozinku. Ništa se ne mijenja dok ne potvrdite.',
    file: 'Datoteka izvoza',
    password: 'Lozinka',
    check: 'Provjeri datoteku',
    compareTitle: 'Zamijeniti sve podatke?',
    replaceWarning:
      'Uvoz zamjenjuje sve podatke u Monthwiseu podacima iz datoteke. Prije toga se izrađuje sigurnosna kopija trenutačnih podataka.',
    made: (date: string, release: string) => `Datoteku je ${date} izradio Monthwise ${release}.`,
    inFile: 'U datoteci',
    now: 'Sada u Monthwiseu',
    typeWord: (word: string) => `Upišite ${word} za potvrdu`,
    confirm: 'Zamijeni sve podatke',
    done: 'Podaci su uvezeni.',
    tooLarge: `Ova je datoteka prevelika da bi bila datoteka izvoza Monthwisea (više od ${maxImportFileMegabytes} MB).`,
  },
  safetyCopy: {
    title: 'Sigurnosna kopija',
    made: (date: string) => `Podaci kakvi su bili prije zadnjeg uvoza, od ${date}.`,
    none: 'Sigurnosna kopija izrađuje se sama prije svakog uvoza.',
    action: 'Vrati',
    confirmTitle: 'Vratiti sigurnosnu kopiju?',
    confirmDescription:
      'Ovo zamjenjuje sve podatke podacima od prije zadnjeg uvoza. Trenutačni podaci najprije dobivaju vlastitu sigurnosnu kopiju.',
    done: 'Sigurnosna kopija je vraćena.',
  },
  reminder: {
    title: 'Podsjetnik na izvoz',
    description: 'Prikaži podsjetnik na Pregledu kada izvoz nije napravljen neko vrijeme.',
    enabled: 'Podsjeti me',
    interval: 'Nakon',
    days: (days: number) => `${days} ${plural(days, { one: 'dan', few: 'dana', other: 'dana' })}`,
    saved: 'Podsjetnik je spremljen.',
    dashboard: (lastExport: string | null) =>
      lastExport ? `Vaš zadnji izvoz bio je ${lastExport}.` : 'Još niste izvezli svoje podatke.',
    dashboardTitle: 'Vrijeme je za izvoz podataka',
    open: 'Izvezi sada',
    dismiss: 'Zatvori',
  },
  history: {
    title: 'Povijest',
    empty: 'Još ništa nije izvezeno ni uvezeno.',
    failed: 'Neuspjelo',
    kinds: {
      Export: 'Izvoz',
      Import: 'Uvoz',
      SafetyCopy: 'Izrađena sigurnosna kopija',
      SafetyCopyRestore: 'Vraćena sigurnosna kopija',
    },
    byUser: (name: string) => `korisnik ${name}`,
    newer: 'Novije',
    older: 'Starije',
  },
  summary: {
    cards: 'Kartice i računi',
    loans: 'Krediti',
    inputs: 'Uplate',
    cardPurchases: 'Kupnje karticama',
    bills: 'Računi',
    expenses: 'Troškovi',
    lendings: 'Posudbe',
  },
  cancel: 'Odustani',
  confirmationWord: 'ZAMIJENI',
}
