import { canHaveDLeagueTeam, dLeagueTeamColor, isDLeagueEligibleSkill } from '../../utils/dLeagueTeam';

describe('isDLeagueEligibleSkill', () => {
  it('allows D-League and Bronze, since Bronze players skate in both leagues', () => {
    expect(isDLeagueEligibleSkill('D-League')).toBe(true);
    expect(isDLeagueEligibleSkill('Bronze')).toBe(true);
  });

  it('rejects Silver and Gold, who are not allowed in D-League', () => {
    expect(isDLeagueEligibleSkill('Silver')).toBe(false);
    expect(isDLeagueEligibleSkill('Gold')).toBe(false);
  });

  it('rejects a missing level', () => {
    expect(isDLeagueEligibleSkill(null)).toBe(false);
    expect(isDLeagueEligibleSkill(undefined)).toBe(false);
  });
});

describe('canHaveDLeagueTeam', () => {
  it('offers the team to a D-League player', () => {
    expect(canHaveDLeagueTeam({ skater: 'D-League' })).toBe(true);
    expect(canHaveDLeagueTeam({ goalie: 'D-League' })).toBe(true);
  });

  it('offers the team to a Bronze player', () => {
    expect(canHaveDLeagueTeam({ skater: 'Bronze' })).toBe(true);
    expect(canHaveDLeagueTeam({ goalie: 'Bronze' })).toBe(true);
  });

  it('withholds it from Silver and Gold players', () => {
    expect(canHaveDLeagueTeam({ skater: 'Silver' })).toBe(false);
    expect(canHaveDLeagueTeam({ goalie: 'Gold', skater: 'Silver' })).toBe(false);
  });

  it('offers it when only one of two positions qualifies', () => {
    expect(canHaveDLeagueTeam({ goalie: 'Gold', skater: 'Bronze' })).toBe(true);
  });

  it('withholds it when no positions are set', () => {
    expect(canHaveDLeagueTeam({})).toBe(false);
    expect(canHaveDLeagueTeam(undefined)).toBe(false);
    expect(canHaveDLeagueTeam(null)).toBe(false);
  });
});

describe('dLeagueTeamColor', () => {
  it('maps each team to its jersey color', () => {
    expect(dLeagueTeamColor('Bombers')).toBe('#E5484D');      // Red
    expect(dLeagueTeamColor('Knuckleheads')).toBe('#3E9BFF'); // Blue
    expect(dLeagueTeamColor('Killer Bees')).toBe('#E0B341');  // Gold
    expect(dLeagueTeamColor('Molar Bears')).toBe('#F0761A');  // Orange
    expect(dLeagueTeamColor('Lawdog')).toBe('#E6EDF3');       // White
    expect(dLeagueTeamColor('Bandits')).toBe('#6E7681');      // Black
  });

  it('returns null when the player is not on a team', () => {
    expect(dLeagueTeamColor(null)).toBeNull();
    expect(dLeagueTeamColor(undefined)).toBeNull();
    expect(dLeagueTeamColor('')).toBeNull();
  });

  it('returns null for an unknown team rather than a wrong color', () => {
    expect(dLeagueTeamColor('Mighty Ducks')).toBeNull();
  });
});
