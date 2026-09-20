import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { ApiService } from '../services/api.service';
import { Post } from '../models/types';

/**
 * Text box + counter + submit button used for new posts and for replies.
 * The parent decides what "submit" means by passing a function that returns the created post.
 */
@Component({
  selector: 'app-composer',
  standalone: true,
  imports: [FormsModule],
  styles: [`
    .composer-container {
      display: flex;
      padding: 16px;
      border-bottom: 1px solid var(--border-color);
    }
    .composer-avatar {
      margin-right: 12px;
    }
    .composer-body {
      flex-grow: 1;
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .composer-textarea {
      width: 100%;
      border: none;
      resize: none;
      background: transparent;
      color: var(--text-primary);
      font-size: 1.25rem;
      outline: none;
      padding: 4px 0;
    }
    .composer-textarea::placeholder {
      color: var(--text-secondary);
    }
    .composer-textarea:focus {
      border: none;
    }
    .composer-footer {
      display: flex;
      justify-content: flex-end;
      align-items: center;
      gap: 16px;
      border-top: 1px solid var(--border-color);
      padding-top: 12px;
    }
    .composer-error {
      margin-right: auto;
      color: var(--danger-color);
      font-size: 0.85rem;
    }
    .char-counter {
      color: var(--text-secondary);
      font-size: 0.85rem;
    }
    .char-counter.warning {
      color: var(--danger-color);
    }
    .publish-btn {
      background-color: var(--accent-color);
      color: #fff;
      font-weight: 700;
      padding: 8px 16px;
      border-radius: 9999px;
      font-size: 0.95rem;
    }
    .publish-btn:hover:not(:disabled) {
      background-color: var(--accent-hover);
    }
    .publish-btn:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }
  `],
  template: `
    <div class="composer-container">
      @if (currentUser()) {
        <img [src]="currentUser()?.avatarUrl || defaultAvatar" alt="Avatar" class="avatar composer-avatar" />
      }
      <div class="composer-body">
        <textarea
          [(ngModel)]="content"
          (ngModelChange)="length.set(content.length)"
          [placeholder]="placeholder()"
          [rows]="rows()"
          maxlength="280"
          class="composer-textarea"
        ></textarea>

        <div class="composer-footer">
          @if (error()) {
            <span class="composer-error">{{ error() }}</span>
          }
          <span class="char-counter" [class.warning]="length() > 250">{{ length() }}/280</span>
          <button [disabled]="!canSubmit()" (click)="submit()" class="publish-btn">
            @if (posting()) { Posting... } @else { {{ buttonLabel() }} }
          </button>
        </div>
      </div>
    </div>
  `
})
export class ComposerComponent {
  private readonly api = inject(ApiService);

  readonly placeholder = input("What's happening?!");
  readonly buttonLabel = input('Post');
  readonly rows = input(3);
  /** Called with the trimmed text; must return the created post. */
  readonly submitFn = input.required<(content: string) => Observable<Post>>();
  readonly posted = output<Post>();

  readonly currentUser = this.api.currentUser;
  readonly defaultAvatar = 'https://abs.twimg.com/sticky/default_profile_images/default_profile_normal.png';

  content = '';
  readonly length = signal(0);
  readonly posting = signal(false);
  readonly error = signal<string | null>(null);
  readonly canSubmit = computed(() => this.length() > 0 && !this.posting());

  submit(): void {
    const text = this.content.trim();
    if (!text || this.posting()) return;

    this.posting.set(true);
    this.error.set(null);
    this.submitFn()(text).subscribe({
      next: (post) => {
        this.content = '';
        this.length.set(0);
        this.posting.set(false);
        this.posted.emit(post);
      },
      error: (err) => {
        this.posting.set(false);
        this.error.set(err.error?.message || 'Could not post. Please try again.');
      },
    });
  }
}
