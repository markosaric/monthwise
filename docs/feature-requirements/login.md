# Requirements: Login

- **Branch:** `feat/login`
- **Status:** Approved (2026-11-27)
- **Date:** 2026-11-27

> This document describes **what** the feature must do, not **how** it is built.
> The implementation plan (`docs/plans/feat-login.md`) is written after these
> requirements are approved.

## 1. Goal

Today anyone who can reach Monthwise sees and changes all household data. On the user's own
computer that is limited to the same computer by default, but on Azure Monthwise is on the public
internet, and on the home network or through Tailscale every device that can connect is let in.

[production-packaging.md](production-packaging.md) FR-11 and
[backup-and-restore.md](backup-and-restore.md) already assume a login: every screen and every API
call needs a signed-in user of the household, and only a **household admin** may export and import.

This feature adds **user accounts of the household with a username and a password**, kept by
Monthwise itself. It works the same on every target (local development, Docker one-click, native
background service and Azure) and needs no internet connection and no outside sign-in service.
Passkeys are a later feature (see Out of scope).

## 2. Terms

| Term | Meaning |
|---|---|
| User | A person of the household who can sign in to Monthwise. Not the same as a payer or a borrower, who are people in the household's records. |
| Username | The name a user signs in with. Unique within the installation. |
| Household admin | A user who may manage users (FR-6) and export and import data ([backup-and-restore.md](backup-and-restore.md)). |
| Member | A user who is not a household admin: sees and changes household data, but does not manage users and does not export or import. |
| Session | The time a user stays signed in on one browser, after signing in. |
| Setup code | A one-time code that only the person who installed Monthwise can see, needed to create the first household admin (FR-1). |
| Temporary password | A password a household admin sets for another user, which that user must change at the next sign-in (FR-7). |
| Installation, Deployment target | As in [production-packaging.md](production-packaging.md). |

## 3. User attributes

| # | Attribute | Required | Description and rules |
|---|---|---|---|
| U1 | Display name | Yes | Shown in the menu and in the Data and backups history ("who did it"). Maximum 100 characters. |
| U2 | Username | Yes | 3 to 50 characters: letters, digits, dot, dash and underscore. Unique within the installation, not case-sensitive ("Ana" and "ana" are the same user). |
| U3 | Password | Yes | At least 12 characters, at most 128. Any characters, including spaces. Never shown or stored readable. |
| U4 | Role | Yes | Household admin or Member. |
| U5 | Active | Yes | An inactive user cannot sign in. Default: active. |

## 4. Functional requirements

### 4.1 First start

**FR-1 Create the first household admin**
- While an installation has no users, every screen shows only a **Set up Monthwise** form: display
  name, username, password (entered twice) and the setup code.
- The setup code is made when the installation first starts without users. Only the person who
  installed Monthwise can read it: the start or install command shows it on the user's own
  computer, and on Azure it is in the installation's secret store. It never appears on a screen
  of Monthwise itself and never in a response to someone who has not entered it.
- With the right setup code the form creates the first user as household admin and signs them in.
  The code then stops working.
- A wrong setup code is refused, and repeated wrong codes are slowed down as in FR-3.
- Existing installations that are updated to this release start with no users and show the same
  form; all their data stays as it was.

### 4.2 Signing in and out

**FR-2 Sign in**
- Every screen except the sign-in screen and the setup form (FR-1) needs a signed-in user. Someone
  who is not signed in sees the sign-in screen and, after signing in, the screen they asked for.
- Every API call except the health address, signing in and the setup form answers "not signed
  in" when no user is signed in, and shows no household data.
- The sign-in screen asks for username and password and has a **Stay signed in** choice.
- A wrong username, a wrong password and an inactive user all get the same message, so the
  screen does not tell whether a username exists.

**FR-3 Protection against guessing**
- After 5 wrong passwords in a row for one username, that username cannot sign in for 15 minutes,
  even with the right password. The screen says it is locked for a while, not why.
- Many sign-in attempts from one place in a short time are slowed down or refused, whatever the
  username.
- A successful sign-in resets the count of wrong passwords.

**FR-4 Staying signed in**
- Without **Stay signed in**, the session ends when the browser is closed, or after 12 hours.
- With **Stay signed in**, the session lasts 30 days from the last use.
- A session survives a restart and an update of Monthwise, and on Azure a start after scaling to
  zero: users are not signed out by these.
- Each browser and device has its own session; signing in on the phone does not sign out the
  computer.

**FR-5 Sign out**
- The menu shows the signed-in user's display name and a **Sign out** action.
- Signing out ends the session on that browser and shows the sign-in screen.

### 4.3 Users

**FR-6 Manage users**
- Settings gets a **Users** tile in the Accounts group (see [design-refresh.md](design-refresh.md)
  FR-20), with a count of users.
- A household admin can list, add and edit users (display name, username, role, active) and
  delete them. Members see their own user only (FR-8).
- There is always at least one active household admin: making the last active household admin a
  member, inactive or deleted is refused with a clear message.
- Deleting a user, making them inactive or changing their role takes effect on their next screen or
  API call, also in sessions that are already open.
- The Data and backups history keeps the display name of a deleted user.

**FR-7 Passwords set by an admin**
- When adding a user, the household admin enters a temporary password and gives it to that user
  in person.
- A household admin can reset another user's password to a new temporary password. This ends all
  of that user's sessions.
- A user signing in with a temporary password must choose a new password before anything else.

**FR-8 Own password**
- Every user can change their own password, by entering the current one and the new one twice.
- Changing the password ends that user's other sessions; the current one stays signed in.

**FR-9 Forgotten password**
- A user who forgot their password asks a household admin for a temporary password (FR-7).
- If no household admin can sign in, the person who installed Monthwise can make a new setup code
  with one documented command on the installation (on the user's own computer and on Azure). With
  it, the **Set up Monthwise** form (FR-1) resets an existing household admin's password, or
  creates a new household admin. Household data is not changed.

### 4.4 Other features

**FR-10 Data and backups**
- Only household admins see and use **Export data** and **Import data**, as in
  [backup-and-restore.md](backup-and-restore.md) FR-1; members see the history only.
- The history records the signed-in user's display name as "who did it".
- Users, passwords and sessions are never part of an export file, and an import does not change
  them ([backup-and-restore.md](backup-and-restore.md) FR-8).

**FR-11 Local development and tests**
- Local development needs a sign-in like every other target; the developer creates their user
  with the setup form (FR-1) or with a documented developer command.
- The release check in CI keeps using only the health address and needs no user.

## 5. Non-functional requirements

- **NFR-1 Same on every target:** the login behaves the same on local development, Docker
  one-click, native background service and Azure, and works without an internet connection.
- **NFR-2 Passwords:** passwords are stored only as a slow, salted one-way hash suited to
  passwords; a password can never be read back, not even by a household admin or from the
  database.
- **NFR-3 Secrets and logs:** passwords, temporary passwords, setup codes and session values never
  appear in logs, error messages, screens other than where they are typed, or the repository.
  Sign-ins, failed sign-ins and lockouts are logged with the username and time only.
- **NFR-4 Session safety:** a session cannot be read by the page's scripts or sent by another web
  site on the user's behalf; on Azure it is sent only over HTTPS. Changing data needs a request
  that only Monthwise's own screens can make.
- **NFR-5 Access from other devices:** sign-in works from the phone on the home network and through
  Tailscale when that setting is turned on ([production-packaging.md](production-packaging.md)
  FR-11), also over plain HTTP on the home network.
- **NFR-6 Responsive:** the sign-in screen, the setup form, the password change and the Users
  screens work on phone and desktop.
- **NFR-7 Language:** every new text is in English and Croatian (see
  [croatian-language.md](croatian-language.md) FR-9). Croatian terms: *Prijava* (Sign in),
  *Odjava* (Sign out), *Korisnici* (Users), *korisničko ime* (username), *lozinka* (password),
  *privremena lozinka* (temporary password), *administrator kućanstva* (household admin), *član*
  (member), *ostani prijavljen* (stay signed in), *postavljanje* (setup), *kod za postavljanje*
  (setup code).
- **NFR-8 User manual:** the user manual (English and Croatian) gets a short Sign in and users
  chapter, including the first start with the setup code on each target and the forgotten-password
  command.

## 6. Out of scope

- Passkeys (Face ID, Touch ID, Windows Hello), planned as a later feature.
- Signing in with Microsoft, Google or another outside service, and Azure's own sign-in in front of
  the app.
- Two-step verification with codes or an authenticator app.
- Email: no sign-up, no password-reset email, no email addresses for users.
- Users signing themselves up; only a household admin adds users.
- Several households in one installation, and permissions finer than household admin and member.
- A list of a user's open sessions with the option to end single ones.
- Per-user settings, for example the language (the language setting stays as it is).

## 7. Acceptance criteria

1. A new installation shows only the **Set up Monthwise** form; with the setup code from the
   install or start command (or from the Azure secret store) it creates the first household admin,
   and after that the code no longer works.
2. An existing installation updated to this release shows the setup form, and after setup all its
   data is unchanged.
3. Without signing in, no screen shows household data and every API call except the health
   address, sign-in and setup answers "not signed in".
4. A wrong username or password gets one and the same message; after 5 wrong passwords the
   username is locked for 15 minutes.
5. With **Stay signed in**, the user is still signed in after closing the browser and after a
   restart or update of Monthwise (and on Azure after scaling to zero); without it, closing the
   browser signs them out.
6. **Sign out** ends the session on that browser.
7. A household admin can add, edit, deactivate and delete users and reset their passwords; the last
   active household admin cannot be removed, made a member or made inactive.
8. A user with a temporary password must set a new one before using Monthwise.
9. A deactivated or deleted user is signed out on their next screen or API call.
10. Members do not see Users management, Export data or Import data; household admins do.
11. The Data and backups history shows the display name of the user who exported or imported.
12. If no household admin can sign in, the documented command makes a new setup code that lets the
    installer reset an admin's password; household data is unchanged.
13. Passwords, setup codes and session values appear in no log and no error message.
14. Sign-in works from a phone on the home network over HTTP when access from other devices is
    turned on, and on Azure over HTTPS.
15. All new texts appear in English and Croatian, and the user manual has a Sign in and users
    chapter in both languages.
