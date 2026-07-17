// Client-side mirror of the backend's GroupMeLinkValidator:
// links must be https URLs on groupme.com (or www.groupme.com), max 500 chars.
// The backend is authoritative - this just gives users a friendly error before submitting.
const GROUPME_LINK_PATTERN = /^https:\/\/(www\.)?groupme\.com([/?#]|$)/i;

export const GROUPME_LINK_ERROR =
  'GroupMe link must be an https://groupme.com URL (e.g., https://groupme.com/join_group/...)';

export function isValidGroupMeLink(link: string): boolean {
  const trimmed = link.trim();
  return trimmed.length <= 500 && GROUPME_LINK_PATTERN.test(trimmed);
}
