import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../services/api.service';
import { User } from '../models/types';

/** Right column shared by every page: user search and "Who to follow". */
@Component({
  selector: 'app-widgets',
  standalone: true,
  imports: [FormsModule, RouterLink],
  // The host must not create its own box, so the <aside> stays a direct flex child of .app-container.
  styles: [`
    :host { display: contents; }
    .user-row {
      align-items: center;
      justify-content: space-between;
      gap: 12px;
    }
    .user-details {
      display: flex;
      align-items: center;
      cursor: pointer;
      flex-grow: 1;
    }
    .user-details:hover .display-name {
      text-decoration: underline;
    }
    .follow-btn {
      background-color: var(--text-primary);
      color: var(--bg-primary);
      font-weight: 700;
      font-size: 0.85rem;
      padding: 6px 16px;
      border-radius: 9999px;
    }
    .follow-btn:hover {
      opacity: 0.9;
    }
    .follow-btn.following {
      background-color: transparent;
      color: var(--text-primary);
      border: 1px solid var(--border-color);
    }
    .follow-btn.following:hover {
      background-color: rgba(244, 33, 46, 0.1);
      color: var(--danger-color);
      border-color: var(--danger-color);
    }
    .follow-btn.following:hover::after {
      content: 'Unfollow';
    }
    .muted {
      color: var(--text-secondary);
    }
  `],
  template: `
    <aside class="widgets">
      <div class="search-box">
        <span class="material-symbols-outlined">search</span>
        <input
          type="text"
          [(ngModel)]="searchQuery"
          (input)="onSearchChange()"
          placeholder="Search users..."
        />
      </div>

      @if (searchResults().length > 0) {
        <div class="widget-card">
          <h3>Search Results</h3>
          @for (user of searchResults(); track user.id) {
            <div class="widget-item user-row">
              <div class="user-details" [routerLink]="['/profile', user.username]">
                <img [src]="user.avatarUrl || defaultAvatar" alt="Avatar" class="avatar" />
                <div class="user-info">
                  <span class="display-name">{{ user.displayName }}</span>
                  <span class="username">@{{ user.username }}</span>
                </div>
              </div>
            </div>
          }
        </div>
      }

      <div class="widget-card">
        <h3>Who to follow</h3>
        @if (loadingSuggestions()) {
          <div class="muted">Loading recommendations...</div>
        } @else if (suggestions().length === 0) {
          <div class="muted">No recommendations found.</div>
        } @else {
          @for (user of suggestions(); track user.id) {
            <div class="widget-item user-row">
              <div class="user-details" [routerLink]="['/profile', user.username]">
                <img [src]="user.avatarUrl || defaultAvatar" alt="Avatar" class="avatar" />
                <div class="user-info">
                  <span class="display-name">{{ user.displayName }}</span>
                  <span class="username">@{{ user.username }}</span>
                </div>
              </div>
              <button class="follow-btn" [class.following]="user.isFollowed" (click)="toggleFollow(user)">
                {{ user.isFollowed ? 'Following' : 'Follow' }}
              </button>
            </div>
          }
        }
      </div>
    </aside>
  `
})
export class WidgetsComponent implements OnInit {
  private readonly api = inject(ApiService);

  readonly defaultAvatar = 'https://abs.twimg.com/sticky/default_profile_images/default_profile_normal.png';

  searchQuery = '';
  searchResults = signal<User[]>([]);
  suggestions = signal<User[]>([]);
  loadingSuggestions = signal(true);

  ngOnInit(): void {
    this.api.getSuggestions(4).subscribe({
      next: (users) => {
        this.suggestions.set(users);
        this.loadingSuggestions.set(false);
      },
      error: () => this.loadingSuggestions.set(false),
    });
  }

  onSearchChange(): void {
    if (!this.searchQuery.trim()) {
      this.searchResults.set([]);
      return;
    }
    this.api.searchUsers(this.searchQuery).subscribe({
      next: (users) => this.searchResults.set(users),
    });
  }

  toggleFollow(user: User): void {
    const wasFollowing = user.isFollowed;
    const setFollowed = (value: boolean) =>
      this.suggestions.update((list) => list.map((u) => (u.id === user.id ? { ...u, isFollowed: value } : u)));

    setFollowed(!wasFollowing);
    (wasFollowing ? this.api.unfollowUser(user.id) : this.api.followUser(user.id)).subscribe({
      error: () => setFollowed(wasFollowing),
    });
  }
}
