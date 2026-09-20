import { Component, input } from '@angular/core';
import { LoadMoreSource } from '../services/paged-list';

/** The button under a paged list. It is only there while the server has more to give. */
@Component({
  selector: 'app-load-more',
  standalone: true,
  template: `
    @let source = list();
    @if (source.hasMore()) {
      <div class="load-more" [attr.aria-busy]="source.loadingMore()">
        @if (source.failed()) {
          <p class="load-more-error" role="alert">Couldn't load more. Check your connection and try again.</p>
        }
        <button type="button" class="load-more-btn" [disabled]="source.loadingMore()" (click)="source.loadMore()">
          {{ source.loadingMore() ? 'Loading...' : source.failed() ? 'Try again' : 'Load more' }}
        </button>
      </div>
    }
  `,
  styles: [`
    .load-more {
      border-bottom: 1px solid var(--border-color);
    }
    .load-more-error {
      padding: 12px 16px 0;
      color: var(--danger-color);
      font-size: 0.9rem;
      text-align: center;
    }
    .load-more-btn {
      display: block;
      width: 100%;
      padding: 16px;
      background: transparent;
      color: var(--accent-color);
      font-size: 0.95rem;
    }
    .load-more-btn:hover:not(:disabled) {
      background-color: rgba(29, 155, 240, 0.1);
    }
    .load-more-btn:disabled {
      color: var(--text-secondary);
      cursor: default;
    }
  `]
})
export class LoadMoreComponent {
  readonly list = input.required<LoadMoreSource>();
}
