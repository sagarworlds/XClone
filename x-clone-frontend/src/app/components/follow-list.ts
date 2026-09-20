import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { ApiService } from '../services/api.service';
import { PagedList } from '../services/paged-list';
import { User } from '../models/types';
import { LoadMoreComponent } from './load-more';
import { SidebarComponent } from './sidebar';
import { UserRowComponent } from './user-row';
import { WidgetsComponent } from './widgets';

/**
 * Who follows a user and who they follow (`/profile/:username/followers` and `.../following`), newest follow first.
 * One page for both tabs, so switching between them does not fetch the profile again.
 */
@Component({
  selector: 'app-follow-list',
  standalone: true,
  imports: [RouterLink, SidebarComponent, WidgetsComponent, LoadMoreComponent, UserRowComponent],
  template: `
    <div class="app-container">
      <app-sidebar [active]="isOwnProfile() ? 'profile' : null" />

      <main class="main-content">
        @if (loadingProfile()) {
          <div class="state-message">Loading...</div>
        } @else if (!profile()) {
          <div class="state-message">
            <h3>User not found</h3>
            <p>The profile you are looking for does not exist.</p>
            <a routerLink="/home">Go Home</a>
          </div>
        } @else {
          <header class="header page-header">
            <a class="back-btn" [routerLink]="['/profile', profile()?.username]" aria-label="Back to profile">
              <span class="material-symbols-outlined">arrow_back</span>
            </a>
            <div class="header-titles">
              <h2>{{ profile()?.displayName }}</h2>
              <span class="handle">@{{ profile()?.username }}</span>
            </div>
          </header>

          <nav class="tabs">
            <a class="tab" [class.active]="tab() === 'followers'" [routerLink]="['/profile', profile()?.username, 'followers']">Followers</a>
            <a class="tab" [class.active]="tab() === 'following'" [routerLink]="['/profile', profile()?.username, 'following']">Following</a>
          </nav>

          @let people = activeList();
          @if (people.loading()) {
            <div class="state-message">Loading...</div>
          } @else if (people.items().length === 0) {
            <div class="state-message">
              @if (people.failed()) {
                <h3>Couldn't load the list</h3>
                <p>Check your connection and reload the page.</p>
              } @else if (tab() === 'followers') {
                <h3>No followers yet</h3>
                <p>When someone follows @{{ profile()?.username }}, they'll show up here.</p>
              } @else {
                <h3>Not following anyone yet</h3>
                <p>When @{{ profile()?.username }} follows someone, they'll show up here.</p>
              }
            </div>
          } @else {
            @for (person of people.items(); track person.id) {
              <app-user-row [user]="person" />
            }
            <app-load-more [list]="people" />
          }
        }
      </main>

      <app-widgets />
    </div>
  `,
  styles: [`
    .page-header {
      display: flex;
      align-items: center;
      gap: 24px;
    }
    .back-btn {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 36px;
      height: 36px;
      border-radius: 50%;
      color: var(--text-primary);
    }
    .back-btn:hover {
      background-color: rgba(231, 233, 234, 0.1);
      text-decoration: none;
    }
    .header-titles h2 {
      line-height: 1.2;
    }
    .handle {
      font-size: 0.8rem;
      color: var(--text-secondary);
    }
    .tabs {
      display: flex;
      border-bottom: 1px solid var(--border-color);
    }
    .tab {
      flex: 1;
      text-align: center;
      padding: 16px;
      font-weight: 700;
      font-size: 0.95rem;
      color: var(--text-secondary);
      border-bottom: 4px solid transparent;
    }
    .tab:hover {
      background-color: rgba(255, 255, 255, 0.03);
      text-decoration: none;
    }
    .tab.active {
      color: var(--text-primary);
      border-color: var(--accent-color);
    }
    app-user-row {
      display: block;
      padding: 12px 16px;
      border-bottom: 1px solid var(--border-color);
    }
    .state-message {
      text-align: center;
      padding: 40px 20px;
      color: var(--text-secondary);
    }
    .state-message h3 {
      color: var(--text-primary);
      margin-bottom: 8px;
    }
  `]
})
export class FollowListComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(ApiService);

  readonly profile = signal<User | null>(null);
  readonly loadingProfile = signal(true);
  readonly tab = signal<'followers' | 'following'>('followers');

  readonly followers = new PagedList<User>((cursor, take) => this.api.getFollowers(this.profile()!.id, cursor, take), (u) => String(u.id));
  readonly following = new PagedList<User>((cursor, take) => this.api.getFollowing(this.profile()!.id, cursor, take), (u) => String(u.id));
  readonly activeList = computed(() => (this.tab() === 'followers' ? this.followers : this.following));
  readonly isOwnProfile = computed(() => this.profile()?.id === this.api.currentUser()?.id);

  private routeSub?: Subscription;
  private profileRequest?: Subscription;

  ngOnInit(): void {
    // The username or the tab in the address changed
    this.routeSub = this.route.paramMap.subscribe((params) => {
      const username = params.get('username');
      if (!username) return;

      this.tab.set(params.get('list') === 'following' ? 'following' : 'followers');
      if (this.profile()?.username === username) {
        this.activeList().ensureLoaded(); // only the tab changed
      } else {
        this.loadProfile(username);
      }
    });
  }

  ngOnDestroy(): void {
    this.routeSub?.unsubscribe();
    this.profileRequest?.unsubscribe();
    this.followers.reset();
    this.following.reset();
  }

  private loadProfile(username: string): void {
    // A slow answer for the profile we just left must not replace the one we are on
    this.profileRequest?.unsubscribe();
    this.loadingProfile.set(true);
    this.profile.set(null);
    this.followers.reset();
    this.following.reset();

    this.profileRequest = this.api.getUserProfileByUsername(username).subscribe({
      next: (user) => {
        this.profile.set(user);
        this.loadingProfile.set(false);
        this.activeList().ensureLoaded();
      },
      error: () => this.loadingProfile.set(false),
    });
  }
}
