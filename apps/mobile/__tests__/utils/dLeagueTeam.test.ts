import { dLeagueTeamColor } from '../../utils/dLeagueTeam';

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
