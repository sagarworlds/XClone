import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../services/api.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="login-wrapper">
      <div class="login-card">
        <div class="logo">
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <path d="M18.244 2.25h3.308l-7.227 8.26 8.502 11.24H16.17l-5.214-6.817L4.99 21.75H1.68l7.73-8.835L1.254 2.25H8.08l4.713 6.231zm-1.161 17.52h1.833L7.084 4.126H5.117z"></path>
          </svg>
        </div>
        
        <h2>Sign in to X</h2>
        
        @if (error()) {
          <div class="error-banner">
            {{ error() }}
          </div>
        }

        <form (ngSubmit)="onSubmit()" #loginForm="ngForm">
          <div class="form-group">
            <input 
              type="text" 
              name="usernameOrEmail" 
              [(ngModel)]="credentials.usernameOrEmail" 
              required 
              placeholder="Username or Email"
              class="form-input"
            />
          </div>

          <div class="form-group">
            <input 
              type="password" 
              name="password" 
              [(ngModel)]="credentials.password" 
              required 
              placeholder="Password"
              class="form-input"
            />
          </div>

          <button 
            type="submit" 
            [disabled]="loginForm.invalid || loading()" 
            class="submit-btn"
          >
            @if (loading()) {
              Logging in...
            } @else {
              Log in
            }
          </button>
        </form>

        <p class="signup-prompt">
          Don't have an account? <a routerLink="/register">Sign up</a>
        </p>
      </div>
    </div>
  `,
  styles: [`
    .login-wrapper {
      display: flex;
      justify-content: center;
      align-items: center;
      min-height: 100vh;
      background-color: #000;
      color: #fff;
      font-family: inherit;
    }
    .login-card {
      width: 100%;
      max-width: 400px;
      padding: 40px 30px;
      border-radius: 16px;
      background: rgba(21, 24, 28, 0.8);
      backdrop-filter: blur(10px);
      border: 1px solid #2f3336;
      display: flex;
      flex-direction: column;
      align-items: stretch;
      box-shadow: 0 8px 32px rgba(0, 0, 0, 0.5);
    }
    .logo {
      display: flex;
      justify-content: center;
      margin-bottom: 20px;
    }
    .logo svg {
      width: 45px;
      height: 45px;
      fill: #fff;
    }
    h2 {
      font-size: 1.8rem;
      font-weight: 800;
      margin-bottom: 30px;
      text-align: center;
    }
    form {
      display: flex;
      flex-direction: column;
      gap: 20px;
    }
    .form-group {
      position: relative;
    }
    .form-input {
      width: 100%;
      padding: 16px;
      font-size: 1rem;
      background-color: transparent;
      border: 1px solid #2f3336;
      border-radius: 8px;
      color: #fff;
      transition: border-color 0.2s;
    }
    .form-input:focus {
      border-color: #1d9bf0;
      outline: none;
    }
    .submit-btn {
      background-color: #fff;
      color: #000;
      font-size: 1rem;
      font-weight: 700;
      padding: 14px;
      border-radius: 9999px;
      border: none;
      transition: background-color 0.2s, transform 0.1s;
    }
    .submit-btn:hover:not(:disabled) {
      background-color: #e6e6e6;
    }
    .submit-btn:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }
    .error-banner {
      background-color: #f4212e;
      color: #fff;
      padding: 12px;
      border-radius: 8px;
      margin-bottom: 20px;
      font-size: 0.9rem;
      font-weight: 500;
      text-align: center;
    }
    .signup-prompt {
      margin-top: 30px;
      text-align: center;
      color: #71767b;
      font-size: 0.95rem;
    }
    .signup-prompt a {
      color: #1d9bf0;
      text-decoration: none;
    }
    .signup-prompt a:hover {
      text-decoration: underline;
    }
  `]
})
export class LoginComponent {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  credentials = {
    usernameOrEmail: '',
    password: ''
  };

  loading = signal(false);
  error = signal<string | null>(null);

  onSubmit(): void {
    this.loading.set(true);
    this.error.set(null);

    this.api.login(this.credentials).subscribe({
      next: () => {
        this.router.navigate(['/home']);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err.error?.message || 'Invalid username/email or password');
      }
    });
  }
}
