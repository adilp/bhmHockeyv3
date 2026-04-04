import type { EventRegistrationDto, RosterOrderItem, RosterOrderResult, TeamAssignment } from '@bhmhockey/shared';
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

    const result = buildRosterOrderItems(registrations, draggedReg, targetTeam, targetSlotIndex);

    // g1 should now be on White team
    const g1Item = result!.items.find(i => i.registrationId === 'g1');
    expect(g1Item?.teamAssignment).toBe('White');

    // No black goalies remain
    const blackGoalies = result!.items.filter(i => {
      const reg = registrations.find(r => r.id === i.registrationId);
      return reg?.registeredPosition === 'Goalie' && i.teamAssignment === 'Black';
    });
    expect(blackGoalies).toHaveLength(0);

    // White should have 2 goalies
    const whiteGoalies = result!.items.filter(i => {
      const reg = registrations.find(r => r.id === i.registrationId);
      return reg?.registeredPosition === 'Goalie' && i.teamAssignment === 'White';
    });
    expect(whiteGoalies).toHaveLength(2);
  });

  it('returns RosterOrderResult (not plain array)', () => {
    const registrations = [
      makeReg({ id: 'g1', teamAssignment: 'Black', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 's1', teamAssignment: 'Black', registeredPosition: 'Skater', rosterOrder: 1 }),
      makeReg({ id: 's2', teamAssignment: 'White', registeredPosition: 'Skater', rosterOrder: 1 }),
    ];

    const result = buildRosterOrderItems(registrations, registrations[0], 'White', 0);

    expect(result).not.toBeNull();
    expect(result).toHaveProperty('items');
    expect(Array.isArray(result!.items)).toBe(true);
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
    const result = buildRosterOrderItems(registrations, draggedReg, targetTeam, targetSlotIndex);
    expect(result).not.toBeNull();

    const g2Item = result!.items.find(i => i.registrationId === 'g2');
    expect(g2Item?.teamAssignment).toBe('White');
  });
});

describe('buildRosterOrderItems — cross-position drops', () => {
  it('skater dropped on goalie slot returns result with positionChange', () => {
    const registrations = [
      makeReg({ id: 'g1', teamAssignment: 'Black', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 'g2', teamAssignment: 'White', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 's1', teamAssignment: 'Black', registeredPosition: 'Skater', rosterOrder: 1 }),
      makeReg({ id: 's2', teamAssignment: 'White', registeredPosition: 'Skater', rosterOrder: 1 }),
    ];

    const result = buildRosterOrderItems(registrations, registrations[2], 'Black', 0);

    expect(result).not.toBeNull();
    expect(result!.positionChange).toEqual({
      registrationId: 's1',
      newPosition: 'Goalie',
      playerName: 's1',
    });
  });

  it('goalie dropped on skater slot returns result with positionChange', () => {
    const registrations = [
      makeReg({ id: 'g1', teamAssignment: 'Black', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 'g2', teamAssignment: 'White', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 's1', teamAssignment: 'Black', registeredPosition: 'Skater', rosterOrder: 1 }),
      makeReg({ id: 's2', teamAssignment: 'White', registeredPosition: 'Skater', rosterOrder: 1 }),
    ];

    // maxGoalies = 1, so slot index 1 = first skater slot
    const result = buildRosterOrderItems(registrations, registrations[0], 'Black', 1);

    expect(result).not.toBeNull();
    expect(result!.positionChange).toEqual({
      registrationId: 'g1',
      newPosition: 'Skater',
      playerName: 'g1',
    });
  });

  it('cross-position drop sets registeredPosition on the moved item', () => {
    const registrations = [
      makeReg({ id: 'g1', teamAssignment: 'Black', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 's1', teamAssignment: 'Black', registeredPosition: 'Skater', rosterOrder: 1 }),
    ];

    // Skater s1 dropped on goalie slot (index 0)
    const result = buildRosterOrderItems(registrations, registrations[1], 'Black', 0);

    const s1Item = result!.items.find(i => i.registrationId === 's1');
    expect(s1Item!.registeredPosition).toBe('Goalie');
  });

  it('same-position moves do NOT set positionChange', () => {
    const registrations = [
      makeReg({ id: 'g1', teamAssignment: 'Black', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 'g2', teamAssignment: 'White', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 's1', teamAssignment: 'Black', registeredPosition: 'Skater', rosterOrder: 1 }),
    ];

    // Goalie moved to other team (same position type)
    const result = buildRosterOrderItems(registrations, registrations[0], 'White', 0);

    expect(result).not.toBeNull();
    expect(result!.positionChange).toBeUndefined();
  });

  it('skater-to-goalie inserts at position 0 via unshift', () => {
    const registrations = [
      makeReg({ id: 'g1', teamAssignment: 'Black', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 's1', teamAssignment: 'Black', registeredPosition: 'Skater', rosterOrder: 1 }),
      makeReg({ id: 's2', teamAssignment: 'Black', registeredPosition: 'Skater', rosterOrder: 2 }),
    ];

    // s2 dropped on goalie slot
    const result = buildRosterOrderItems(registrations, registrations[2], 'Black', 0);

    // s2 should now be first goalie (rosterOrder 0)
    const s2Item = result!.items.find(i => i.registrationId === 's2');
    expect(s2Item!.registeredPosition).toBe('Goalie');
    // s2 should be before g1 (unshift behavior)
    const goalieItems = result!.items.filter(i => {
      const reg = registrations.find(r => r.id === i.registrationId);
      return i.registeredPosition === 'Goalie' || (reg?.registeredPosition === 'Goalie' && !i.registeredPosition);
    });
    expect(goalieItems[0].registrationId).toBe('s2');
  });

  it('goalie-to-skater inserts at correct skater position', () => {
    const registrations = [
      makeReg({ id: 'g1', teamAssignment: 'Black', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 'g2', teamAssignment: 'White', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 's1', teamAssignment: 'Black', registeredPosition: 'Skater', rosterOrder: 1 }),
      makeReg({ id: 's2', teamAssignment: 'Black', registeredPosition: 'Skater', rosterOrder: 2 }),
    ];

    // g1 dropped on skater slot index 2 (second skater row, since maxGoalies=1)
    const result = buildRosterOrderItems(registrations, registrations[0], 'Black', 2);

    expect(result!.positionChange!.newPosition).toBe('Skater');
    // g1 should be inserted between s1 and s2
    const blackSkaterItems = result!.items
      .filter(i => i.teamAssignment === 'Black')
      .filter(i => {
        const reg = registrations.find(r => r.id === i.registrationId);
        return i.registeredPosition === 'Skater' || (reg?.registeredPosition !== 'Goalie' && !i.registeredPosition);
      })
      .sort((a, b) => a.rosterOrder - b.rosterOrder);

    const ids = blackSkaterItems.map(i => i.registrationId);
    expect(ids).toEqual(['s1', 'g1', 's2']);
  });

  it('cross-position to different team works', () => {
    const registrations = [
      makeReg({ id: 'g1', teamAssignment: 'Black', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 's1', teamAssignment: 'White', registeredPosition: 'Skater', rosterOrder: 1 }),
    ];

    // s1 (White skater) dropped on goalie slot, Black team
    const result = buildRosterOrderItems(registrations, registrations[1], 'Black', 0);

    expect(result).not.toBeNull();
    expect(result!.positionChange).toEqual({
      registrationId: 's1',
      newPosition: 'Goalie',
      playerName: 's1',
    });
    const s1Item = result!.items.find(i => i.registrationId === 's1');
    expect(s1Item!.teamAssignment).toBe('Black');
    expect(s1Item!.registeredPosition).toBe('Goalie');
  });
})

describe('buildRosterOrderItems — label shifting', () => {
  it('shifts labels up when skater becomes goalie (maxGoalies increases)', () => {
    // 1 goalie each team. Labels: {1: "C", 2: "LW"}
    const registrations = [
      makeReg({ id: 'g1', teamAssignment: 'Black', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 'g2', teamAssignment: 'White', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 's1', teamAssignment: 'Black', registeredPosition: 'Skater', rosterOrder: 1 }),
      makeReg({ id: 's2', teamAssignment: 'Black', registeredPosition: 'Skater', rosterOrder: 2 }),
    ];

    const labels: Record<number, string> = { 1: 'C', 2: 'LW' };

    // s1 dropped on goalie slot → now 2 goalies on Black, maxGoalies goes from 1 to 2
    const result = buildRosterOrderItems(registrations, registrations[2], 'Black', 0, labels);

    // Labels should shift up by 1: {2: "C", 3: "LW"}
    expect(result!.shiftedLabels).toEqual({ 2: 'C', 3: 'LW' });
  });

  it('shifts labels down when goalie becomes skater (maxGoalies decreases)', () => {
    // 2 goalies on Black, 1 on White → maxGoalies = 2. Labels keyed at {2: "C", 3: "LW"}
    const registrations = [
      makeReg({ id: 'g1', teamAssignment: 'Black', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 'g2', teamAssignment: 'Black', registeredPosition: 'Goalie', rosterOrder: 1 }),
      makeReg({ id: 'g3', teamAssignment: 'White', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 's1', teamAssignment: 'Black', registeredPosition: 'Skater', rosterOrder: 2 }),
    ];

    const labels: Record<number, string> = { 2: 'C', 3: 'LW' };

    // g2 dropped on skater slot (index 2 = first skater when maxGoalies=2)
    const result = buildRosterOrderItems(registrations, registrations[1], 'Black', 2, labels);

    // maxGoalies goes from 2 to 1, labels shift down by 1: {1: "C", 2: "LW"}
    expect(result!.shiftedLabels).toEqual({ 1: 'C', 2: 'LW' });
  });

  it('no shiftedLabels when maxGoalies stays the same', () => {
    const registrations = [
      makeReg({ id: 'g1', teamAssignment: 'Black', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 'g2', teamAssignment: 'White', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 's1', teamAssignment: 'Black', registeredPosition: 'Skater', rosterOrder: 1 }),
      makeReg({ id: 's2', teamAssignment: 'White', registeredPosition: 'Skater', rosterOrder: 1 }),
    ];

    const labels: Record<number, string> = { 1: 'C' };

    // Same-position move: skater to other team (maxGoalies stays 1)
    const result = buildRosterOrderItems(registrations, registrations[2], 'White', 1, labels);

    expect(result!.shiftedLabels).toBeUndefined();
  });

  it('no shiftedLabels when no labels provided', () => {
    const registrations = [
      makeReg({ id: 'g1', teamAssignment: 'Black', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 's1', teamAssignment: 'Black', registeredPosition: 'Skater', rosterOrder: 1 }),
    ];

    // Cross-position with no labels
    const result = buildRosterOrderItems(registrations, registrations[1], 'Black', 0);

    expect(result!.shiftedLabels).toBeUndefined();
  });

  it('ignores stale labels at goalie slot indices during shift', () => {
    // maxGoalies = 1. Stale label at index 0 (goalie slot) should be dropped, not shifted
    const registrations = [
      makeReg({ id: 'g1', teamAssignment: 'Black', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 'g2', teamAssignment: 'White', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 's1', teamAssignment: 'Black', registeredPosition: 'Skater', rosterOrder: 1 }),
    ];

    const labels: Record<number, string> = { 0: 'BAD', 1: 'C' };

    // s1 dropped on goalie slot → maxGoalies goes from 1 to 2
    const result = buildRosterOrderItems(registrations, registrations[2], 'Black', 0, labels);

    // Only the skater label (key 1) should shift to key 2; the stale key 0 is dropped
    expect(result!.shiftedLabels).toEqual({ 2: 'C' });
  });
});

describe('buildRosterOrderItems — edge cases', () => {
  it('moving last goalie to skater leaves 0 goalies on team', () => {
    const registrations = [
      makeReg({ id: 'g1', teamAssignment: 'Black', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 'g2', teamAssignment: 'White', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 's1', teamAssignment: 'Black', registeredPosition: 'Skater', rosterOrder: 1 }),
    ];

    // Move the only Black goalie to skater
    const result = buildRosterOrderItems(registrations, registrations[0], 'Black', 1);

    expect(result).not.toBeNull();
    expect(result!.positionChange!.newPosition).toBe('Skater');

    // No Black goalies remain in items
    const blackGoalies = result!.items.filter(i => {
      const reg = registrations.find(r => r.id === i.registrationId);
      const isGoalie = i.registeredPosition === 'Goalie' ||
        (!i.registeredPosition && reg?.registeredPosition === 'Goalie');
      return isGoalie && i.teamAssignment === 'Black';
    });
    expect(blackGoalies).toHaveLength(0);
  });

  it('undefined registeredPosition is treated as Skater (cross-position to goalie)', () => {
    const registrations = [
      makeReg({ id: 'g1', teamAssignment: 'Black', registeredPosition: 'Goalie', rosterOrder: 0 }),
      makeReg({ id: 'u1', teamAssignment: 'Black', registeredPosition: undefined as any, rosterOrder: 1 }),
    ];

    // u1 (undefined position, treated as Skater) dropped on goalie slot
    const result = buildRosterOrderItems(registrations, registrations[1], 'Black', 0);

    expect(result).not.toBeNull();
    expect(result!.positionChange).toEqual({
      registrationId: 'u1',
      newPosition: 'Goalie',
      playerName: 'u1',
    });
    const u1Item = result!.items.find(i => i.registrationId === 'u1');
    expect(u1Item!.registeredPosition).toBe('Goalie');
  });

  it('playerName includes full name (first + last)', () => {
    const registrations = [
      makeReg({ id: 'g1', teamAssignment: 'Black', registeredPosition: 'Goalie', rosterOrder: 0 }),
      {
        ...makeReg({ id: 's1', teamAssignment: 'Black', registeredPosition: 'Skater', rosterOrder: 1 }),
        user: { id: 's1', firstName: 'John', lastName: 'Smith', email: '' } as any,
      } as EventRegistrationDto,
    ];

    const result = buildRosterOrderItems(registrations, registrations[1], 'Black', 0);

    expect(result!.positionChange!.playerName).toBe('John Smith');
  });
});
