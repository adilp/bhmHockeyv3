import type { EventRegistrationDto, RosterOrderItem, RosterOrderResult, TeamAssignment } from '@bhmhockey/shared';

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
  currentLabels?: Record<number, string>,
): RosterOrderResult | null {
  const isGoalie = draggedReg.registeredPosition === 'Goalie';
  const currentTeam = draggedReg.teamAssignment as TeamAssignment;

  // Compute pre-move maxGoalies
  const blackGoalieCount = registrations.filter(r => r.teamAssignment === 'Black' && r.registeredPosition === 'Goalie').length;
  const whiteGoalieCount = registrations.filter(r => r.teamAssignment === 'White' && r.registeredPosition === 'Goalie').length;
  const maxGoaliesBefore = Math.max(blackGoalieCount, whiteGoalieCount, 1);

  // Determine target position type based on pre-move boundary
  const targetIsGoalieSlot = targetSlotIndex < maxGoaliesBefore;
  const isCrossPosition = (isGoalie && !targetIsGoalieSlot) || (!isGoalie && targetIsGoalieSlot);
  const newPosition = targetIsGoalieSlot ? 'Goalie' : 'Skater';

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
  if (newPosition === 'Goalie') {
    // Goalies always unshift (insert at position 0)
    if (targetTeam === 'Black') {
      allBlackGoalies.unshift(draggedReg);
    } else {
      allWhiteGoalies.unshift(draggedReg);
    }
  } else {
    const insertIndex = targetSlotIndex - maxGoaliesBefore;
    if (targetTeam === 'Black') {
      allBlackSkaters.splice(Math.min(insertIndex, allBlackSkaters.length), 0, draggedReg);
    } else {
      allWhiteSkaters.splice(Math.min(insertIndex, allWhiteSkaters.length), 0, draggedReg);
    }
  }

  // Compute post-move maxGoalies
  const maxGoaliesAfter = Math.max(allBlackGoalies.length, allWhiteGoalies.length, 1);

  // Build roster order items
  const items: RosterOrderItem[] = [];
  allBlackGoalies.forEach((r, i) => {
    const item: RosterOrderItem = { registrationId: r.id, teamAssignment: 'Black', rosterOrder: i };
    if (isCrossPosition && r.id === draggedReg.id) item.registeredPosition = 'Goalie';
    items.push(item);
  });
  allWhiteGoalies.forEach((r, i) => {
    const item: RosterOrderItem = { registrationId: r.id, teamAssignment: 'White', rosterOrder: i };
    if (isCrossPosition && r.id === draggedReg.id) item.registeredPosition = 'Goalie';
    items.push(item);
  });
  const goalieOffset = Math.max(allBlackGoalies.length, allWhiteGoalies.length, 1);
  allBlackSkaters.forEach((r, i) => {
    const item: RosterOrderItem = { registrationId: r.id, teamAssignment: 'Black', rosterOrder: goalieOffset + i };
    if (isCrossPosition && r.id === draggedReg.id) item.registeredPosition = 'Skater';
    items.push(item);
  });
  allWhiteSkaters.forEach((r, i) => {
    const item: RosterOrderItem = { registrationId: r.id, teamAssignment: 'White', rosterOrder: goalieOffset + i };
    if (isCrossPosition && r.id === draggedReg.id) item.registeredPosition = 'Skater';
    items.push(item);
  });

  // Build result
  const result: RosterOrderResult = { items };

  // Set positionChange for cross-position drops
  if (isCrossPosition) {
    result.positionChange = {
      registrationId: draggedReg.id,
      newPosition,
      playerName: `${draggedReg.user.firstName} ${draggedReg.user.lastName}`.trim(),
    };
  }

  // Compute shifted labels if maxGoalies changed
  if (currentLabels && Object.keys(currentLabels).length > 0 && maxGoaliesAfter !== maxGoaliesBefore) {
    const shift = maxGoaliesAfter - maxGoaliesBefore;
    const shifted: Record<number, string> = {};
    for (const [key, value] of Object.entries(currentLabels)) {
      const numKey = Number(key);
      // Only shift labels at skater indices (>= old maxGoalies boundary)
      if (numKey >= maxGoaliesBefore) {
        shifted[numKey + shift] = value;
      }
    }
    result.shiftedLabels = shifted;
  }

  return result;
}
