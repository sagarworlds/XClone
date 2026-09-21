import examples from '../../testing/text-entities.json';
import { hashtagsOf, isValidHashtag, MAX_MENTIONS, MAX_TAGS, mentionsOf, tokenize } from './text-entities';

interface Example {
  text: string;
  tags: string[];
}

interface MentionExample {
  text: string;
  names: string[];
}

const hashtagExamples = examples.hashtags as Example[];
const mentionExamples = examples.mentions as MentionExample[];

describe('hashtagsOf', () => {
  it('has the shared examples to check against', () => {
    expect(hashtagExamples.length).toBeGreaterThanOrEqual(40);
  });

  // The server's HashtagParser is tested against the very same file, so the two always agree
  it.each(hashtagExamples.map((e) => [JSON.stringify(e.text), e] as const))('finds the tags of %s', (_label, example) => {
    expect(hashtagsOf(example.text)).toEqual(example.tags);
  });

  it('takes a tag of fifty letters and no tag at all of fifty-one', () => {
    const fifty = 'a'.repeat(50);

    expect(hashtagsOf(`#${fifty}`)).toEqual([fifty]);
    expect(hashtagsOf(`#${fifty}a`)).toEqual([]);
    expect(hashtagsOf(`#${fifty}a #ok`)).toEqual(['ok']);
  });

  it('counts only the first ten different tags', () => {
    const text = Array.from({ length: 12 }, (_, i) => `#tag${i + 1}`).join(' ');

    expect(hashtagsOf(text)).toEqual(Array.from({ length: 10 }, (_, i) => `tag${i + 1}`));
    expect(hashtagsOf(text)).toHaveLength(MAX_TAGS);
  });

  it('does not let repeats use up the ten', () => {
    const text = Array.from({ length: 30 }, (_, i) => `#same #other${(i % 9) + 1}`).join(' ');

    const tags = hashtagsOf(text);

    expect(tags).toHaveLength(10);
    expect(new Set(tags).size).toBe(10);
    expect(tags[0]).toBe('same');
  });
});

describe('isValidHashtag', () => {
  it.each(['sunset', 'Sunset', 'SUNSET_2026', '2026a', '__a', 'école', '日本語', 'a'.repeat(50)])('accepts %s', (tag) => {
    expect(isValidHashtag(tag)).toBe(true);
  });

  it.each(['', '#', '#sunset', 'a b', 'a-b', 'a#b', '2026', '_', '___', '😀', 'tag\n', 'tag,', '../x', 'a'.repeat(51)])(
    'refuses %j',
    (tag) => {
      expect(isValidHashtag(tag)).toBe(false);
    },
  );
});

describe('tokenize', () => {
  const tagsLinked = (text: string) => tokenize(text).flatMap((s) => (s.kind === 'hashtag' ? [s.tag] : []));

  // Nothing may be lost or added: the pieces put back together are the text
  it.each(hashtagExamples.map((e) => [JSON.stringify(e.text), e.text] as const))('keeps every character of %s', (_label, text) => {
    expect(tokenize(text).map((s) => s.text).join('')).toBe(text);
  });

  it.each(hashtagExamples.map((e) => [JSON.stringify(e.text), e] as const))('links exactly the tags of %s', (_label, example) => {
    expect(new Set(tagsLinked(example.text))).toEqual(new Set(example.tags));
  });

  it('gives no pieces for no text', () => {
    expect(tokenize('')).toEqual([]);
  });

  it('gives one plain piece for text without tags', () => {
    expect(tokenize('just words')).toEqual([{ kind: 'text', text: 'just words' }]);
  });

  it('cuts around each tag, keeping the # and the letters as typed, and the tag in lower case', () => {
    expect(tokenize('Hello #World, bye')).toEqual([
      { kind: 'text', text: 'Hello ' },
      { kind: 'hashtag', text: '#World', tag: 'world' },
      { kind: 'text', text: ', bye' },
    ]);
  });

  it('starts and ends with a tag without empty pieces around it', () => {
    expect(tokenize('#a b #c')).toEqual([
      { kind: 'hashtag', text: '#a', tag: 'a' },
      { kind: 'text', text: ' b ' },
      { kind: 'hashtag', text: '#c', tag: 'c' },
    ]);
  });

  it('links every appearance of a tag, not only the first', () => {
    expect(tagsLinked('#Sunset then #sunset again')).toEqual(['sunset', 'sunset']);
  });

  it('links only the tags the server records: the first ten different ones', () => {
    const text = Array.from({ length: 12 }, (_, i) => `#tag${i + 1}`).join(' ');

    expect(tagsLinked(text)).toEqual(Array.from({ length: 10 }, (_, i) => `tag${i + 1}`));
  });

  it('still links a repeat of one of the first ten after the eleventh has been skipped', () => {
    const text = Array.from({ length: 11 }, (_, i) => `#tag${i + 1}`).join(' ') + ' #tag1';

    expect(tagsLinked(text)).toContain('tag1');
    expect(tagsLinked(text)).not.toContain('tag11');
  });

  it('leaves what only looks like a tag as plain text', () => {
    expect(tokenize('abc#def #2026 ##x')).toEqual([{ kind: 'text', text: 'abc#def #2026 ##x' }]);
  });

  it('does not treat markup as anything but text', () => {
    const pieces = tokenize('<script>alert(1)</script> #tag <b>bold</b>');

    expect(pieces.map((p) => p.text).join('')).toBe('<script>alert(1)</script> #tag <b>bold</b>');
    expect(pieces.filter((p) => p.kind === 'hashtag')).toEqual([{ kind: 'hashtag', text: '#tag', tag: 'tag' }]);
  });
});

describe('mentionsOf', () => {
  it('has the shared examples to check against', () => {
    expect(mentionExamples.length).toBeGreaterThanOrEqual(40);
  });

  // The server's MentionParser is tested against the very same file, so the two always agree
  it.each(mentionExamples.map((e) => [JSON.stringify(e.text), e] as const))('finds the names in %s', (_label, example) => {
    expect(mentionsOf(example.text)).toEqual(example.names);
  });

  it('takes a name of fifty characters and no mention at all of fifty-one', () => {
    const fifty = 'a'.repeat(50);

    expect(mentionsOf(`@${fifty}`)).toEqual([fifty]);
    expect(mentionsOf(`@${fifty}a`)).toEqual([]);
  });

  it('counts only the first ten different names', () => {
    const text = Array.from({ length: 12 }, (_, i) => `@user${String(i + 1).padStart(2, '0')}`).join(' ');

    expect(mentionsOf(text)).toEqual(Array.from({ length: 10 }, (_, i) => `user${String(i + 1).padStart(2, '0')}`));
    expect(mentionsOf(text)).toHaveLength(MAX_MENTIONS);
  });

  it('does not let repeats in any case use up the ten', () => {
    const text = Array.from({ length: 30 }, (_, i) => `@Same @SAME @other${(i % 9) + 1}`).join(' ');

    const names = mentionsOf(text);

    expect(names).toHaveLength(10);
    expect(names[0]).toBe('Same'); // as first written
    expect(new Set(names.map((n) => n.toLowerCase())).size).toBe(10);
  });

  it('keeps a name as first written, not lower-cased', () => {
    expect(mentionsOf('@BoB_Smith @bob_smith')).toEqual(['BoB_Smith']);
  });
});

describe('tokenize with mentions', () => {
  const linked = (text: string, accounts: string[]) =>
    tokenize(text, accounts).flatMap((s) => (s.kind === 'mention' ? [s.username] : []));

  // Nothing may be lost or added, whatever accounts exist
  it.each(mentionExamples.map((e) => [JSON.stringify(e.text), e] as const))('keeps every character of %s', (_label, example) => {
    expect(tokenize(example.text, example.names).map((s) => s.text).join('')).toBe(example.text);
    expect(tokenize(example.text).map((s) => s.text).join('')).toBe(example.text);
  });

  it.each(mentionExamples.map((e) => [JSON.stringify(e.text), e] as const))('links exactly the names of %s when they are all accounts', (_label, example) => {
    expect(new Set(linked(example.text, example.names).map((n) => n.toLowerCase()))).toEqual(new Set(example.names.map((n) => n.toLowerCase())));
  });

  it.each(mentionExamples.map((e) => [JSON.stringify(e.text), e] as const))('links nothing in %s when none of them is an account', (_label, example) => {
    expect(linked(example.text, [])).toEqual([]);
  });

  it('links a name that is an account, as the account spells it, whatever the case in the text', () => {
    expect(tokenize('Hi @BOB, and @bob!', ['Bob'])).toEqual([
      { kind: 'text', text: 'Hi ' },
      { kind: 'mention', text: '@BOB', username: 'Bob' },
      { kind: 'text', text: ', and ' },
      { kind: 'mention', text: '@bob', username: 'Bob' },
      { kind: 'text', text: '!' },
    ]);
  });

  it('leaves a name that is not an account as plain text', () => {
    expect(tokenize('@bob and @ghost', ['bob'])).toEqual([
      { kind: 'mention', text: '@bob', username: 'bob' },
      { kind: 'text', text: ' and @ghost' },
    ]);
  });

  it('never links accounts the text does not name', () => {
    expect(tokenize('no names here', ['bob', 'alice'])).toEqual([{ kind: 'text', text: 'no names here' }]);
  });

  it('links only the first ten names, whoever the accounts are', () => {
    const names = Array.from({ length: 12 }, (_, i) => `user${String(i + 1).padStart(2, '0')}`);

    expect(linked(names.map((n) => '@' + n).join(' '), names)).toEqual(names.slice(0, 10));
  });

  it('puts hashtags and mentions in the order they are written', () => {
    expect(tokenize('#one @bob #two @alice', ['bob', 'alice'])).toEqual([
      { kind: 'hashtag', text: '#one', tag: 'one' },
      { kind: 'text', text: ' ' },
      { kind: 'mention', text: '@bob', username: 'bob' },
      { kind: 'text', text: ' ' },
      { kind: 'hashtag', text: '#two', tag: 'two' },
      { kind: 'text', text: ' ' },
      { kind: 'mention', text: '@alice', username: 'alice' },
    ]);
  });

  it('does not mistake an email address for a mention', () => {
    expect(linked('write to bob@example.com', ['bob', 'example'])).toEqual([]);
  });

  it('does not treat markup as anything but text', () => {
    const pieces = tokenize('<b>@bob</b> <script>@bob</script>', ['bob']);

    expect(pieces.map((p) => p.text).join('')).toBe('<b>@bob</b> <script>@bob</script>');
    expect(pieces.filter((p) => p.kind === 'mention')).toHaveLength(2);
  });
});
