import { Component, computed, effect, ElementRef, input, linkedSignal, output, viewChild } from '@angular/core';

/** The longest a post can be (the API refuses more). */
export const MAX_POST_LENGTH = 280;

/**
 * The text box that replaces a post's text while its author edits it: the text so far, a counter, Cancel and Save.
 * It only asks (`submitted` carries the new text, trimmed); the card decides what saving means and shows a failure
 * through `error`. Escape cancels, Ctrl/Cmd+Enter saves.
 */
@Component({
  selector: 'app-post-editor',
  standalone: true,
  styles: [`
    .editor {
      display: flex;
      flex-direction: column;
      gap: 8px;
      margin-bottom: 12px;
    }
    .editor-textarea {
      width: 100%;
      resize: vertical;
      background: transparent;
      color: var(--text-primary);
      border: 1px solid var(--border-color);
      border-radius: 8px;
      padding: 8px;
      font-size: 0.95rem;
      line-height: 1.4;
      outline: none;
    }
    .editor-textarea:focus {
      border-color: var(--accent-color);
    }
    .editor-footer {
      display: flex;
      justify-content: flex-end;
      align-items: center;
      gap: 12px;
    }
    .editor-error {
      margin-right: auto;
      color: var(--danger-color);
      font-size: 0.85rem;
    }
    .editor-counter {
      color: var(--text-secondary);
      font-size: 0.85rem;
    }
    .editor-counter.warning {
      color: var(--danger-color);
    }
    .editor-cancel,
    .editor-save {
      font-weight: 700;
      padding: 6px 14px;
      border-radius: 9999px;
      font-size: 0.9rem;
    }
    .editor-cancel {
      background: transparent;
      color: var(--text-primary);
      border: 1px solid var(--border-color);
    }
    .editor-save {
      background-color: var(--accent-color);
      color: #fff;
    }
    .editor-save:hover:not(:disabled) {
      background-color: var(--accent-hover);
    }
    .editor-save:disabled,
    .editor-cancel:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }
  `],
  template: `
    <div class="editor" (click)="$event.stopPropagation()">
      <textarea
        #box
        class="editor-textarea"
        aria-label="Edit your post"
        rows="3"
        [attr.maxlength]="max"
        [value]="draft()"
        [disabled]="saving()"
        (input)="draft.set(box.value)"
        (keydown.escape)="cancel()"
        (keydown.control.enter)="save()"
        (keydown.meta.enter)="save()"
      ></textarea>
      <div class="editor-footer">
        @if (error()) {
          <span class="editor-error" role="alert">{{ error() }}</span>
        }
        <span class="editor-counter" [class.warning]="draft().length > 250">{{ draft().length }}/{{ max }}</span>
        <button type="button" class="editor-cancel" [disabled]="saving()" (click)="cancel()">Cancel</button>
        <button type="button" class="editor-save" [disabled]="!canSave()" (click)="save()">
          @if (saving()) { Saving... } @else { Save }
        </button>
      </div>
    </div>
  `,
})
export class PostEditorComponent {
  /** The text as it is now. */
  readonly content = input.required<string>();
  /** True while the card waits for the server: the box and the buttons are locked. */
  readonly saving = input(false);
  /** Why the last save failed, if it did; the box stays open with the text as it was typed. */
  readonly error = input<string | null>(null);
  /** The new text, trimmed. Only sent when there is something to save. */
  readonly submitted = output<string>();
  readonly cancelled = output<void>();

  readonly max = MAX_POST_LENGTH;
  readonly draft = linkedSignal(() => this.content());
  private readonly trimmed = computed(() => this.draft().trim());
  /** There is text, it is not the text the post has already, and nothing is being saved. */
  readonly canSave = computed(() => this.trimmed().length > 0 && this.trimmed() !== this.content() && !this.saving());

  private readonly box = viewChild.required<ElementRef<HTMLTextAreaElement>>('box');

  constructor() {
    // Ready to type, with the cursor after the text
    effect(() => {
      const box = this.box().nativeElement;
      box.focus();
      box.setSelectionRange(box.value.length, box.value.length);
    });
  }

  save(): void {
    if (this.canSave()) this.submitted.emit(this.trimmed());
  }

  cancel(): void {
    if (!this.saving()) this.cancelled.emit();
  }
}
