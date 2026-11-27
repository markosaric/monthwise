import { plural, type Translation } from '@/i18n/texts'
import type { LoginTextsEn } from './texts'

/** Croatian sign-in, setup, password and user texts (login NFR-7). */
export const loginTextsHr: Translation<LoginTextsEn> = {
  appName: 'Monthwise',
  signIn: {
    title: 'Prijava',
    description: 'Prijavite se svojim korisničkim imenom i lozinkom za Monthwise.',
    username: 'Korisničko ime',
    password: 'Lozinka',
    staySignedIn: 'Ostani prijavljen',
    staySignedInHint: 'Na 30 dana na ovom uređaju. Ne uključujte na zajedničkom uređaju.',
    submit: 'Prijava',
    haveSetupCode: 'Imate kod za postavljanje?',
    forgotPassword:
      'Zaboravili ste lozinku? Administrator kućanstva može vam dati privremenu lozinku.',
  },
  setup: {
    title: 'Postavljanje Monthwisea',
    description:
      'Izradite prvog administratora kućanstva. Kod za postavljanje prikazan je pri instalaciji Monthwisea; pogledajte korisnički priručnik, poglavlje Prijava i korisnici.',
    recoveryDescription:
      'S novim kodom za postavljanje možete odabrati novu lozinku za administratora kućanstva ili dodati novog administratora kućanstva. Podaci kućanstva se ne mijenjaju.',
    setupCode: 'Kod za postavljanje',
    displayName: 'Ime za prikaz',
    displayNameHint: 'Prikazuje se u izborniku i u povijesti podataka i sigurnosnih kopija.',
    username: 'Korisničko ime',
    usernameHint: 'Od 3 do 50 znakova: slova, brojke, točka, crtica ili podvlaka.',
    password: 'Lozinka',
    passwordHint: 'Najmanje 12 znakova.',
    repeatPassword: 'Ponovite lozinku',
    submit: 'Postavi i prijavi se',
    backToSignIn: 'Natrag na prijavu',
  },
  password: {
    title: 'Promjena lozinke',
    forcedTitle: 'Odaberite novu lozinku',
    forcedDescription:
      'Prijavili ste se privremenom lozinkom. Za nastavak odaberite svoju lozinku.',
    description: 'Promjenom lozinke odjavljujete se na svojim drugim uređajima.',
    current: 'Trenutna lozinka',
    currentTemporary: 'Privremena lozinka',
    new: 'Nova lozinka',
    repeat: 'Ponovite novu lozinku',
    submit: 'Promijeni lozinku',
    changed: 'Lozinka je promijenjena.',
  },
  account: {
    title: 'Prijavljeni',
    signedInAs: (name: string) => `Prijavljeni ste kao ${name}`,
    signOut: 'Odjava',
    changePassword: 'Promijeni lozinku',
  },
  users: {
    section: 'Korisnici',
    description: 'Tko se može prijaviti i tko upravlja korisnicima i sigurnosnim kopijama.',
    count: (count: number) =>
      `${count} ${plural(count, { one: 'korisnik', few: 'korisnika', other: 'korisnika' })}`,
    add: 'Dodaj korisnika',
    edit: 'Uredi korisnika',
    empty: 'Nema korisnika.',
    you: 'vi',
    inactive: 'Neaktivan',
    temporaryPassword: 'Privremena lozinka',
    temporaryPasswordHint:
      'Najmanje 12 znakova. Dajte je korisniku osobno; svoju lozinku odabire pri prvoj prijavi.',
    mustChange: 'Mora odabrati lozinku',
    active: 'Aktivan',
    activeHint: 'Neaktivan korisnik se ne može prijaviti.',
    role: 'Uloga',
    roles: {
      HouseholdAdmin: 'Administrator kućanstva',
      Member: 'Član',
    },
    roleHints: {
      HouseholdAdmin: 'Upravlja korisnicima te izvozi i uvozi podatke.',
      Member: 'Vidi i mijenja podatke kućanstva.',
    },
    memberNotice: 'Samo administrator kućanstva može dodavati i mijenjati korisnike.',
    resetPassword: 'Postavi privremenu lozinku',
    resetTitle: (name: string) => `Privremena lozinka za korisnika ${name}`,
    resetDescription:
      'Korisnik se odjavljuje posvuda i pri sljedećoj prijavi mora odabrati novu lozinku.',
    resetDone: 'Privremena lozinka je postavljena.',
    remove: 'Obriši',
    removeTitle: (name: string) => `Obrisati korisnika ${name}?`,
    removeDescription: 'Korisnik se više ne može prijaviti. Podaci kućanstva se ne mijenjaju.',
    removed: 'Korisnik je obrisan.',
    saved: 'Spremljeno.',
    save: 'Spremi',
    cancel: 'Odustani',
  },
  errors: {
    required: 'Obavezno.',
    passwordTooShort: 'Najmanje 12 znakova.',
    passwordsDiffer: 'Lozinke nisu iste.',
    users: 'Korisnici se nisu mogli učitati.',
  },
}
