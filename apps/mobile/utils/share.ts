import { Share } from 'react-native';
import { APP_STORE_URLS } from '../config/appStoreUrls';

export async function shareOrganizationInvite(
  orgId: string,
  orgName: string
): Promise<boolean> {
  const deepLink = `bhmhockey://organizations/${orgId}`;

  const message = `Join ${orgName} on BHM Hockey!

1. Download the app:
   - iPhone: ${APP_STORE_URLS.ios}
   - Android: ${APP_STORE_URLS.android}

2. After installing, tap this link to join:
   ${deepLink}

Already have the app? Just tap the link above!`;

  try {
    const result = await Share.share({ message });
    return result.action === Share.sharedAction;
  } catch (error) {
    console.error('Error sharing organization invite:', error);
    return false;
  }
}

export async function shareTournamentTeam(
  tournamentId: string,
  tournamentName: string,
  teamId: string,
  teamName: string
): Promise<boolean> {
  const deepLink = `bhmhockey://tournament/${tournamentId}/team/${teamId}`;

  const message = `Check out ${teamName} in the ${tournamentName} tournament on BHM Hockey!

1. Download the app:
   - iPhone: ${APP_STORE_URLS.ios}
   - Android: ${APP_STORE_URLS.android}

2. After installing, tap this link to view the team:
   ${deepLink}

Already have the app? Just tap the link above!`;

  try {
    const result = await Share.share({ message });
    return result.action === Share.sharedAction;
  } catch (error) {
    console.error('Error sharing tournament team:', error);
    return false;
  }
}
