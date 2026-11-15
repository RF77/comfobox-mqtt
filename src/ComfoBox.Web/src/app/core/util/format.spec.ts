import { describe, expect, it } from 'vitest';

import { formatNumber } from './format';

describe('formatNumber', () => {
  it('verwendet den Apostroph als Tausendertrennzeichen', () => {
    expect(formatNumber(1234.5)).toBe("1'234,5");
    expect(formatNumber(1_234_567)).toBe("1'234'567");
  });

  it('lässt kleine Zahlen ohne Gruppierung', () => {
    expect(formatNumber(21.5)).toBe('21,5');
    expect(formatNumber(0)).toBe('0');
  });

  it('begrenzt die Nachkommastellen', () => {
    expect(formatNumber(21.567, 2)).toBe('21,57');
    expect(formatNumber(21.567, 0)).toBe('22');
  });

  it('liefert für fehlende Werte einen Gedankenstrich', () => {
    expect(formatNumber(null)).toBe('–');
    expect(formatNumber(undefined)).toBe('–');
  });
});
