/** A piece of a post's text: plain text, or a hashtag that is shown as a link. */
export type TextSegment =
  | { kind: 'text'; text: string }
  | { kind: 'hashtag'; text: string; tag: string };

// The same rule as the server's HashtagParser (XCloneAPI/Services/HashtagParser.cs); both are tested against
// src/testing/text-entities.json, so the app links exactly the words the server records as hashtags.
//
// A hashtag is a # followed by 1-50 letters, marks, digits or underscores, with at least one letter, not glued to a
// word or symbol in front, and ending where those characters end. Tags are compared in lower case, and only the
// first 10 different tags of a post count.
const HASHTAG = /(?<![\p{L}\p{M}\p{N}_#&])#([\p{L}\p{M}\p{N}_]{1,50})(?![\p{L}\p{M}\p{N}_])/gu;
const WHOLE_TAG = /^[\p{L}\p{M}\p{N}_]{1,50}$/u;
const HAS_LETTER = /\p{L}/u;
export const MAX_TAGS = 10;

/** The hashtags of a text, lower case, without the #, in the order they first appear (at most 10). */
export function hashtagsOf(text: string): string[] {
  const tags: string[] = [];
  for (const match of text.matchAll(HASHTAG)) {
    if (!HAS_LETTER.test(match[1])) continue;
    const tag = match[1].toLowerCase();
    if (tags.includes(tag)) continue;
    tags.push(tag);
    if (tags.length === MAX_TAGS) break;
  }
  return tags;
}

/** Whether this could be a hashtag as written in an address (without the #). */
export function isValidHashtag(tag: string): boolean {
  return WHOLE_TAG.test(tag) && HAS_LETTER.test(tag);
}

/**
 * Cuts a post's text into plain text and hashtags, without losing or adding a character: putting the segments'
 * texts back together gives the original text. Only tags the server records get a link (the first 10 different ones).
 */
export function tokenize(text: string): TextSegment[] {
  const recorded = new Set(hashtagsOf(text));
  const segments: TextSegment[] = [];
  let end = 0;

  for (const match of text.matchAll(HASHTAG)) {
    const tag = match[1].toLowerCase();
    if (!HAS_LETTER.test(match[1]) || !recorded.has(tag)) continue;

    if (match.index > end) segments.push({ kind: 'text', text: text.slice(end, match.index) });
    segments.push({ kind: 'hashtag', text: match[0], tag });
    end = match.index + match[0].length;
  }

  if (end < text.length) segments.push({ kind: 'text', text: text.slice(end) });
  return segments;
}
