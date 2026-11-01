import { inject, Injectable } from '@angular/core';

import type { ItemDto } from '../models/item.model';
import { ApiClient } from './api-client';

/** Der Zugriff auf die Item-Endpunkte. */
@Injectable({ providedIn: 'root' })
export class ItemsService {
  private readonly api = inject(ApiClient);

  /** Lädt den gesamten Katalog mit den aktuellen Werten. */
  async getAll(): Promise<ItemDto[]> {
    return this.api.get<ItemDto[]>('/items');
  }

  /** Lädt ein einzelnes Item. */
  async getById(id: string): Promise<ItemDto> {
    return this.api.get<ItemDto>(`/items/${encodeURIComponent(id)}`);
  }

  /** Lädt alle Items einer Gruppe. */
  async getByGroup(group: string): Promise<ItemDto[]> {
    return this.api.get<ItemDto[]>(`/items/group/${encodeURIComponent(group)}`);
  }

  /**
   * Schreibt einen Wert.
   *
   * <p>Der Wert wird **ohne** optimistisches Update gesendet: erst wenn der
   * Server mit `204` antwortet, gilt der Wert als angenommen. Bei einem Fehler
   * bleibt der Serverwert maßgeblich.
   *
   * @param id Die Item-Id.
   * @param value Der Wert (Zahl oder Enum-Name).
   */
  async write(id: string, value: number | string): Promise<void> {
    await this.api.put<void>(`/items/${encodeURIComponent(id)}`, { value });
  }
}
