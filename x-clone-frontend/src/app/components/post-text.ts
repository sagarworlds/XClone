import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { tokenize } from '../utils/text-entities';

/**
 * The text of a post, with hashtags as links. The text is only ever put on the page as text (never as HTML), and
 * every character of it is kept: line breaks and spaces are shown as typed.
 */
@Component({
  selector: 'app-post-text',
  standalone: true,
  imports: [RouterLink],
  styles: [`
    .hashtag {
      color: var(--accent-color);
    }
    .hashtag:hover {
      text-decoration: underline;
    }
  `],
  // No spaces between the pieces: they would show up in the text
  template: `@for (segment of segments(); track $index) {@if (segment.kind === 'hashtag') {<a class="hashtag" [routerLink]="['/hashtag', segment.tag]">{{ segment.text }}</a>} @else {<span>{{ segment.text }}</span>}}`,
})
export class PostTextComponent {
  readonly text = input.required<string>();
  readonly segments = computed(() => tokenize(this.text()));
}
