import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';

import { API_BASE_URL } from '../api-config';
import { AuthResponse, AuthenticatedUser, LoginRequest } from '../models/auth.models';

const STORAGE_KEY = 'automargin.session';

interface StoredSession {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: AuthenticatedUser;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly session = signal<StoredSession | null>(this.restore());

  readonly user = computed(() => this.session()?.user ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);

  get accessToken(): string | null {
    return this.session()?.accessToken ?? null;
  }

  get refreshToken(): string | null {
    return this.session()?.refreshToken ?? null;
  }

  hasRole(role: string): boolean {
    return this.session()?.user.roles.includes(role) ?? false;
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${API_BASE_URL}/api/auth/login`, request)
      .pipe(tap((response) => this.store(response)));
  }

  /** Canjea el refresh token. El backend rota el token, así que la sesión se reemplaza entera. */
  refresh(): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${API_BASE_URL}/api/auth/refresh`, { refreshToken: this.refreshToken })
      .pipe(tap((response) => this.store(response)));
  }

  logout(redirect = true): void {
    const token = this.refreshToken;

    // Se limpia la sesión local primero: aunque el servidor no responda, el usuario queda fuera.
    this.session.set(null);
    localStorage.removeItem(STORAGE_KEY);

    if (token) {
      this.http
        .post(`${API_BASE_URL}/api/auth/logout`, { refreshToken: token })
        .subscribe({ error: () => undefined });
    }

    if (redirect) this.router.navigate(['/login']);
  }

  private store(response: AuthResponse): void {
    const session: StoredSession = {
      accessToken: response.accessToken,
      refreshToken: response.refreshToken,
      expiresAt: response.expiresAt,
      user: response.user
    };

    this.session.set(session);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
  }

  private restore(): StoredSession | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return null;

      const session = JSON.parse(raw) as StoredSession;

      // Una sesión expirada no sirve; el refresh token se intenta al primer 401.
      return session.accessToken ? session : null;
    } catch {
      localStorage.removeItem(STORAGE_KEY);
      return null;
    }
  }
}
