import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AuthResponse } from './cv-types';

type AuthMode = 'login' | 'register';

@Component({
  selector: 'app-auth-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSnackBarModule
  ],
  templateUrl: './auth-page.component.html',
  styleUrl: './auth-page.component.scss'
})
export class AuthPageComponent {
  private readonly http = inject(HttpClient);
  private readonly snackBar = inject(MatSnackBar);

  @Input({ required: true }) apiBaseUrl = '';
  @Output() authenticated = new EventEmitter<AuthResponse>();

  mode: AuthMode = 'login';
  isSubmitting = false;

  companyName = '';
  fullName = '';
  email = '';
  password = '';

  setMode(mode: AuthMode): void {
    this.mode = mode;
  }

  submit(): void {
    if (this.isSubmitting) {
      return;
    }

    if (!this.email.trim() || !this.password.trim()) {
      this.showError('Email and password are required.');
      return;
    }

    if (this.mode === 'register') {
      if (!this.companyName.trim() || !this.fullName.trim()) {
        this.showError('Company name and full name are required.');
        return;
      }

      if (this.password.trim().length < 8) {
        this.showError('Password must be at least 8 characters long.');
        return;
      }
    }

    this.isSubmitting = true;
    const request = this.mode === 'register'
      ? this.http.post<AuthResponse>(`${this.apiBaseUrl}/auth/register`, {
          companyName: this.companyName.trim(),
          fullName: this.fullName.trim(),
          email: this.email.trim(),
          password: this.password.trim()
        })
      : this.http.post<AuthResponse>(`${this.apiBaseUrl}/auth/login`, {
          email: this.email.trim(),
          password: this.password.trim()
        });

    request.subscribe({
      next: response => {
        this.isSubmitting = false;
        this.authenticated.emit(response);
      },
      error: error => {
        this.isSubmitting = false;
        this.showError((error?.error?.message as string) ?? 'Authentication failed.');
      }
    });
  }

  private showError(message: string): void {
    this.snackBar.open(message, 'Close', { duration: 4500 });
  }
}
