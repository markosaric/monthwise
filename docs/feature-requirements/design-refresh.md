# Requirements: Design refresh

- **Branch:** `feat/design_refresh`
- **Status:** Approved (2026-10-18)
- **Date:** 2026-10-18

> This document describes **what** the design must look like and do, not **how** it is built.
> The implementation plan (`docs/plans/feat-design_refresh.md`) is written after these
> requirements are approved.

## 1. Goal

Monthwise works, but its screens are mostly grey text, tables and badges, so it is hard to see the
important numbers of a month at a glance. This document collects the design changes that make the
app easier to read and nicer to use. It covers the **general design** (section 3.1), the **left menu** (section 3.2),
the **Dashboard** (section 3.3), the **Cards** screen (section 3.4), **quick actions** (section
3.5) and the **Month**, **Cash flow**, **Loans**, **Shared costs** and **Settings** screens
(sections 3.6 to 3.10); later design changes for
single screens are added here as new sections before approval.

The user's reference picture (an admin dashboard template) is a **style reference only**: it
shows the kind of look wanted, not a layout to copy screen by screen. The traits taken from it are:

- white cards with soft shadows on a light grey page background, with plenty of space between them;
- small stat cards: a small coloured icon, one large bold number, a short grey label under it, and
  a small coloured pill with the change against last period (green going up, red going down);
- one strong accent colour (teal in the picture) for highlights, buttons and the main chart, plus
  a few solid colours (blue, green, amber, red, pink) for icons;
- one highlight card filled with the accent colour for the most important message;
- small cards with a number and a sparkline (a tiny area chart without axes);
- a large overview card: a big headline number with its label, a short comparison with last month
  (green up arrow, red down arrow) and a bar chart next to it;
- bold, larger type for numbers and card titles; small, light type for labels.

## 2. Terms

| Term | Meaning |
|---|---|
| Left menu | The main navigation on the left side of the screen on desktop and tablet (today the sidebar with Dashboard, Month, Cash flow, …). |
| Bottom navigation | The main navigation at the bottom of the screen on phones. |
| Expanded menu | The left menu showing the app name, icons and labels. |
| Collapsed menu | The left menu showing only the icons, so the screen content gets more width. |
| Menu toggle | The one button that switches the left menu between expanded and collapsed. |
| Section colour | The colour that belongs to one main section of the app (e.g. Cards), used for its menu icon and as its accent colour on its screens. |
| Key figure | One of the few most important numbers of a screen, e.g. the month's balance or total outputs. |
| System status | Whether the app can reach the API and whether the API reports the database as healthy. Shown today as the **System status** card on the Dashboard. |
| Healthy | The API is reachable and reports the database as **Healthy**. Anything else (API unreachable, database unhealthy or unknown) is a **problem**. |

## 3. Functional requirements

### 3.1 General design

**FR-1 More colour**
- Every main section has its own section colour, used for its icon in the left menu and the bottom
  navigation and as its accent on its own screens (e.g. headings, key figure cards, chart series).
- Colours carry meaning consistently across the whole app:
  - inputs / income: green,
  - outputs / bills / expenses: red or orange,
  - planned or expected (not yet paid or received): a lighter tint of the same colour, or amber,
  - overdue or a problem: red,
  - neutral information: the section colour or grey.
- The app has one main accent colour (teal, as in the reference picture) for highlights, primary
  buttons and the main chart of a screen.
- Screens show content in white cards on a light grey page background. A screen may have one
  highlight card filled with the accent colour for its most important message (e.g. "All bills of
  October are paid"). Body text stays dark on light and easy to read.

**FR-2 Less detail**
- Each overview screen (Dashboard, Month, Cash flow, Cards, Loans, Shared costs, Savings) opens with
  its **key figures** (at most 4), followed by charts and only then by detailed lists and tables.
- A key figure is shown as a small stat card: a coloured icon, the amount in large bold type, a
  short label under it, and, where the app has the previous month's value, a small pill with the
  change in percent against the previous month (green when it is better, red when it is worse).
- Secondary details (descriptions, notes, technical values such as IDs or dates of last change) are
  hidden from overview lists and shown only on the details or edit screen, or when the user expands
  a row.
- Helper text under headings and fields is shortened or removed where the screen is clear without
  it.

**FR-3 More charts**
- Wherever a screen shows amounts that are easier to compare as a picture, it shows a chart next to
  or above the numbers. At least:
  - **Dashboard:** inputs versus outputs of recent months, and this month's outputs by kind
    (FR-10).
  - **Month:** outputs of the month by kind, and paid versus still open (FR-15).
  - **Cash flow:** the existing cash flow chart, in the new colours.
  - **Cards:** current versus expected end-of-month balance per account, and installments still to
    pay by month (FR-12).
  - **Loans:** paid versus remaining principal per loan.
  - **Shared costs:** received versus outstanding contributions per participant.
- Where a key figure has a history (e.g. the balance of recent months), its card may show it as a
  sparkline: a small area chart without axes.
- Charts use the meaning colours of FR-1, have a legend or labels that name each colour, and show
  the exact amount when the user points at or taps a part of the chart.
- A chart never replaces the numbers: the exact amounts stay available on the same screen.

**FR-4 Fonts for important topics**
- Headings, key figures and section titles use a separate display font that differs from the body
  text font, so the most important topics stand out.
- Key figures use large, bold numbers with digits of equal width, so amounts line up.
- Body text, tables and forms keep a calm, easy-to-read font.
- At most two font families are used in the whole app.

**FR-5 Consistent look**
- The new colours, fonts and card style apply to all screens, including forms, settings and
  details screens, so the app looks like one product.
- Light mode is required. A dark mode is not part of this document (see Out of scope).

### 3.2 Left menu

The drawing of the suggested left menu is in
[design-refresh-mockup.html](design-refresh-mockup.html) (open it in a browser).

**FR-6 Collapsible left menu**
- On desktop and tablet the left menu has one **menu toggle** button. Pressing it collapses the
  expanded menu; pressing the same button again expands it.
- **Expanded menu** shows the app name, and for every section its coloured icon and label.
- **Collapsed menu** shows only the coloured icons (and the app logo instead of the name). Pointing
  at an icon shows the section's label as a tooltip.
- The current section is highlighted in both states, using its section colour.
- The screen content takes the width freed by collapsing the menu.
- The first time, the menu opens expanded. The app remembers the user's choice on that device and opens with the menu in the same state
  next time.
- The toggle can be used with the keyboard and tells screen readers whether the menu is expanded or
  collapsed.

**FR-7 Coloured menu icons**
- The menu keeps its current icons and sections, and each icon is shown in its section colour
  (FR-1), for example:

| Section | Colour |
|---|---|
| Dashboard | Indigo |
| Month | Blue |
| Cash flow | Teal |
| Cards | Violet |
| Loans | Orange |
| Shared costs | Pink |
| Savings | Green |
| Settings | Grey |

- The bottom navigation on phones uses the same coloured icons.

**FR-8 System status in the menu**
- The **System status** card is removed from the Dashboard.
- System status is shown at the bottom of the left menu **only when there is a problem**: the API
  is unreachable, or the database is not healthy or its state is unknown. When everything is
  healthy, nothing about system status is shown anywhere.
- The status shows what is wrong in short words, e.g. "API unreachable" or "Database unhealthy", in
  the problem colour (red) with a warning icon.
- In the collapsed menu it is shown as a red warning icon; pointing at it shows what is wrong.
- While the app is still checking the status after it opens, nothing is shown; a problem appears
  only once it is known.
- The status is checked again regularly, so a problem appears without reloading the page and
  disappears on its own when the API and database are healthy again.
- On phones, where there is no left menu, a problem is shown as a slim red bar above the bottom
  navigation with the same text, and nothing is shown when everything is healthy.

### 3.3 Dashboard

The drawing of the suggested Dashboard is in
[design-refresh-mockup.html](design-refresh-mockup.html) (open it in a browser; the
Dashboard is the first screen).

The Dashboard answers one question: **how is this month going?** It shows the month's inputs and
outputs, what is still to pay and what is coming up, and nothing about account balances.

**FR-9 What leaves the Dashboard**
- The **Cards** widget is removed from the Dashboard completely, with everything it shows today:
  the list of cards and accounts, their status, the **current balance**, the **expected balance**
  at the end of the month and the hint to add a **starting balance**. This information moves to
  the Cards screen (FR-12). No other place on the Dashboard shows account balances, expected
  end-of-month balances or starting balances.
- The **System status** card is removed (FR-8).
- The placeholder text under the Dashboard heading is removed.

**FR-10 What the Dashboard shows**

The Dashboard always shows the **current month** and shows, from top to bottom:

1. **Quick actions:** the row of add buttons of FR-14.
2. **Key figures:** four stat cards (FR-2), each opening the screen it comes from when selected:

| # | Key figure | Value | Change pill against last month | Opens |
|---|---|---|---|---|
| K1 | Inputs | All inputs of the month, received and expected. Under it: how much is already received. | Yes, green when higher | Month |
| K2 | Outputs | All outputs of the month (bills, expenses, loan payments, card payments), paid and expected. Under it: how much is already paid. | Yes, green when lower | Month |
| K3 | Month result | K1 minus K2. Negative is shown in red. | Yes, green when better | Cash flow report of the month |
| K4 | Still to pay | Expected outputs of the month that are not paid yet, with how many there are. Red when any is overdue. | No | Month |

3. **Highlight card** (teal, FR-1): one short message about the month, chosen in this order:
   - if anything is overdue: how many bills or payments are overdue and their total, in red
     instead of teal;
   - else if outputs are still to pay: the next due one, e.g. "Next: Electricity €64.20 on 22 Oct";
   - else: "All bills and payments of October are paid".
4. **Overview card:** the month result (K3) as a big number, a short comparison with last month
   (green up arrow or red down arrow, with the difference in percent for inputs and outputs), and a
   bar chart of inputs versus outputs for the last 6 months. Selecting it opens the Cash flow
   screen.
5. **Two cards side by side:**
   - **Coming up:** the bills, loan payments, card payments and expected inputs due in the next 7
     days, at most 5, each with name, due date and amount (outputs red or orange, inputs green),
     and a link to the Month screen. When nothing is due: "Nothing due in the next 7 days".
   - **This month's outputs by kind:** a ring chart of the month's outputs split into bills,
     expenses, loan payments and card payments, with the amount of each in the legend.
6. **Bottom row of small cards** (as in the reference picture), each with a solid coloured icon in
   its section colour, a bold number and a short label, opening its screen:
   - **Loans:** total monthly payment of active loans, and the total still to repay (replaces the
     Loans widget; overdue loan payments make the card red).
   - **Card payments:** this month's card payments total, and the number of cards with
     installments.
   - **Shared costs:** outstanding contributions total, and the number of participants who owe
     something (red when any is overdue).
   - **Recurring bills:** number of active recurring bills, and how many of this month's bills from
     them are not issued yet (recurring bills have no amount of their own, so there is no monthly
     total).
   A small card is not shown when its section has no data (e.g. no loans).

**FR-11 Empty Dashboard**
- When the app has no data for the month yet, the key figures show €0.00, the highlight card says
  what to do first ("Add this month's inputs and bills") with a link to the Month screen, and the
  charts show an empty state instead of empty axes.

### 3.4 Cards screen

**FR-12 Accounts and balances on the Cards screen**
- The Cards screen gets a new first part, **Accounts**, which shows everything the Dashboard Cards
  widget showed before (FR-9), with the same rules: which cards and accounts are listed (valid ones,
  and blocked or expired ones only while they have ongoing transactions), primary accounts first and
  marked, card status, masked card number and/or IBAN, the current balance, the expected balance at
  the end of the month (dimmed or in another colour), a red negative balance, and the hint to add a
  starting balance where it is missing. Selecting an account opens its details.
- The empty states of the old widget (no cards yet, no valid cards) move with it.
- Above the list, the Cards screen shows its key figures (FR-2):
  - **Current balance** of all listed accounts together,
  - **Expected at end of month** of all listed accounts together,
  - **Card payments this month** (from the existing card payments),
  - **Installments left** to pay on all cards.
- A bar chart shows the current and the expected end-of-month balance per account side by side.
- The existing list of cards with installments follows below the accounts, unchanged in content.

### 3.5 Quick actions

**FR-14 Quick actions**
- The Dashboard starts, right under its heading and above the key figures, with a row of **quick
  action** buttons for the things the user records most often. They open the same forms as the
  buttons on the Month and Cards screens, for the current month:

| # | Button | Icon | Colour | Opens |
|---|---|---|---|---|
| Q1 | Add income | Arrow into a wallet / down arrow | Green | The new input form (today **Add input** on the Month screen) |
| Q2 | Add bill | Receipt | Orange | The new bill form (**Add bill**) |
| Q3 | Add expense | Shopping cart | Amber | The new expense form (**Add expense**) |
| Q4 | Cash withdrawal | Banknote | Teal | The cash withdrawal form (**Cash withdrawal**) |
| Q5 | Add purchase | Shopping bag | Violet (Cards colour) | The new card purchase form (**Add purchase** on the Cards screen) |

- Each button shows **only its icon** on a tinted square in its colour, without visible text. Its
  label (e.g. "Add bill") appears as a tooltip when the pointer rests on the icon for about one
  second, and disappears when the pointer leaves. The label also appears when the button gets
  keyboard focus, and screen readers always read it.
- **Add purchase** follows the same rule as on the Cards screen: it is shown only when at least one
  card can take a new purchase.
- After the form is saved or cancelled, the user returns to the screen the button was on (the
  Dashboard), and the Dashboard shows the new entry in its figures.
- On phones, where there is no hover, the buttons are a row of round coloured icons without text;
  a long press shows the label. The row scrolls sideways when it does not fit.
- The quick action row is only on the Dashboard. On the **Month** screen the add buttons stay in
  the section they belong to (FR-15), using the same icons and colours.

### 3.6 Month screen

**FR-15 Month screen redesign**
- **Header:** the month name as the heading with the previous and next month arrows right next to
  it, and **This month** when another month is shown.
- **Add buttons stay in their sections**, as today: **Add input** in Income; **Add bill** and
  **Add recurring bills** in Bills; **Add expense** and **Cash withdrawal** in Expenses. They sit in
  the section header and use the icons and colours of FR-14 (Add recurring bills: blue, repeat
  icon). There is no quick action row on the Month screen.
- **Key figures** (FR-2): Inputs (with how much is received), Outputs (with how much is paid),
  Month result, Still to pay (red when anything is overdue).
- **Progress:** two thin bars under the key figures: inputs received of expected (green) and outputs
  paid of total (orange), each with "x of y" next to it.
- **Sections:** Income, Bills, Expenses, Loan payments and Card payments stay in this order, each in
  its own card with its section icon and colour on the left of the title. The header shows the
  section total and "x of y paid" (or received). A section can be collapsed; a section whose items
  are all paid or received starts collapsed.
- **Rows:** each row shows only name, due date, amount and a status chip: **Paid**/**Received**
  (green), **Expected** (grey), **Overdue** (red). Marking an item paid or received is one tap on
  the row's check button. Account, category, payer, notes and shares are shown when the row is
  expanded, and on its edit screen.
- **Chart:** a ring chart of the month's outputs by kind (as on the Dashboard) next to the key
  figures on wide screens, below them on phones.

### 3.7 Cash flow screens

**FR-16 Cash flow screen redesign**
- **Key figures** for the chosen period: total inputs, total outputs, result, and average result per
  month.
- **Period** stays visible as a row of buttons (This month, Last 12 months, This year). The other filters (account,
  payer, input category, loan category, card, expense category) move behind one **Filters** button;
  active filters are shown as removable chips under it.
- **Chart:** the cash flow chart is the main element, full width: inputs green and outputs orange,
  the received or paid part solid and the expected part as a lighter tint of the same colour, and a
  line for the month result.
- **Months list:** under the chart, one row per month with inputs, outputs and the result (green
  when positive, red when negative), each opening that month's report.

**FR-17 Monthly cash flow report redesign**
- Key figures: inputs, outputs, month result, each with a change pill against the previous month.
- Two ring charts side by side: inputs by category and outputs by category.
- The grouped lists (by category, account, payer, loan, card) are collapsible cards, collapsed by
  default except **by category**.
- Starting balances and corrections stay at the end, collapsed by default.

### 3.8 Loans screen

**FR-18 Loans screen redesign**
- **Key figures:** number of active loans, total monthly payment, total still to repay, and the next
  payment (date and amount).
- **Active / Closed** stays as a two-button switch.
- **Loan cards:** each loan is a card in the Loans colour (orange) with name, category, monthly
  payment, next payment date, and a progress bar of payments made ("18 of 60 paid") with the
  amount still to repay. An overdue payment or a missing account makes the card red with a badge.
- **Chart:** paid versus remaining amount per loan as horizontal bars (FR-3).
- **Loan details:** the same progress bar at the top, then the list of payments with status chips (Paid,
  Expected, Overdue) instead of plain text.

### 3.9 Shared costs screen

**FR-19 Shared costs screen redesign**
- **Key figures:** outstanding contributions, overdue contributions (red), contributions received
  this month, number of active participants.
- **Participant cards:** one card per participant with a pink circle showing their initial, their
  name, the outstanding amount in large type, and a progress bar of this month's contributions
  received of expected. Under it a compact list of their shared costs: name, share (e.g. "1 of 2 ·
  50%") and their part.
- **Outstanding contributions:** a list with due date and amount, overdue ones in red, and a one-tap
  check button to mark a contribution received.
- **Chart:** received versus outstanding contributions per participant (FR-3).
- Managing participants (add, edit, archive) moves to the end of the screen, below the overview.

### 3.10 Settings screen

**FR-20 Settings screen redesign**
- The list of settings becomes a grid of tiles, grouped under three short headings:
  - **Accounts:** Cards and accounts.
  - **Money in:** Payers, Input categories.
  - **Money out:** Expense categories, Loan categories, Recurring bills.
- Each tile has a solid coloured icon (the colour of the section it belongs to, e.g. violet for
  Cards and accounts), its title, a one-line description and a count of what it holds (e.g. "4
  cards", "12 categories").
- **Recurring bills** gets a Settings tile, so it can be found without going through the Month
  screen; the link on the Month screen stays.
- The settings sub-screens use the same card and colour style (FR-5); categories keep their order
  and hidden categories are shown dimmed.

### 3.11 Changes to earlier requirements

Moving the cards and balances off the Dashboard changes requirements already merged to `main`.
They are not rewritten; each gets an appendix:

- [credit-card-settings.md](credit-card-settings.md) Appendix 3 (FR-9, FR-10, FR-11, Appendix 1)
- [input-cash-flow.md](input-cash-flow.md) Appendix 2 (FR-11, Appendix 1)
- [loans-and-leases.md](loans-and-leases.md) Appendix 2 (FR-3, FR-13)
- [credit-card-installments.md](credit-card-installments.md) Appendix 2 (FR-9)

The redesigns of FR-14 to FR-20 change only how screens look and where buttons are; what each
screen shows, the forms and their rules stay as in the earlier requirements, so they need no
appendix.

## 4. Non-functional requirements

- **NFR-1 Readability and accessibility:** text and key figures keep a contrast of at least WCAG
  2.1 AA against their background. Colour is never the only way to tell things apart: amounts also
  have a sign or label, and chart parts have a legend or label.
- **NFR-2 Responsive:** all changes work on phone, tablet and desktop; on phones the bottom
  navigation stays and the left menu is not shown.
- **NFR-3 Performance:** charts and fonts do not make screens noticeably slower to open than today.
- **NFR-4 No behaviour change:** the design changes do not change what the app stores or how any
  existing amount is calculated; every amount shown today is still shown with the same value (on
  the Cards screen instead of the Dashboard for balances). New values are only sums and
  comparisons of amounts the app already has (Dashboard key figures, Cards screen totals).
- **NFR-5 Language:** UI text in English for now; new texts (menu tooltips, status messages) must be
  easy to translate to Croatian later.

## 5. Out of scope

- Dark mode.
- New screens or new data; charts and key figures only use data the app already has.
- Choosing another month on the Dashboard (it always shows the current month).
- Changing the sections of the menu, their order or their icons (only their colour).
- A user setting to pick colours or themes.
- Quick actions on screens other than the Dashboard (Month keeps its add buttons in its sections).
- Details of the system status beyond API and database (e.g. version, response times).

## 6. Acceptance criteria

1. Every main section has its section colour, used for its menu icon and as the accent on its
   screens; inputs are green, outputs red or orange and overdue items red on every screen.
2. Each overview screen starts with at most 4 key figures in large type, followed by charts and then
   the detailed lists; secondary details are only on details or edit screens or in expanded rows.
3. The screens listed in FR-3 show the listed charts in the meaning colours, with labels or a
   legend and the exact amount on point or tap, and the exact amounts stay on the screen.
4. Headings and key figures use a display font different from the body font; no more than two font
   families are used.
5. On desktop and tablet one button collapses the left menu to coloured icons and the same button
   expands it again; the current section is highlighted in both states and the choice is
   remembered on that device.
6. In the collapsed menu, pointing at an icon shows its label.
7. The Dashboard has no Cards widget, no System status card and shows no current, expected or
   starting balance anywhere.
8. When the API and database are healthy, no system status is shown anywhere.
9. When the API is unreachable or the database is not healthy, the bottom of the left menu (or a
   red bar on phones) shows what is wrong, and it disappears on its own once both are healthy
   again.
10. All amounts shown before the redesign are still shown with the same values.
11. The Dashboard shows the current month's four key figures with change pills, the highlight
    card, the overview card with the 6-month chart, Coming up, outputs by kind and the small cards
    for sections that have data; each opens the screen it comes from.
12. The Cards screen shows the accounts with their current and expected end-of-month balances,
    status and starting balance hint by the same rules as the old Dashboard widget, its four key
    figures and the balance chart, followed by the cards with installments.
13. The Dashboard starts with the quick actions Add income, Add bill, Add expense, Cash withdrawal
    and (when a card can take a purchase) Add purchase, shown as coloured icons whose label appears
    only after resting the pointer on the icon for about one second (or on focus); each opens
    its form for the current month and returns to the Dashboard after saving or cancelling.
14. The Month screen keeps each add button in its own section header with the FR-14 icon and
    colour (no quick action row), and shows its key figures and
    progress bars, collapsible sections with totals and "x of y paid", and short rows with status
    chips and one-tap paid or received.
15. The Cash flow screen shows its key figures, the period buttons, the other filters behind one
    Filters button with chips, the full-width chart and the months list; the monthly report shows
    key figures with change pills, the two ring charts and collapsible groups.
16. The Loans screen shows its key figures, a card per loan with a progress bar of payments made,
    overdue loans in red, and the paid versus remaining chart.
17. The Shared costs screen shows its key figures, a card per participant with outstanding amount
    and progress, the outstanding list with one-tap received, and the per-participant chart.
18. Settings shows grouped tiles with coloured icons and counts, including Recurring bills.
