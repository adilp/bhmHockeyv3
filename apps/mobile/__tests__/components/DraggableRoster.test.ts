import type { EventRegistrationDto, RosterOrderItem, TeamAssignment } from '@bhmhockey/shared';
import { buildRosterSlots, buildRosterOrderItems } from '../../components/DraggableRoster.utils';

// Helper to create a minimal registration for testing
function makeReg(
  overrides: Partial<EventRegistrationDto> & { id: string; teamAssignment: TeamAssignment; registeredPosition: string }
): EventRegistrationDto {
  return {
    userId: overrides.id,
    eventId: 'event-1',
    status: 'Registered',
    rosterOrder: overrides.rosterOrder ?? 0,
    user: { id: overrides.id, firstName: overrides.id, lastName: '', email: '' } as any,
    ...overrides,
  } as EventRegistrationDto;
}

describe('buildRosterSlots', () => {
  it('shows a single goalie row when each team has one goalie', () => {
    const registrations = [
      makeReg({ id: 'g1', teamAssignment: 'Black', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 'g2', teamAssignment: 'White', registeredPosition: 'Goalie', rosterOrder: 0 }),
    ];

    const slots = buildRosterSlots(registrations);
    const goalieSlots = slots.filter(s => s.type === 'goalie');

    expect(goalieSlots).toHaveLength(1);
    expect(goalieSlots[0].blackPlayer?.id).toBe('g1');
    expect(goalieSlots[0].whitePlayer?.id).toBe('g2');
  });

  it('shows two goalie rows when one team has two goalies', () => {
    const registrations = [
      makeReg({ id: 'g1', teamAssignment: 'White', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 'g2', teamAssignment: 'White', registeredPosition: 'Goalie', rosterOrder: 1 }),
    ];

    const slots = buildRosterSlots(registrations);
    const goalieSlots = slots.filter(s => s.type === 'goalie');

    expect(goalieSlots).toHaveLength(2);
    // First row: no black goalie, white goalie g1
    expect(goalieSlots[0].blackPlayer).toBeNull();
    expect(goalieSlots[0].whitePlayer?.id).toBe('g1');
    // Second row: no black goalie, white goalie g2
    expect(goalieSlots[1].blackPlayer).toBeNull();
    expect(goalieSlots[1].whitePlayer?.id).toBe('g2');
  });

  it('shows one goalie row minimum even with no goalies', () => {
    const registrations = [
      makeReg({ id: 's1', teamAssignment: 'Black', registeredPosition: 'Skater', rosterOrder: 0 }),
    ];

    const slots = buildRosterSlots(registrations);
    const goalieSlots = slots.filter(s => s.type === 'goalie');

    expect(goalieSlots).toHaveLength(1);
    expect(goalieSlots[0].blackPlayer).toBeNull();
    expect(goalieSlots[0].whitePlayer).toBeNull();
  });

  it('skater slot indices start after goalie slots', () => {
    const registrations = [
      makeReg({ id: 'g1', teamAssignment: 'Black', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 'g2', teamAssignment: 'Black', registeredPosition: 'Goalie', rosterOrder: 1 }),
      makeReg({ id: 's1', teamAssignment: 'Black', registeredPosition: 'Skater', rosterOrder: 0 }),
    ];

    const slots = buildRosterSlots(registrations);
    const goalieSlots = slots.filter(s => s.type === 'goalie');
    const skaterSlots = slots.filter(s => s.type === 'skater');

    expect(goalieSlots).toHaveLength(2);
    expect(goalieSlots[0].index).toBe(0);
    expect(goalieSlots[1].index).toBe(1);
    // Skater indices start after goalies
    expect(skaterSlots[0].index).toBe(2);
  });
});

describe('buildRosterOrderItems', () => {
  it('allows moving a goalie from Black to White (2 white goalies, 0 black)', () => {
    const registrations = [
      makeReg({ id: 'g1', teamAssignment: 'Black', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 'g2', teamAssignment: 'White', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 's1', teamAssignment: 'Black', registeredPosition: 'Skater', rosterOrder: 1 }),
      makeReg({ id: 's2', teamAssignment: 'White', registeredPosition: 'Skater', rosterOrder: 1 }),
    ];

    const draggedReg = registrations[0]; // g1, Black goalie
    const targetTeam: TeamAssignment = 'White';
    const targetSlotIndex = 0; // goalie slot

    const items = buildRosterOrderItems(registrations, draggedReg, targetTeam, targetSlotIndex);

    // g1 should now be on White team
    const g1Item = items.find(i => i.registrationId === 'g1');
    expect(g1Item?.teamAssignment).toBe('White');

    // No black goalies remain
    const blackGoalies = items.filter(i => {
      const reg = registrations.find(r => r.id === i.registrationId);
      return reg?.registeredPosition === 'Goalie' && i.teamAssignment === 'Black';
    });
    expect(blackGoalies).toHaveLength(0);

    // White should have 2 goalies
    const whiteGoalies = items.filter(i => {
      const reg = registrations.find(r => r.id === i.registrationId);
      return reg?.registeredPosition === 'Goalie' && i.teamAssignment === 'White';
    });
    expect(whiteGoalies).toHaveLength(2);
  });

  it('goalie drop on any goalie slot index is valid (not just 0)', () => {
    // 2 goalies on Black, 0 on White. Drag one to White at slot index 1 (second goalie row)
    const registrations = [
      makeReg({ id: 'g1', teamAssignment: 'Black', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 'g2', teamAssignment: 'Black', registeredPosition: 'Goalie', rosterOrder: 1 }),
      makeReg({ id: 's1', teamAssignment: 'Black', registeredPosition: 'Skater', rosterOrder: 2 }),
    ];

    const draggedReg = registrations[1]; // g2
    const targetTeam: TeamAssignment = 'White';
    const targetSlotIndex = 1; // second goalie slot

    // Should NOT return null (i.e., should be a valid drop)
    const items = buildRosterOrderItems(registrations, draggedReg, targetTeam, targetSlotIndex);
    expect(items).not.toBeNull();

    const g2Item = items!.find(i => i.registrationId === 'g2');
    expect(g2Item?.teamAssignment).toBe('White');
  });
});
