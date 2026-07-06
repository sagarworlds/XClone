import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../services/api.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="register-wrapper">
      <div class="register-card">
        <div class="logo">
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <path d="M18.244 2.25h3.308l-7.227 8.26 8.502 11.24H16.17l-5.214-6.817L4.99 21.75H1.68l7.73-8.835L1.254 2.25H8.08l4.713 6.231zm-1.161 17.52h1.833L7.084 4.126H5.117z"></path>
          </svg>
        </div>
        
        <h2>Create your account</h2>
        
        @if (error()) {
          <div class="error-banner">
            {{ error() }}
          </div>
        }

        <form (ngSubmit)="onSubmit()" #registerForm="ngForm">
          <div class="form-group">
            <input 
              type="text" 
              name="username" 
              [(ngModel)]="user.username" 
              required 
              placeholder="Username (e.g. johndoe)"
              class="form-input"
            />
          </div>

          <div class="form-group">
            <input 
              type="text" 
              name="displayName" 
              [(ngModel)]="user.displayName" 
              required 
              placeholder="Display Name (e.g. John Doe)"
              class="form-input"
            />
          </div>

          <div class="form-group">
            <input 
              type="email" 
              name="email" 
              [(ngModel)]="user.email" 
              required 
              placeholder="Email address"
              class="form-input"
            />
          </div>

          <div class="form-group">
            <input 
              type="password" 
              name="password" 
              [(ngModel)]="user.password" 
              required 
              placeholder="Password"
              class="form-input"
            />
          </div>

          <div class="form-group">
            <textarea 
              name="bio" 
              [(ngModel)]="user.bio" 
              placeholder="Bio (optional)"
              class="form-input text-area"
              rows="3"
            ></textarea>
          </div>

          <button 
            type="submit" 
            [disabled]="registerForm.invalid || loading()" 
            class="submit-btn"
          >
            @if (loading()) {
              Creating account...
            } @else {
              Sign up
            }
          </button>
        </form>

        <p class="login-prompt">
          Already have an account? <a routerLink="/login">Log in</a>
        </p>
      </div>
    </div>
  `,
  styles: [`
    .register-wrapper {
      display: flex;
      justify-content: center;
      align-items: center;
      min-height: 100vh;
      background-color: #000;
      color: #fff;
      font-family: inherit;
      padding: 20px;
    }
    .register-card {
      width: 100%;
      max-width: 450px;
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
      gap: 16px;
    }
    .form-group {
      position: relative;
    }
    .form-input {
      width: 100%;
      padding: 14px;
      font-size: 0.95rem;
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
    .text-area {
      resize: none;
    }
    .submit-btn {
      background-color: #fff;
      color: #000;
      font-size: 1rem;
      font-weight: 700;
      padding: 14px;
      border-radius: 9999px;
      border: none;
      margin-top: 10px;
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
    .login-prompt {
      margin-top: 24px;
      text-align: center;
      color: #71767b;
      font-size: 0.95rem;
    }
    .login-prompt a {
      color: #1d9bf0;
      text-decoration: none;
    }
    .login-prompt a:hover {
      text-decoration: underline;
    }
  `]
})
export class RegisterComponent {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  user = {
    username: '',
    email: '',
    password: '',
    displayName: '',
    bio: ''
  };

  loading = signal(false);
  error = signal<string | null>(null);

  onSubmit(): void {
    this.loading.set(true);
    this.error.set(null);

    this.api.register(this.user).subscribe({
      next: () => {
        this.router.navigate(['/home']);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err.error?.message || 'Error occurred during registration. Try a different username/email.');
      }
    });
  }
}
