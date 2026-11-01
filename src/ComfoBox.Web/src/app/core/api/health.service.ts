import { inject, Injectable } from '@angular/core';

import type {
  ErrorEntryDto,
  HealthReport,
  StatusReport,
  WriteStatusDto,
} from '../models/status.model';
import { ApiClient } from './api-client';

/** Der Zugriff auf die Health-, Status- und Diagnose-Endpunkte. */
@Injectable({ providedIn: 'root' })
export class HealthService {
  private readonly api = inject(ApiClient);

  /** Lädt den Health-Bericht. */
  async getHealth(): Promise<HealthReport> {
    return this.api.get<HealthReport>('/health');
  }

  /** Lädt den vollständigen Laufzeitstatus. */
  async getStatus(): Promise<StatusReport> {
    return this.api.get<StatusReport>('/status');
  }

  /** Lädt die Fehlerhistorie (die letzten 50 Fehler). */
  async getErrors(): Promise<ErrorEntryDto[]> {
    return this.api.get<ErrorEntryDto[]>('/diagnostics/errors');
  }

  /** Lädt die Statistik des Schreibzählers. */
  async getWriteStats(): Promise<WriteStatusDto> {
    return this.api.get<WriteStatusDto>('/diagnostics/writes');
  }
}
