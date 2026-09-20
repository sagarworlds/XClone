import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { ApiService } from '../services/api.service';
import { PagedList } from '../services/paged-list';
import { Post } from '../models/types';
import { isValidHashtag } from '../utils/text-entities';
import { LoadMoreComponent } from './load-more';
import { PostCardComponent } from './post-card';
import { SidebarComponent } from './sidebar';
import { WidgetsComponent } from './widgets';

/** Every post and reply that uses a hashtag (`/hashtag/:tag`), newest first. */
@Component({
  selector: 'app-hashtag',
  standalone: true,
  imports: [RouterLink, SidebarComponent, WidgetsComponent, PostCardComponent, LoadMoreComponent],
  template: `
    <div class="app-container">
      <app-sidebar [active]="null" />

      <main class="main-content">
        <header class="header page-header">
          <a class="back-btn" routerLink="/home" aria-label="Back to home">
            <span class="material-symbols-outlined">arrow_back</span>
          </a>
          <div class="header-titles">
            <h2>#{{ tag() }}</h2>
          </div>
        </header>

        @if (!valid()) {
          <div class="state-message">
            <h3>That is not a hashtag</h3>
            <p>Hashtags are made of letters, digits and underscores, with at least one letter.</p>
          </div>
        } @else if (list.loading()) {
          <div class="state-message">Loading posts...</div>
        } @else if (list.items().length === 0) {
          <div class="state-message">
            @if (list.failed()) {
              <h3>Couldn't load the posts</h3>
              <p>Check your connection and reload the page.</p>
            } @else {
              <h3>No posts with #{{ tag() }} yet</h3>
              <p>Be the first: write a post that includes it.</p>
            }
          </div>
        } @else {
          @for (post of list.items(); track post.id) {
            <app-post-card [post]="post" (deleted)="onPostDeleted($event)" />
          }
          <app-load-more [list]="list" />
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
      overflow-wrap: anywhere;
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
  `]
})
export class HashtagComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(ApiService);

  readonly tag = signal('');
  readonly valid = signal(true);
  readonly list = new PagedList<Post>((cursor, take) => this.api.getHashtagPosts(this.tag(), cursor, take), (post) => String(post.id));

  private routeSub?: Subscription;

  ngOnInit(): void {
    // The same page is kept when going from one hashtag to another
    this.routeSub = this.route.paramMap.subscribe((params) => {
      const tag = (params.get('tag') ?? '').replace(/^#/, '');
      this.tag.set(tag);
      this.valid.set(isValidHashtag(tag));

      if (this.valid()) {
        this.list.loadFirst(); // also forgets the other hashtag's posts and cancels its request
      } else {
        this.list.reset();
      }
    });
  }

  ngOnDestroy(): void {
    this.routeSub?.unsubscribe();
    this.list.reset();
  }

  onPostDeleted(id: number): void {
    this.list.remove((post) => post.id === id);
  }
}
