import type { UserRole } from '@/api/users'
import { localized } from '@/i18n/texts'
import { loginTextsHr } from './texts.hr'

/** Sign-in, setup, password and user texts, in English and Croatian (login NFR-7). */
const en = {
  appName: 'Monthwise',
  signIn: {
    title: 'Sign in',
    description: 'Sign in with your Monthwise username and password.',
    username: 'Username',
    password: 'Password',
    staySignedIn: 'Stay signed in',
    staySignedInHint: 'For 30 days on this device. Leave it off on a shared device.',
    submit: 'Sign in',
    haveSetupCode: 'Have a setup code?',
    forgotPassword: 'Forgot your password? A household admin can give you a temporary one.',
  },
  setup: {
    title: 'Set up Monthwise',
    description:
      'Create the first household admin. The setup code was shown when Monthwise was installed; see the user manual, chapter Sign in and users.',
    recoveryDescription:
      'With a new setup code you can choose a new password for a household admin, or add a new household admin. Household data is not changed.',
    setupCode: 'Setup code',
    displayName: 'Display name',
    displayNameHint: 'Shown in the menu and in the data and backups history.',
    username: 'Username',
    usernameHint: '3 to 50 characters: letters, digits, dot, dash or underscore.',
    password: 'Password',
    passwordHint: 'At least 12 characters.',
    repeatPassword: 'Repeat the password',
    submit: 'Set up and sign in',
    backToSignIn: 'Back to sign in',
  },
  password: {
    title: 'Change password',
    forcedTitle: 'Choose a new password',
    forcedDescription:
      'You signed in with a temporary password. Choose your own password to continue.',
    description: 'Changing the password signs you out on your other devices.',
    current: 'Current password',
    currentTemporary: 'Temporary password',
    new: 'New password',
    repeat: 'Repeat the new password',
    submit: 'Change password',
    changed: 'Password changed.',
  },
  account: {
    title: 'Signed in',
    signedInAs: (name: string) => `Signed in as ${name}`,
    signOut: 'Sign out',
    changePassword: 'Change password',
  },
  users: {
    section: 'Users',
    description: 'Who can sign in, and who manages users and backups.',
    count: (count: number) => `${count} ${count === 1 ? 'user' : 'users'}`,
    add: 'Add user',
    edit: 'Edit user',
    empty: 'No users.',
    you: 'you',
    inactive: 'Inactive',
    temporaryPassword: 'Temporary password',
    temporaryPasswordHint:
      'At least 12 characters. Give it to the user in person; they choose their own at the first sign-in.',
    mustChange: 'Must choose a password',
    active: 'Active',
    activeHint: 'An inactive user cannot sign in.',
    role: 'Role',
    roles: {
      HouseholdAdmin: 'Household admin',
      Member: 'Member',
    } satisfies Record<UserRole, string>,
    roleHints: {
      HouseholdAdmin: 'Manages users and exports and imports data.',
      Member: 'Sees and changes household data.',
    } satisfies Record<UserRole, string>,
    memberNotice: 'Only a household admin can add and change users.',
    resetPassword: 'Set temporary password',
    resetTitle: (name: string) => `Temporary password for ${name}`,
    resetDescription:
      'The user is signed out everywhere and must choose a new password at the next sign-in.',
    resetDone: 'Temporary password set.',
    remove: 'Delete',
    removeTitle: (name: string) => `Delete ${name}?`,
    removeDescription: 'The user can no longer sign in. Household data is not changed.',
    removed: 'User deleted.',
    saved: 'Saved.',
    save: 'Save',
    cancel: 'Cancel',
  },
  errors: {
    required: 'Required.',
    passwordTooShort: 'At least 12 characters.',
    passwordsDiffer: 'The passwords are not the same.',
    users: 'Users could not be loaded.',
  },
} as const

export type LoginTextsEn = typeof en

export const loginTexts = localized(en, loginTextsHr)
