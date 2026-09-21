/** A piece of a post's text: plain text, a hashtag, or a mention, the last two shown as links. */
export type TextSegment =
  | { kind: 'text'; text: string }
  | { kind: 'hashtag'; text: string; tag: string }
  | { kind: 'mention'; text: string; username: string };

// The same rules as the server's HashtagParser and MentionParser (XCloneAPI/Services); all are tested against
// src/testing/text-entities.json, so the app links exactly the words the server records.
//
// A hashtag is a # followed by 1-50 letters, marks, digits or underscores, with at least one letter, not glued to a
// word or symbol in front, and ending where those characters end. Tags are compared in lower case, and only the
// first 10 different tags of a post count.
//
// A mention is an @ followed by 3-50 letters (a-z), digits or underscores (what a username is made of), not glued to
// a word in front (so an email address is not one), and ending where the word ends. Names are compared without
// regard to case, and only the first 10 different names of a post count. The server also says which of them are
// real accounts (Post.mentions); only those become links.
const HASHTAG = /(?<![\p{L}\p{M}\p{N}_#&])#([\p{L}\p{M}\p{N}_]{1,50})(?![\p{L}\p{M}\p{N}_])/gu;
const MENTION = /(?<![\p{L}\p{M}\p{N}_@])@([A-Za-z0-9_]{3,50})(?![\p{L}\p{M}\p{N}_])/gu;
const WHOLE_TAG = /^[\p{L}\p{M}\p{N}_]{1,50}$/u;
const HAS_LETTER = /\p{L}/u;
export const MAX_TAGS = 10;
export const MAX_MENTIONS = 10;

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

/** The names a text mentions, without the @, as first written, each once whatever its case (at most 10). */
export function mentionsOf(text: string): string[] {
  const names: string[] = [];
  for (const match of text.matchAll(MENTION)) {
    const name = match[1];
    if (names.some((n) => n.toLowerCase() === name.toLowerCase())) continue;
    names.push(name);
    if (names.length === MAX_MENTIONS) break;
  }
  return names;
}

/** Whether this could be a hashtag as written in an address (without the #). */
export function isValidHashtag(tag: string): boolean {
  return WHOLE_TAG.test(tag) && HAS_LETTER.test(tag);
}

/**
 * Cuts a post's text into plain text, hashtags and mentions, without losing or adding a character: putting the
 * segments' texts back together gives the original text. Only tags the server records get a link (the first 10
 * different ones); a mention gets one when it names one of `accounts` (the usernames the server found, which say how
 * each is spelled on its profile).
 */
export function tokenize(text: string, accounts: readonly string[] = []): TextSegment[] {
  const recordedTags = new Set(hashtagsOf(text));
  const recordedNames = new Set(mentionsOf(text).map((n) => n.toLowerCase()));
  const spelled = new Map(accounts.map((username) => [username.toLowerCase(), username]));

  const links: { index: number; segment: TextSegment }[] = [];

  for (const match of text.matchAll(HASHTAG)) {
    const tag = match[1].toLowerCase();
    if (!recordedTags.has(tag)) continue;
    links.push({ index: match.index, segment: { kind: 'hashtag', text: match[0], tag } });
  }

  for (const match of text.matchAll(MENTION)) {
    const lowered = match[1].toLowerCase();
    const username = spelled.get(lowered);
    if (!recordedNames.has(lowered) || username === undefined) continue;
    links.push({ index: match.index, segment: { kind: 'mention', text: match[0], username } });
  }

  links.sort((a, b) => a.index - b.index);

  const segments: TextSegment[] = [];
  let end = 0;
  for (const { index, segment } of links) {
    if (index > end) segments.push({ kind: 'text', text: text.slice(end, index) });
    segments.push(segment);
    end = index + segment.text.length;
  }

  if (end < text.length) segments.push({ kind: 'text', text: text.slice(end) });
  return segments;
}
