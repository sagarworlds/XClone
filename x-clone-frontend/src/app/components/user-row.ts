import { Component, computed, inject, input, linkedSignal, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../services/api.service';
import { User } from '../models/types';

/**
 * A person in a list (search results, "Who to follow", followers, following): avatar, names, and a follow button.
 * The button is optimistic and reverts on error; you never get one on your own row.
 */
@Component({
  selector: 'app-user-row',
  standalone: true,
  imports: [RouterLink],
  styles: [`
    .user-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
    }
    .user-details {
      display: flex;
      align-items: center;
      cursor: pointer;
      flex-grow: 1;
      min-width: 0;
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
  `],
  template: `
    @let u = state();
    <div class="user-row">
      <div class="user-details" [routerLink]="['/profile', u.username]">
        <img [src]="u.avatarUrl || defaultAvatar" alt="Avatar" class="avatar" />
        <div class="user-info">
          <span class="display-name">{{ u.displayName }}</span>
          <span class="username">@{{ u.username }}</span>
        </div>
      </div>
      @if (canFollow()) {
        <button class="follow-btn" [class.following]="u.isFollowed" (click)="toggleFollow()">
          {{ u.isFollowed ? 'Following' : 'Follow' }}
        </button>
      }
    </div>
  `,
})
export class UserRowComponent {
  private readonly api = inject(ApiService);

  readonly user = input.required<User>();
  /** Set to false where the row is only a link to the profile (search results). */
  readonly followButton = input(true);
  /** Emits the user after the follow state changed (optimistically, and again when it is reverted). */
  readonly changed = output<User>();

  readonly defaultAvatar = 'https://abs.twimg.com/sticky/default_profile_images/default_profile_normal.png';

  // Local copy so the optimistic update re-renders; it resets whenever the parent passes another user.
  readonly state = linkedSignal(() => this.user());
  readonly canFollow = computed(() => this.followButton() && this.state().id !== this.api.currentUser()?.id);

  toggleFollow(): void {
    const before = this.state();
    this.update({ ...before, isFollowed: !before.isFollowed });
    (before.isFollowed ? this.api.unfollowUser(before.id) : this.api.followUser(before.id)).subscribe({
      error: () => this.update(before),
    });
  }

  private update(next: User): void {
    this.state.set(next);
    this.changed.emit(next);
  }
}
