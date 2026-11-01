import { inject, Injectable } from '@angular/core';

import type { CreatedTokenDto, CreateTokenRequest, TokenInfoDto } from '../models/token.model';
import { ApiClient } from './api-client';

/** Der Zugriff auf die Token-Verwaltung. */
@Injectable({ providedIn: 'root' })
export class TokensService {
  private readonly api = inject(ApiClient);

  /** Lädt die Metadaten aller Token (ohne Klartext). */
  async getAll(): Promise<TokenInfoDto[]> {
    return this.api.get<TokenInfoDto[]>('/security/tokens');
  }

  /**
   * Erzeugt ein neues Token.
   *
   * <p>Der Klartext ist **nur** in der Antwort sichtbar - er wird nie wieder
   * angezeigt.
   */
  async create(request: CreateTokenRequest): Promise<CreatedTokenDto> {
    return this.api.post<CreatedTokenDto>('/security/tokens', request);
  }

  /** Widerruft ein Token. */
  async remove(id: string): Promise<void> {
    await this.api.delete<void>(`/security/tokens/${encodeURIComponent(id)}`);
  }
}
