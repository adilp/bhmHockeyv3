import { parseWaiverSegments } from '../../utils/waiverFormat';

describe('parseWaiverSegments', () => {
  it('returns plain text untouched when there are no markers', () => {
    expect(parseWaiverSegments('Just a waiver.')).toEqual([
      { text: 'Just a waiver.', bold: false },
    ]);
  });

  it('bolds a single marked section', () => {
    expect(parseWaiverSegments('Read **carefully** please')).toEqual([
      { text: 'Read ', bold: false },
      { text: 'carefully', bold: true },
      { text: ' please', bold: false },
    ]);
  });

  it('handles multiple bold sections', () => {
    expect(parseWaiverSegments('**A** and **B**')).toEqual([
      { text: 'A', bold: true },
      { text: ' and ', bold: false },
      { text: 'B', bold: true },
    ]);
  });

  it('renders an unmatched trailing marker literally instead of bolding the rest', () => {
    expect(parseWaiverSegments('Signed **here and more text')).toEqual([
      { text: 'Signed ', bold: false },
      { text: '**here and more text', bold: false },
    ]);
  });

  it('handles bold spanning newlines', () => {
    expect(parseWaiverSegments('**Section 1**\nBody')).toEqual([
      { text: 'Section 1', bold: true },
      { text: '\nBody', bold: false },
    ]);
  });

  it('handles empty input', () => {
    expect(parseWaiverSegments('')).toEqual([{ text: '', bold: false }]);
  });
});
