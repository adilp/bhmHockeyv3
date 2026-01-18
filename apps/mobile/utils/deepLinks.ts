import * as Linking from 'expo-linking';
import { router } from 'expo-router';

/**
 * Deep link URL patterns:
 * - bhmhockey://tournament/{tournamentId}/team/{teamId}
 */

interface DeepLinkData {
  path: string;
  queryParams?: Record<string, string>;
}

/**
 * Parse incoming deep link URL and extract path and params
 */
export function parseDeepLink(url: string): DeepLinkData | null {
  console.log('🔗 Parsing deep link:', url);

  // Parse URL using expo-linking
  const parsed = Linking.parse(url);
  console.log('🔗 Parsed URL:', JSON.stringify(parsed, null, 2));

  if (!parsed.path) {
    console.log('🔗 No path found in deep link');
    return null;
  }

  return {
    path: parsed.path,
    queryParams: parsed.queryParams as Record<string, string> | undefined,
  };
}

/**
 * Handle deep link navigation
 * Maps deep link patterns to app routes
 */
export function handleDeepLink(url: string) {
  console.log('🔗 handleDeepLink called with:', url);

  const data = parseDeepLink(url);
  if (!data) {
    console.log('🔗 Could not parse deep link');
    return;
  }

  const { path } = data;
  console.log('🔗 Deep link path:', path);

  // Match pattern: tournament/{tournamentId}/team/{teamId}
  const tournamentTeamMatch = path.match(/^tournament\/([^/]+)\/team\/([^/]+)\/?$/);
  if (tournamentTeamMatch) {
    const [, tournamentId, teamId] = tournamentTeamMatch;
    console.log('🔗 Navigating to tournament team:', { tournamentId, teamId });
    router.push(`/tournaments/${tournamentId}/teams/${teamId}`);
    return;
  }

  // Add more deep link patterns here as needed
  // Example: tournament/{tournamentId}
  const tournamentMatch = path.match(/^tournament\/([^/]+)\/?$/);
  if (tournamentMatch) {
    const [, tournamentId] = tournamentMatch;
    console.log('🔗 Navigating to tournament:', tournamentId);
    router.push(`/tournaments/${tournamentId}`);
    return;
  }

  console.log('🔗 No matching deep link pattern for path:', path);
}

/**
 * Get the initial deep link URL when app launches
 */
export async function getInitialDeepLink(): Promise<string | null> {
  return await Linking.getInitialURL();
}

/**
 * Add listener for deep links while app is running
 */
export function addDeepLinkListener(
  callback: (url: string) => void
) {
  const subscription = Linking.addEventListener('url', (event) => {
    callback(event.url);
  });
  return subscription;
}
