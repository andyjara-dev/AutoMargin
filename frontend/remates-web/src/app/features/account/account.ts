import { Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';

import { API_BASE_URL } from '../../core/api-config';
import { describeHttpError } from '../../core/http-error';
import { AuthService } from '../../core/services/auth.service';

/**
 * Refleja la política de Identity en el navegador para avisar mientras se escribe.
 * La validación real la hace el servidor: esto solo evita el viaje en el caso obvio.
 */
function passwordPolicy(control: AbstractControl): ValidationErrors | null {
  const value = control.value as string;
  if (!value) return null;

  const problems: string[] = [];
  if (value.length < 10) problems.push('al menos 10 caracteres');
  if (!/[A-Z]/.test(value)) problems.push('una mayúscula');
  if (!/[a-z]/.test(value)) problems.push('una minúscula');
  if (!/[0-9]/.test(value)) problems.push('un número');

  return problems.length ? { policy: problems } : null;
}

@Component({
  selector: 'app-account',
  imports: [ReactiveFormsModule],
  templateUrl: './account.html',
  styleUrl: './account.scss'
})
export class Account {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);

  readonly user = this.auth.user;
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly done = signal(false);

  readonly form = this.fb.nonNullable.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, passwordPolicy]],
    confirmPassword: ['', Validators.required]
  });

  /** Requisitos que faltan, para mostrarlos mientras se escribe. */
  readonly missing = signal<string[]>([]);

  constructor() {
    this.form.controls.newPassword.valueChanges.subscribe(() => {
      const errors = this.form.controls.newPassword.errors;
      this.missing.set((errors?.['policy'] as string[]) ?? []);
    });
  }

  get mismatch(): boolean {
    const { newPassword, confirmPassword } = this.form.getRawValue();
    return confirmPassword.length > 0 && newPassword !== confirmPassword;
  }

  submit(): void {
    if (this.form.invalid || this.mismatch || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.done.set(false);

    const { currentPassword, newPassword } = this.form.getRawValue();

    this.http
      .post(`${API_BASE_URL}/api/auth/change-password`, { currentPassword, newPassword })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.done.set(true);
          this.form.reset({ currentPassword: '', newPassword: '', confirmPassword: '' });
          this.missing.set([]);
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(describeHttpError(err, 'No se pudo cambiar la contraseña.'));
        }
      });
  }
}
