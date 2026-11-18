import { describe, expect, it } from 'vitest';

import { trailingGroupsLast } from './group-order';

describe('trailingGroupsLast', () => {
  it('schiebt die Test-Gruppe ans Ende', () => {
    expect(trailingGroupsLast(['Zone', 'Tests', 'Warmwater'])).toEqual([
      'Zone',
      'Warmwater',
      'Tests',
    ]);
  });

  it('lässt die Reihenfolge der übrigen Gruppen unverändert', () => {
    expect(trailingGroupsLast(['Zone', 'Warmwater', 'Status/Zone'])).toEqual([
      'Zone',
      'Warmwater',
      'Status/Zone',
    ]);
  });

  it('verträgt eine fehlende Test-Gruppe', () => {
    expect(trailingGroupsLast([])).toEqual([]);
    expect(trailingGroupsLast(['Zone'])).toEqual(['Zone']);
  });

  it('lässt eine Liste aus lauter Test-Gruppen unverändert', () => {
    expect(trailingGroupsLast(['Tests'])).toEqual(['Tests']);
  });
});
