import { Component, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../services/api.service';
import { NotificationsService } from '../services/notifications.service';

/** Left navigation column shared by every page. Layout styles live in the global stylesheet. */
@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink],
  // The host must not create its own box, so the <aside> stays a direct flex child of .app-container.
  styles: [`
    :host { display: contents; }
    .icon-wrap {
      position: relative;
      display: inline-flex;
    }
    .badge {
      position: absolute;
      top: -6px;
      right: -8px;
      min-width: 18px;
      height: 18px;
      padding: 0 5px;
      border: 2px solid var(--bg-primary);
      border-radius: 9999px;
      background-color: var(--accent-color);
      color: #fff;
      font-size: 0.7rem;
      font-weight: 700;
      line-height: 14px;
      text-align: center;
      box-sizing: content-box;
    }
  `],
  template: `
    <aside class="sidebar">
      <div>
        <div class="logo-container">
          <svg viewBox="0 0 24 24" aria-hidden="true" style="width: 32px; height: 32px; fill: currentColor;">
            <path d="M18.244 2.25h3.308l-7.227 8.26 8.502 11.24H16.17l-5.214-6.817L4.99 21.75H1.68l7.73-8.835L1.254 2.25H8.08l4.713 6.231zm-1.161 17.52h1.833L7.084 4.126H5.117z"></path>
          </svg>
        </div>

        <nav class="nav-links">
          <a routerLink="/home" class="nav-item" [class.active]="active() === 'home'">
            <span class="material-symbols-outlined">home</span>
            <span>Home</span>
          </a>
          <a routerLink="/notifications" class="nav-item" [class.active]="active() === 'notifications'">
            <span class="icon-wrap">
              <span class="material-symbols-outlined">notifications</span>
              @if (unreadCount() > 0) {
                <span class="badge" [attr.aria-label]="unreadCount() + ' unread'">{{ unreadCount() > 99 ? '99+' : unreadCount() }}</span>
              }
            </span>
            <span>Notifications</span>
          </a>
          @if (currentUser()) {
            <a [routerLink]="['/profile', currentUser()?.username]" class="nav-item" [class.active]="active() === 'profile'">
              <span class="material-symbols-outlined">person</span>
              <span>Profile</span>
            </a>
          }
          <button (click)="api.logout()" class="nav-item logout-btn" style="background: transparent; width: 100%; text-align: left;">
            <span class="material-symbols-outlined">logout</span>
            <span>Log out</span>
          </button>
        </nav>
      </div>

      @if (currentUser()) {
        <div class="user-profile-summary" [routerLink]="['/profile', currentUser()?.username]">
          <img [src]="currentUser()?.avatarUrl || defaultAvatar" alt="Avatar" class="avatar" />
          <div class="user-info">
            <span class="display-name">{{ currentUser()?.displayName }}</span>
            <span class="username">@{{ currentUser()?.username }}</span>
          </div>
        </div>
      }
    </aside>
  `
})
export class SidebarComponent {
  protected readonly api = inject(ApiService);

  /** Which nav item to highlight. */
  readonly active = input<'home' | 'notifications' | 'profile' | null>(null);

  readonly currentUser = this.api.currentUser;
  // Injecting the service is also what starts it polling for new notifications
  readonly unreadCount = inject(NotificationsService).unreadCount;
  readonly defaultAvatar = 'https://abs.twimg.com/sticky/default_profile_images/default_profile_normal.png';
}
