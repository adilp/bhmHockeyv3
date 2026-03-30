import type { EventRegistrationDto, RosterOrderItem, TeamAssignment } from '@bhmhockey/shared';

type SlotType = 'goalie' | 'skater';

export interface RosterSlot {
  type: SlotType;
  index: number;
  blackPlayer: EventRegistrationDto | null;
  whitePlayer: EventRegistrationDto | null;
}

/**
 * Build roster slots from registrations.
 * Groups goalies and skaters into paired rows (Black vs White).
 */
export function buildRosterSlots(registrations: EventRegistrationDto[]): RosterSlot[] {
  const blackGoalies = registrations
    .filter(r => r.teamAssignment === 'Black' && r.registeredPosition === 'Goalie')
    .sort((a, b) => (a.rosterOrder ?? 999) - (b.rosterOrder ?? 999));
  const whiteGoalies = registrations
    .filter(r => r.teamAssignment === 'White' && r.registeredPosition === 'Goalie')
    .sort((a, b) => (a.rosterOrder ?? 999) - (b.rosterOrder ?? 999));
  const blackSkaters = registrations
    .filter(r => r.teamAssignment === 'Black' && r.registeredPosition !== 'Goalie')
    .sort((a, b) => (a.rosterOrder ?? 999) - (b.rosterOrder ?? 999));
  const whiteSkaters = registrations
    .filter(r => r.teamAssignment === 'White' && r.registeredPosition !== 'Goalie')
    .sort((a, b) => (a.rosterOrder ?? 999) - (b.rosterOrder ?? 999));

  const result: RosterSlot[] = [];

  const maxGoalies = Math.max(blackGoalies.length, whiteGoalies.length, 1);
  for (let i = 0; i < maxGoalies; i++) {
    result.push({
      type: 'goalie',
      index: i,
      blackPlayer: blackGoalies[i] || null,
      whitePlayer: whiteGoalies[i] || null,
    });
  }

  const maxSkaters = Math.max(blackSkaters.length, whiteSkaters.length, 1);
  for (let i = 0; i < maxSkaters; i++) {
    result.push({
      type: 'skater',
      index: maxGoalies + i,
      blackPlayer: blackSkaters[i] || null,
      whitePlayer: whiteSkaters[i] || null,
    });
  }

  return result;
}

/**
 * Build roster order items after a drag-drop operation.
 * Returns null if the drop is invalid.
 */
export function buildRosterOrderItems(
  registrations: EventRegistrationDto[],
  draggedReg: EventRegistrationDto,
  targetTeam: TeamAssignment,
  targetSlotIndex: number,
): RosterOrderItem[] | null {
  const isGoalie = draggedReg.registeredPosition === 'Goalie';
  const currentTeam = draggedReg.teamAssignment as TeamAssignment;

  // Compute number of goalie slots to validate drop targets
  const blackGoalieCount = registrations.filter(r => r.teamAssignment === 'Black' && r.registeredPosition === 'Goalie').length;
  const whiteGoalieCount = registrations.filter(r => r.teamAssignment === 'White' && r.registeredPosition === 'Goalie').length;
  const maxGoalies = Math.max(blackGoalieCount, whiteGoalieCount, 1);

  if (isGoalie && targetSlotIndex >= maxGoalies) {
    return null;
  }
  if (!isGoalie && targetSlotIndex < maxGoalies) {
    return null;
  }

  // Build fresh arrays
  const allBlackGoalies = registrations
    .filter(r => r.teamAssignment === 'Black' && r.registeredPosition === 'Goalie')
    .sort((a, b) => (a.rosterOrder ?? 999) - (b.rosterOrder ?? 999));
  const allWhiteGoalies = registrations
    .filter(r => r.teamAssignment === 'White' && r.registeredPosition === 'Goalie')
    .sort((a, b) => (a.rosterOrder ?? 999) - (b.rosterOrder ?? 999));
  const allBlackSkaters = registrations
    .filter(r => r.teamAssignment === 'Black' && r.registeredPosition !== 'Goalie')
    .sort((a, b) => (a.rosterOrder ?? 999) - (b.rosterOrder ?? 999));
  const allWhiteSkaters = registrations
    .filter(r => r.teamAssignment === 'White' && r.registeredPosition !== 'Goalie')
    .sort((a, b) => (a.rosterOrder ?? 999) - (b.rosterOrder ?? 999));

  // Remove from current position
  if (isGoalie) {
    if (currentTeam === 'Black') {
      const idx = allBlackGoalies.findIndex(r => r.id === draggedReg.id);
      if (idx >= 0) allBlackGoalies.splice(idx, 1);
    } else {
      const idx = allWhiteGoalies.findIndex(r => r.id === draggedReg.id);
      if (idx >= 0) allWhiteGoalies.splice(idx, 1);
    }
  } else {
    if (currentTeam === 'Black') {
      const idx = allBlackSkaters.findIndex(r => r.id === draggedReg.id);
      if (idx >= 0) allBlackSkaters.splice(idx, 1);
    } else {
      const idx = allWhiteSkaters.findIndex(r => r.id === draggedReg.id);
      if (idx >= 0) allWhiteSkaters.splice(idx, 1);
    }
  }

  // Add to new position
  if (isGoalie) {
    if (targetTeam === 'Black') {
      allBlackGoalies.unshift(draggedReg);
    } else {
      allWhiteGoalies.unshift(draggedReg);
    }
  } else {
    const insertIndex = targetSlotIndex - maxGoalies;
    if (targetTeam === 'Black') {
      allBlackSkaters.splice(Math.min(insertIndex, allBlackSkaters.length), 0, draggedReg);
    } else {
      allWhiteSkaters.splice(Math.min(insertIndex, allWhiteSkaters.length), 0, draggedReg);
    }
  }

  // Build roster order items
  const items: RosterOrderItem[] = [];
  allBlackGoalies.forEach((r, i) => {
    items.push({ registrationId: r.id, teamAssignment: 'Black', rosterOrder: i });
  });
  allWhiteGoalies.forEach((r, i) => {
    items.push({ registrationId: r.id, teamAssignment: 'White', rosterOrder: i });
  });
  const goalieOffset = Math.max(allBlackGoalies.length, allWhiteGoalies.length, 1);
  allBlackSkaters.forEach((r, i) => {
    items.push({ registrationId: r.id, teamAssignment: 'Black', rosterOrder: goalieOffset + i });
  });
  allWhiteSkaters.forEach((r, i) => {
    items.push({ registrationId: r.id, teamAssignment: 'White', rosterOrder: goalieOffset + i });
  });

  return items;
}
