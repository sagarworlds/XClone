import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { ApiService } from '../services/api.service';
import { PagedList } from '../services/paged-list';
import { Post, User } from '../models/types';
import { LoadMoreComponent } from './load-more';
import { PostCardComponent } from './post-card';
import { SidebarComponent } from './sidebar';
import { UserRowComponent } from './user-row';
import { WidgetsComponent } from './widgets';

/**
 * What matches a query (`/search?q=...`), in a Posts and a People tab (`?tab=posts|people`, Posts by default).
 * Posts page with Load more; people come from the existing (unpaged) user search.
 */
@Component({
  selector: 'app-search',
  standalone: true,
  imports: [RouterLink, SidebarComponent, WidgetsComponent, PostCardComponent, LoadMoreComponent, UserRowComponent],
  template: `
    <div class="app-container">
      <app-sidebar [active]="null" />

      <main class="main-content">
        <header class="header page-header">
          <a class="back-btn" routerLink="/home" aria-label="Back to home">
            <span class="material-symbols-outlined">arrow_back</span>
          </a>
          <div class="header-titles">
            <h2>Search</h2>
            @if (query()) {
              <span class="handle">{{ query() }}</span>
            }
          </div>
        </header>

        @if (!query()) {
          <div class="state-message">
            <h3>Search XClone</h3>
            <p>Find posts and people from the box on the right.</p>
          </div>
        } @else {
          <nav class="tabs">
            <a class="tab" [class.active]="tab() === 'posts'" [routerLink]="['/search']" [queryParams]="{ q: query(), tab: 'posts' }">Posts</a>
            <a class="tab" [class.active]="tab() === 'people'" [routerLink]="['/search']" [queryParams]="{ q: query(), tab: 'people' }">People</a>
          </nav>

          @if (tab() === 'posts') {
            @if (postList.loading()) {
              <div class="state-message">Loading...</div>
            } @else if (postList.items().length === 0) {
              <div class="state-message">
                @if (postList.failed()) {
                  <h3>Couldn't load the results</h3>
                  <p>Check your connection and reload the page.</p>
                } @else {
                  <h3>No posts found</h3>
                  <p>Nothing matches "{{ query() }}".</p>
                }
              </div>
            } @else {
              @for (post of postList.items(); track post.id) {
                <app-post-card [post]="post" (deleted)="onPostDeleted($event)" />
              }
              <app-load-more [list]="postList" />
            }
          } @else {
            @if (loadingPeople()) {
              <div class="state-message">Loading...</div>
            } @else if (people().length === 0) {
              <div class="state-message">
                @if (peopleFailed()) {
                  <h3>Couldn't load the results</h3>
                  <p>Check your connection and reload the page.</p>
                } @else {
                  <h3>No people found</h3>
                  <p>Nobody matches "{{ query() }}".</p>
                }
              </div>
            } @else {
              @for (user of people(); track user.id) {
                <app-user-row [user]="user" />
              }
            }
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
      overflow-wrap: anywhere;
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
      overflow-wrap: anywhere;
    }
  `],
})
export class SearchComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(ApiService);

  readonly query = signal('');
  readonly tab = signal<'posts' | 'people'>('posts');
  readonly postList = new PagedList<Post>((cursor, take) => this.api.searchPosts(this.query(), cursor, take), (post) => String(post.id));
  readonly people = signal<User[]>([]);
  readonly loadingPeople = signal(false);
  readonly peopleFailed = signal(false);

  private routeSub?: Subscription;
  private peopleRequest?: Subscription;

  ngOnInit(): void {
    this.routeSub = this.route.queryParamMap.subscribe((params) => {
      this.tab.set(params.get('tab') === 'people' ? 'people' : 'posts');

      const q = (params.get('q') ?? '').trim();
      if (q === this.query()) return; // only the tab changed; both lists are already loaded (or loading)
      this.query.set(q);

      if (q) {
        this.postList.loadFirst(); // also forgets the previous query's posts and cancels its request
        this.loadPeople(q);
      } else {
        this.postList.reset();
        this.people.set([]);
      }
    });
  }

  ngOnDestroy(): void {
    this.routeSub?.unsubscribe();
    this.peopleRequest?.unsubscribe();
    this.postList.reset();
  }

  onPostDeleted(id: number): void {
    this.postList.remove((post) => post.id === id);
  }

  private loadPeople(query: string): void {
    this.peopleRequest?.unsubscribe();
    this.loadingPeople.set(true);
    this.peopleFailed.set(false);
    this.peopleRequest = this.api.searchUsers(query, 20).subscribe({
      next: (users) => {
        this.people.set(users);
        this.loadingPeople.set(false);
      },
      error: () => {
        this.peopleFailed.set(true);
        this.loadingPeople.set(false);
      },
    });
  }
}
