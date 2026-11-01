import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { ApiErrorDto } from '../models/token.model';

/** Die Basis-URL der REST-Schnittstelle (relativ → derselbe Host). */
export const API_BASE_URL = '/api';

/**
 * Ein Fehler der REST-Schnittstelle.
 *
 * <p>Der Fehlercode (`error`) und die Meldung (`message`) stammen aus dem
 * Gateway - die Oberfläche zeigt **beides** an: den Code als technische
 * Angabe, die Meldung als Klartext für den Anwender.
 */
export class ApiError extends Error {
  /** Der stabile Fehlercode, z. B. `value_out_of_range`. */
  readonly code: string;
  /** Die Id des betroffenen Items (bei Item-Fehlern). */
  readonly itemId: string | null;
  /** Die Anzahl der Schreibvorgänge im 24-h-Fenster. */
  readonly writesLast24h: number | null;
  /** Das Limit der Schreibvorgänge im 24-h-Fenster. */
  readonly maxWritesPer24h: number | null;
  /** Die untere Grenze des zulässigen Bereichs. */
  readonly min: number | null;
  /** Die obere Grenze des zulässigen Bereichs. */
  readonly max: number | null;
  /** Der Hinweis für die Oberfläche, z. B. `no_iam`. */
  readonly hint: string | null;
  /** Der HTTP-Statuscode (oder `0`, wenn die Anfrage gar nicht ankam). */
  readonly status: number;

  constructor(dto: ApiErrorDto, status: number) {
    super(dto.message);
    this.name = 'ApiError';
    this.code = dto.error;
    this.itemId = dto.itemId ?? null;
    this.writesLast24h = dto.writesLast24h ?? null;
    this.maxWritesPer24h = dto.maxWritesPer24h ?? null;
    this.min = dto.min ?? null;
    this.max = dto.max ?? null;
    this.hint = dto.hint ?? null;
    this.status = status;
  }
}

/** Wandelt einen beliebigen Fehler in einen {@link ApiError}. */
export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  if (error instanceof HttpErrorResponse) {
    const body = error.error as Partial<ApiErrorDto> | null;

    if (body && typeof body.error === 'string' && typeof body.message === 'string') {
      return new ApiError(
        {
          error: body.error,
          message: body.message,
          itemId: body.itemId ?? null,
          writesLast24h: body.writesLast24h ?? null,
          maxWritesPer24h: body.maxWritesPer24h ?? null,
          min: body.min ?? null,
          max: body.max ?? null,
          hint: body.hint ?? null,
        },
        error.status,
      );
    }

    // Kein Fehlerobjekt vom Server (z. B. ein 502 vom Proxy).
    return new ApiError(
      {
        error: 'http_error',
        message:
          error.status === 0
            ? 'Der Gateway ist nicht erreichbar. Bitte die Verbindung prüfen.'
            : `Die Anfrage ist fehlgeschlagen (HTTP ${error.status}).`,
      },
      error.status,
    );
  }

  return new ApiError(
    { error: 'unknown_error', message: 'Es ist ein unbekannter Fehler aufgetreten.' },
    0,
  );
}

/**
 * Der einzige Zugriffspunkt auf die REST-Schnittstelle des Gateways.
 *
 * <p>Alle Fehler werden in {@link ApiError} übersetzt - die Aufrufer müssen
 * sich nicht mit `HttpErrorResponse` herumschlagen. Die Basis-URL ist
 * **relativ**: der Browser spricht immer denselben Host an (kein CORS, kein
 * Reverse-Proxy).
 */
@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);

  /** Führt ein GET aus. */
  async get<T>(path: string, params?: Record<string, string | number | boolean>): Promise<T> {
    const options = this.toParams(params);

    if (options) {
      return firstValueFrom(this.http.get<T>(this.url(path), { params: options }));
    }

    return firstValueFrom(this.http.get<T>(this.url(path)));
  }

  /** Führt ein GET aus und liefert den Rohtext (für die Hilfe-HTML). */
  async getText(path: string): Promise<string> {
    return firstValueFrom(
      this.http.get(this.url(path), { responseType: 'text' as const }),
    );
  }

  /** Führt ein PUT aus. */
  async put<T>(path: string, body: unknown): Promise<T> {
    return firstValueFrom(this.http.put<T>(this.url(path), body));
  }

  /** Führt ein POST aus. */
  async post<T>(path: string, body?: unknown): Promise<T> {
    return firstValueFrom(this.http.post<T>(this.url(path), body ?? {}));
  }

  /** Führt ein DELETE aus. */
  async delete<T>(path: string): Promise<T> {
    return firstValueFrom(this.http.delete<T>(this.url(path)));
  }

  /** Erzeugt die vollständige URL zu einem API-Pfad. */
  url(path: string): string {
    const normalized = path.startsWith('/') ? path : `/${path}`;
    return `${API_BASE_URL}${normalized}`;
  }

  /** Wandelt eine einfache Map in HTTP-Parameter. */
  private toParams(params?: Record<string, string | number | boolean>): HttpParams | undefined {
    if (!params) {
      return undefined;
    }

    let result = new HttpParams();

    for (const [key, value] of Object.entries(params)) {
      result = result.set(key, String(value));
    }

    return result;
  }}
