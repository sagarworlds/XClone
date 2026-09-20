import { Component, computed, inject, input, OnDestroy, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, Subscription } from 'rxjs';
import { ApiService } from '../services/api.service';
import { Post } from '../models/types';
import { IMAGE_TYPES, MAX_IMAGE_BYTES, MAX_IMAGES, mediaSrc } from '../utils/media-url';

/** An image chosen for the post: uploading until the server has answered with its address. */
interface Attachment {
  id: number;
  url: string | null;
}

/**
 * Text box + images + counter + submit button used for new posts and for replies.
 * The parent decides what "submit" means by passing a function that returns the created post.
 * Images are uploaded as soon as they are chosen; the post can be sent once they are all there.
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
      min-width: 0;
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
    .attachments {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 8px;
    }
    .attachment {
      position: relative;
      aspect-ratio: 16 / 9;
      border-radius: 12px;
      overflow: hidden;
      background-color: var(--bg-secondary);
      border: 1px solid var(--border-color);
      display: flex;
      align-items: center;
      justify-content: center;
      color: var(--text-secondary);
      font-size: 0.85rem;
    }
    .attachment img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }
    .remove-attachment {
      position: absolute;
      top: 6px;
      right: 6px;
      width: 28px;
      height: 28px;
      border-radius: 50%;
      background-color: rgba(0, 0, 0, 0.7);
      color: #fff;
      font-size: 1.1rem;
      line-height: 1;
    }
    .remove-attachment:hover {
      background-color: rgba(0, 0, 0, 0.9);
    }
    .composer-footer {
      display: flex;
      justify-content: flex-end;
      align-items: center;
      gap: 16px;
      border-top: 1px solid var(--border-color);
      padding-top: 12px;
    }
    .attach-btn {
      margin-right: auto;
      display: flex;
      align-items: center;
      padding: 6px;
      border-radius: 50%;
      background: transparent;
      color: var(--accent-color);
    }
    .attach-btn:hover:not(:disabled) {
      background-color: rgba(29, 155, 240, 0.1);
    }
    .attach-btn:disabled {
      opacity: 0.4;
      cursor: not-allowed;
    }
    .composer-error {
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

        @if (attachments().length > 0) {
          <div class="attachments">
            @for (attachment of attachments(); track attachment.id) {
              <div class="attachment">
                @if (attachment.url) {
                  <img [src]="src(attachment.url)" alt="Attached image" />
                } @else {
                  <span class="attachment-status">Uploading...</span>
                }
                <button type="button" class="remove-attachment" aria-label="Remove image" (click)="remove(attachment.id)">×</button>
              </div>
            }
          </div>
        }

        <div class="composer-footer">
          <input
            #picker
            type="file"
            hidden
            multiple
            [accept]="acceptedTypes"
            (change)="onFilesChosen($event)"
          />
          <button
            type="button"
            class="attach-btn"
            title="Add images"
            aria-label="Add images"
            [disabled]="attachments().length >= maxImages || posting()"
            (click)="picker.click()"
          >
            <span class="material-symbols-outlined">image</span>
          </button>

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
export class ComposerComponent implements OnDestroy {
  private readonly api = inject(ApiService);

  readonly placeholder = input("What's happening?!");
  readonly buttonLabel = input('Post');
  readonly rows = input(3);
  /** Called with the trimmed text and the addresses of the uploaded images; must return the created post. */
  readonly submitFn = input.required<(content: string, mediaUrls: string[]) => Observable<Post>>();
  readonly posted = output<Post>();

  readonly currentUser = this.api.currentUser;
  readonly defaultAvatar = 'https://abs.twimg.com/sticky/default_profile_images/default_profile_normal.png';
  readonly acceptedTypes = IMAGE_TYPES.join(',');
  readonly maxImages = MAX_IMAGES;
  readonly src = (url: string) => mediaSrc(url) ?? '';

  content = '';
  readonly length = signal(0);
  readonly posting = signal(false);
  readonly error = signal<string | null>(null);
  readonly attachments = signal<Attachment[]>([]);
  readonly uploading = computed(() => this.attachments().some((a) => a.url === null));
  readonly canSubmit = computed(() => this.length() > 0 && !this.posting() && !this.uploading());

  private nextId = 0;
  private readonly uploads = new Map<number, Subscription>();

  ngOnDestroy(): void {
    this.uploads.forEach((upload) => upload.unsubscribe());
  }

  onFilesChosen(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    input.value = ''; // so that choosing the same file again is noticed
    this.error.set(null);

    for (const file of files) {
      if (this.attachments().length >= MAX_IMAGES) {
        this.error.set(`You can attach up to ${MAX_IMAGES} images.`);
        return;
      }
      if (!IMAGE_TYPES.includes(file.type)) {
        this.error.set('Only PNG, JPEG, GIF and WebP images can be attached.');
      } else if (file.size > MAX_IMAGE_BYTES) {
        this.error.set('Images can be at most 5 MB.');
      } else {
        this.upload(file);
      }
    }
  }

  remove(id: number): void {
    this.uploads.get(id)?.unsubscribe();
    this.uploads.delete(id);
    this.attachments.update((list) => list.filter((a) => a.id !== id));
  }

  submit(): void {
    const text = this.content.trim();
    if (!text || this.posting() || this.uploading()) return;

    this.posting.set(true);
    this.error.set(null);
    const mediaUrls = this.attachments().map((a) => a.url!);
    this.submitFn()(text, mediaUrls).subscribe({
      next: (post) => {
        this.content = '';
        this.length.set(0);
        this.attachments.set([]);
        this.posting.set(false);
        this.posted.emit(post);
      },
      error: (err) => {
        this.posting.set(false);
        this.error.set(err.error?.message || 'Could not post. Please try again.');
      },
    });
  }

  private upload(file: File): void {
    const id = ++this.nextId;
    this.attachments.update((list) => [...list, { id, url: null }]);

    this.uploads.set(
      id,
      this.api.uploadMedia(file).subscribe({
        next: ({ url }) => {
          this.uploads.delete(id);
          this.attachments.update((list) => list.map((a) => (a.id === id ? { ...a, url } : a)));
        },
        error: (err) => {
          this.uploads.delete(id);
          this.attachments.update((list) => list.filter((a) => a.id !== id));
          this.error.set(err.error?.message || 'Could not upload the image. Please try again.');
        },
      }),
    );
  }
}
