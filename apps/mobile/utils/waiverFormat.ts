export interface WaiverTextSegment {
  text: string;
  bold: boolean;
}

/**
 * Parses waiver text with **bold** markers into renderable segments.
 * The same rule is applied server-side when generating the waiver PDF
 * (OrganizationWaiverService.ParseBoldSegments) — keep them in sync.
 * An unmatched trailing ** is rendered literally rather than bolding
 * the rest of the document.
 */
export function parseWaiverSegments(text: string): WaiverTextSegment[] {
  const parts = text.split('**');
  if (parts.length === 1) {
    return [{ text, bold: false }];
  }

  const unbalanced = parts.length % 2 === 0;
  const segments: WaiverTextSegment[] = [];
  parts.forEach((part, i) => {
    const isLast = i === parts.length - 1;
    if (unbalanced && isLast) {
      segments.push({ text: `**${part}`, bold: false });
    } else if (part.length > 0) {
      segments.push({ text: part, bold: i % 2 === 1 });
    }
  });
  return segments;
}
