import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideAppTesting } from '../../testing/helpers';
import { PostTextComponent } from './post-text';

describe('PostTextComponent', () => {
  let fixture: ComponentFixture<PostTextComponent>;

  const el = () => fixture.nativeElement as HTMLElement;
  const links = () => [...el().querySelectorAll<HTMLAnchorElement>('a')];

  async function show(text: string) {
    fixture = TestBed.createComponent(PostTextComponent);
    fixture.componentRef.setInput('text', text);
    fixture.detectChanges();
    await fixture.whenStable();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [PostTextComponent], providers: provideAppTesting() });
  });

  it('shows plain text as it is', async () => {
    await show('Just some words.');

    expect(el().textContent).toBe('Just some words.');
    expect(links()).toHaveLength(0);
  });

  it('makes each hashtag a link to its page, and leaves the words around it alone', async () => {
    await show('Watching the #Sunset tonight, and #coffee later');

    expect(el().textContent).toBe('Watching the #Sunset tonight, and #coffee later');
    expect(links().map((a) => [a.textContent, a.getAttribute('href')])).toEqual([
      ['#Sunset', '/hashtag/sunset'],
      ['#coffee', '/hashtag/coffee'],
    ]);
    expect(links()[0].classList).toContain('hashtag');
  });

  it('writes tags of other scripts into the address safely', async () => {
    await show('夜の #東京 と #Ελληνικά');

    expect(links().map((a) => a.getAttribute('href'))).toEqual([
      '/hashtag/%E6%9D%B1%E4%BA%AC',
      '/hashtag/%CE%B5%CE%BB%CE%BB%CE%B7%CE%BD%CE%B9%CE%BA%CE%AC',
    ]);
  });

  it('keeps every space and line break exactly as typed', async () => {
    const text = 'first line\n\n  indented  #tag  \n\tlast';
    await show(text);

    expect(el().textContent).toBe(text);
  });

  it('never turns text into markup', async () => {
    const text = '<img src=x onerror=alert(1)> <b>bold</b> #tag <script>alert(2)</script>';
    await show(text);

    expect(el().textContent).toBe(text);
    expect(el().querySelector('img, b, script')).toBeNull();
    expect(links()).toHaveLength(1);
    expect(el().querySelectorAll('span, a')).toHaveLength(3); // text, the link, text: nothing else was created
  });

  it('does not link what only looks like a hashtag', async () => {
    await show('abc#def, #2026 and ##x');

    expect(links()).toHaveLength(0);
    expect(el().textContent).toBe('abc#def, #2026 and ##x');
  });

  it('links only the tags the server records: the first ten', async () => {
    await show(Array.from({ length: 12 }, (_, i) => `#tag${i + 1}`).join(' '));

    expect(links()).toHaveLength(10);
    expect(links().at(-1)!.textContent).toBe('#tag10');
  });

  it('updates when the text changes', async () => {
    await show('before #one');

    fixture.componentRef.setInput('text', 'after #two and #three');
    await fixture.whenStable();

    expect(links().map((a) => a.textContent)).toEqual(['#two', '#three']);
    expect(el().textContent).toBe('after #two and #three');
  });

  it('shows nothing for no text', async () => {
    await show('');

    expect(el().textContent).toBe('');
  });
});
