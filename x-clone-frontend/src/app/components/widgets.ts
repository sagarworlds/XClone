import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../services/api.service';
import { TrendingHashtag, User } from '../models/types';
import { UserRowComponent } from './user-row';

/** Right column shared by every page: user search and "Who to follow". */
@Component({
  selector: 'app-widgets',
  standalone: true,
  imports: [FormsModule, RouterLink, UserRowComponent],
  // The host must not create its own box, so the <aside> stays a direct flex child of .app-container.
  styles: [`
    :host { display: contents; }
    app-user-row {
      display: block;
      margin-bottom: 16px;
    }
    app-user-row:last-child {
      margin-bottom: 0;
    }
    .muted {
      color: var(--text-secondary);
    }
    .trend {
      display: flex;
      flex-direction: column;
      margin-bottom: 16px;
      color: var(--text-primary);
    }
    .trend:last-child {
      margin-bottom: 0;
    }
    .trend:hover {
      text-decoration: none;
    }
    .trend:hover .trend-tag {
      text-decoration: underline;
    }
    .trend-tag {
      font-weight: 700;
      overflow-wrap: anywhere;
    }
    .trend-count {
      color: var(--text-secondary);
      font-size: 0.85rem;
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
            <app-user-row [user]="user" [followButton]="false" />
          }
        </div>
      }

      @if (trends().length > 0) {
        <div class="widget-card">
          <h3>Trends</h3>
          @for (trend of trends(); track trend.tag) {
            <a class="trend" [routerLink]="['/hashtag', trend.tag]">
              <span class="trend-tag">#{{ trend.tag }}</span>
              <span class="trend-count">{{ trend.postsCount }} {{ trend.postsCount === 1 ? 'post' : 'posts' }}</span>
            </a>
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
            <app-user-row [user]="user" />
          }
        }
      </div>
    </aside>
  `
})
export class WidgetsComponent implements OnInit {
  private readonly api = inject(ApiService);

  searchQuery = '';
  searchResults = signal<User[]>([]);
  suggestions = signal<User[]>([]);
  trends = signal<TrendingHashtag[]>([]);
  loadingSuggestions = signal(true);

  ngOnInit(): void {
    // Trends are a nicety: when they cannot be fetched the card is simply not there
    this.api.getTrendingHashtags(5).subscribe({
      next: (trends) => this.trends.set(trends),
      error: () => this.trends.set([]),
    });

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
}
