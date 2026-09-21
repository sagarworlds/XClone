import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { tokenize } from '../utils/text-entities';

/**
 * The text of a post, with hashtags and mentions of real accounts as links. The text is only ever put on the page as text (never as HTML), and
 * every character of it is kept: line breaks and spaces are shown as typed.
 */
@Component({
  selector: 'app-post-text',
  standalone: true,
  imports: [RouterLink],
  styles: [`
    .hashtag, .mention {
      color: var(--accent-color);
    }
    .hashtag:hover, .mention:hover {
      text-decoration: underline;
    }
  `],
  // No spaces between the pieces: they would show up in the text
  template: `@for (segment of segments(); track $index) {@if (segment.kind === 'hashtag') {<a class="hashtag" [routerLink]="['/hashtag', segment.tag]">{{ segment.text }}</a>} @else if (segment.kind === 'mention') {<a class="mention" [routerLink]="['/profile', segment.username]">{{ segment.text }}</a>} @else {<span>{{ segment.text }}</span>}}`,
})
export class PostTextComponent {
  readonly text = input.required<string>();
  /** The accounts the text names (Post.mentions): only these @names become links. */
  readonly mentions = input<readonly string[]>([]);
  // (a response from before mentions existed has no list; tokenize then links no names)
  readonly segments = computed(() => tokenize(this.text(), this.mentions()));
}
