import { formatCents } from '@/lib/money'

/**
 * Shared cost labels and messages in one place, so they can be translated to Croatian later (shared
 * bills NFR-5).
 */
export const sharedCostTexts = {
  share: {
    shared: 'Shared',
    sharedHint: 'Split into equal shares with one participant, who pays their part back.',
    participant: 'Participant',
    chooseParticipant: 'Choose a participant',
    addNew: 'Add new…',
    newName: 'Name of the new participant',
    add: 'Add',
    shares: 'Shares',
    participantShares: 'Participant pays',
    sharesOption: (count: number) => `${count} shares`,
    participantSharesOption: (count: number, of: number) => `${count} of ${of}`,
    contributionAccount: 'Contributions paid to',
    contributionAccountDefault: 'Account it is paid from',
    summary: (name: string, percentage: number) => `${name} pays ${percentage} %`,
    parts: (participantCents: number, ownCents: number) =>
      `Their part ${formatCents(participantCents)}, your part ${formatCents(ownCents)}.`,
    participantRequired: 'Choose who shares the cost.',
    sharedMarker: 'Shared',
    changedMarker: 'Share changed',
    ownPart: (cents: number) => `Your part ${formatCents(cents)}`,
    ownPartTotal: 'Your part',
    keepChanged: (count: number) =>
      count === 1
        ? 'Keep the share set by hand on 1 expected payment'
        : `Keep the shares set by hand on ${count} expected payments`,
  },
  payment: {
    title: 'Share this payment',
    description:
      'Shares only this payment, or stops sharing it, whatever the loan’s share is. The other payments stay as they are.',
    followLoan: 'Follow the loan’s share',
    save: 'Save',
    cancel: 'Cancel',
    saved: 'Share saved.',
  },
  contribution: {
    title: 'Contribution',
    of: (cost: string) => `For ${cost}`,
    part: (cents: number) => `Part ${formatCents(cents)}`,
    edited: 'Edited',
    needsAccount: 'No account yet',
    resetToPart: 'Reset to part',
    reset: 'Contribution reset to the part.',
    noDelete: 'A contribution goes away when its bill or loan payment is no longer shared.',
    received: 'Received',
    differs: (receivedCents: number, partCents: number) =>
      `Received ${formatCents(receivedCents)}, the part is now ${formatCents(partCents)} (difference ${formatCents(partCents - receivedCents)}).`,
    group: (count: number) => (count === 1 ? '1 contribution' : `${count} contributions`),
    groupReceived: (name: string) => `Received: all contributions of ${name}`,
    groupPartly: (received: number, count: number) => `${received} of ${count} received`,
    groupNeedsAccount: (count: number) =>
      count === 1
        ? '1 contribution has no account yet; tick it on its own to choose one.'
        : `${count} contributions have no account yet; tick them on their own to choose one.`,
    showSingle: (count: number) => `Show ${count}`,
    hideSingle: 'Hide',
    expectedOn: (date: string) => `Expected ${date}`,
    receivedOn: (date: string) => `Received ${date}`,
    amount: 'Amount',
    expectedDate: 'Expected on',
    receivedDate: 'Received on',
    account: 'Paid to account',
    amountInvalid: 'Enter an amount greater than zero.',
    accountRequired: 'Choose the account the contribution is paid to.',
    receivedChanges: (count: number) =>
      count === 1
        ? 'A received contribution does not change with this; settle the difference with the participant.'
        : `${count} received contributions do not change with this; settle the differences with the participant.`,
  },
  screen: {
    title: 'Shared costs',
    description:
      'What each participant shares, their parts and the contributions still outstanding.',
    participants: 'Participants',
    addParticipant: 'Add participant',
    editParticipant: 'Edit participant',
    noParticipants: 'No participants yet. Add one to share bills and loans.',
    showArchived: 'Show archived',
    archived: 'Archived',
    archive: 'Archive',
    restore: 'Restore',
    delete: 'Delete',
    deleteConfirm: (name: string) => `Delete ${name}?`,
    deleteDescription: 'The participant shares no cost; their payer and contributions stay.',
    edit: 'Edit',
    saved: 'Participant saved.',
    costs: 'Shares',
    noCosts: 'Shares no recurring bill or loan.',
    recurringBill: 'Recurring bill',
    loan: 'Loan',
    monthParts: 'Parts this month',
    monthReceived: 'Received this month',
    outstanding: 'Outstanding',
    outstandingList: 'Outstanding contributions',
    nothingOutstanding: 'Nothing outstanding.',
    monthContributions: 'Contributions this month',
    noMonthContributions: 'No contributions this month.',
    overdue: 'Overdue',
    tickReceived: 'Received',
    expected: 'Expected',
    sharedCount: (count: number) => `${count} shared ${count === 1 ? 'cost' : 'costs'}`,
    receivedOf: (received: string, parts: string) => `${received} of ${parts} received this month`,
    receivedAndOutstanding: 'Received and outstanding per participant',
    name: 'Name',
    description_: 'Description',
    save: 'Save',
    cancel: 'Cancel',
  },
  report: {
    title: 'Shared costs',
    total: 'Shared bills and loan payments',
    participantsPart: 'Participants’ parts',
    ownPart: 'Your part',
    received: 'Contributions received',
    outstanding: 'Contributions outstanding',
  },
  errors: {
    nameRequired: 'Name is required.',
    nameTooLong: 'Name can be at most 50 characters.',
    descriptionTooLong: 'Description can be at most 200 characters.',
    participantSharesInvalid: 'The participant pays at least one share and fewer than all of them.',
  },
}
