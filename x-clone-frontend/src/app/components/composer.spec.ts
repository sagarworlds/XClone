import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Observable, of, Subject, throwError } from 'rxjs';
import { API, http, makePost, makeUser, provideAppTesting, signInAs } from '../../testing/helpers';
import { Post } from '../models/types';
import { ComposerComponent } from './composer';

describe('ComposerComponent', () => {
  const me = makeUser({ id: 1, username: 'me', displayName: 'Me' });
  const uploaded = (letter: string, extension = 'png') => `/uploads/${letter.repeat(32)}.${extension}`;
  let fixture: ComponentFixture<ComposerComponent>;
  let submitFn: ReturnType<typeof vi.fn<(content: string, mediaUrls: string[]) => Observable<Post>>>;
  let posted: Post[];

  const el = () => fixture.nativeElement as HTMLElement;
  const settle = () => fixture.whenStable();
  const textarea = () => el().querySelector('textarea')!;
  const postButton = () => el().querySelector<HTMLButtonElement>('.publish-btn')!;
  const attachButton = () => el().querySelector<HTMLButtonElement>('.attach-btn')!;
  const fileInput = () => el().querySelector<HTMLInputElement>('input[type=file]')!;
  const previews = () => [...el().querySelectorAll<HTMLElement>('.attachment')];
  const images = () => [...el().querySelectorAll<HTMLImageElement>('.attachment img')];
  const error = () => el().querySelector('.composer-error')?.textContent?.trim();

  const file = (name = 'holiday.png', type = 'image/png', size = 10) => new File([new Uint8Array(size)], name, { type });

  async function show(submit?: (content: string, mediaUrls: string[]) => Observable<Post>) {
    submitFn = vi.fn(submit ?? (() => of(makePost(1))));
    posted = [];
    fixture = TestBed.createComponent(ComposerComponent);
    fixture.componentRef.setInput('submitFn', submitFn);
    fixture.componentInstance.posted.subscribe((p) => posted.push(p));
    fixture.detectChanges();
    await settle();
  }

  async function type(text: string) {
    textarea().value = text;
    textarea().dispatchEvent(new Event('input'));
    await settle();
  }

  /** Picks files the way the browser's file dialog does. */
  async function choose(...files: File[]) {
    Object.defineProperty(fileInput(), 'files', { value: files, configurable: true });
    fileInput().dispatchEvent(new Event('change'));
    await settle();
  }

  const uploads = () => http().match((r) => r.method === 'POST' && r.url === `${API}/media`);

  /** Answers the one pending upload with this address. */
  async function finishUpload(url: string) {
    const [request] = uploads();
    request.flush({ url });
    await settle();
  }

  beforeEach(() => {
    localStorage.clear();
    signInAs(me);
    TestBed.configureTestingModule({ imports: [ComposerComponent], providers: provideAppTesting() });
  });

  afterEach(() => {
    http().verify();
    vi.restoreAllMocks();
    localStorage.clear();
  });

  describe('posting text', () => {
    it('cannot post until something is typed', async () => {
      await show();
      expect(postButton().disabled).toBe(true);

      await type('hello');
      expect(postButton().disabled).toBe(false);

      await type('');
      expect(postButton().disabled).toBe(true);
    });

    it('posts the trimmed text with no images, empties the box and reports the post', async () => {
      const created = makePost(7, { content: 'hello' });
      await show(() => of(created));
      await type('  hello  ');

      postButton().click();
      await settle();

      expect(submitFn).toHaveBeenCalledExactlyOnceWith('hello', []);
      expect(textarea().value).toBe('');
      expect(el().querySelector('.char-counter')?.textContent?.trim()).toBe('0/280');
      expect(posted).toEqual([created]);
    });

    it('shows the answer of the server when posting fails, and keeps the text', async () => {
      await show(() => throwError(() => ({ error: { message: 'Post content cannot be empty' } })));
      await type('hello');

      postButton().click();
      await settle();

      expect(error()).toBe('Post content cannot be empty');
      expect(textarea().value).toBe('hello');
      expect(posted).toEqual([]);
    });

    it('says something general when the server gave no reason', async () => {
      await show(() => throwError(() => ({ status: 0 })));
      await type('hello');

      postButton().click();
      await settle();

      expect(error()).toBe('Could not post. Please try again.');
    });

    it('cannot be clicked twice while posting', async () => {
      const answer = new Subject<Post>();
      await show(() => answer);
      await type('hello');

      postButton().click();
      await settle();
      expect(postButton().disabled).toBe(true);
      expect(postButton().textContent).toContain('Posting...');
      postButton().click();

      expect(submitFn).toHaveBeenCalledOnce();
    });
  });

  describe('adding images', () => {
    it('offers the four image types, several at once', async () => {
      await show();

      expect(fileInput().accept).toBe('image/png,image/jpeg,image/gif,image/webp');
      expect(fileInput().multiple).toBe(true);
    });

    it('uploads a chosen image at once, as a form with the file, and shows it when it has arrived', async () => {
      await show();

      await choose(file('holiday.png'));
      const [request] = uploads();
      expect(((request.request.body as FormData).get('file') as File).name).toBe('holiday.png');
      expect(previews()).toHaveLength(1);
      expect(previews()[0].textContent).toContain('Uploading...');
      expect(images()).toHaveLength(0);

      request.flush({ url: uploaded('a') });
      await settle();

      expect(previews()[0].textContent).not.toContain('Uploading...');
      expect(images().map((i) => i.getAttribute('src'))).toEqual([`http://localhost:5168${uploaded('a')}`]);
      expect(images()[0].alt).toBe('Attached image');
    });

    it('cannot post while an image is still uploading, even with text', async () => {
      await show();
      await type('look at this');

      await choose(file());
      expect(postButton().disabled).toBe(true);
      postButton().click();
      expect(submitFn).not.toHaveBeenCalled();

      await finishUpload(uploaded('a'));
      expect(postButton().disabled).toBe(false);
    });

    it('refuses to submit while an image is uploading, however it is asked to', async () => {
      await show();
      await type('early');
      await choose(file());

      fixture.componentInstance.submit(); // not through the (disabled) button

      expect(submitFn).not.toHaveBeenCalled();
      uploads()[0].flush({ url: uploaded('a') });
    });

    it('needs text as well: an image alone cannot be posted', async () => {
      await show();

      await choose(file());
      await finishUpload(uploaded('a'));

      expect(postButton().disabled).toBe(true);
    });

    it('sends the addresses of the images with the post, in the order they were chosen, and then starts afresh', async () => {
      await show();
      await type('two pictures');
      await choose(file('a.png'));
      await finishUpload(uploaded('a'));
      await choose(file('b.jpg', 'image/jpeg'));
      await finishUpload(uploaded('b', 'jpg'));

      postButton().click();
      await settle();

      expect(submitFn).toHaveBeenCalledExactlyOnceWith('two pictures', [uploaded('a'), uploaded('b', 'jpg')]);
      expect(previews()).toHaveLength(0);

      await type('and now none');
      postButton().click();
      await settle();
      expect(submitFn).toHaveBeenLastCalledWith('and now none', []);
    });

    it('keeps the images and the text when posting fails, so the same post can be sent again', async () => {
      let attempts = 0;
      await show(() => (++attempts === 1 ? throwError(() => ({ error: { message: 'boom' } })) : of(makePost(1))));
      await type('try twice');
      await choose(file());
      await finishUpload(uploaded('a'));

      postButton().click();
      await settle();
      expect(error()).toBe('boom');
      expect(previews()).toHaveLength(1);

      postButton().click();
      await settle();
      expect(submitFn).toHaveBeenNthCalledWith(2, 'try twice', [uploaded('a')]);
      expect(previews()).toHaveLength(0);
    });

    it('takes several files at once, one upload each', async () => {
      await show();

      await choose(file('a.png'), file('b.gif', 'image/gif'), file('c.webp', 'image/webp'));

      const requests = uploads();
      expect(requests.map((r) => ((r.request.body as FormData).get('file') as File).name)).toEqual(['a.png', 'b.gif', 'c.webp']);
      expect(previews()).toHaveLength(3);
      requests.forEach((r, i) => r.flush({ url: uploaded('abc'[i]) }));
      await settle();
      expect(images()).toHaveLength(3);
    });
  });

  describe('taking an image off again', () => {
    it('removes a finished image, and it is not posted', async () => {
      await show();
      await type('one picture left');
      await choose(file('a.png'), file('b.png'));
      const [first, second] = uploads();
      first.flush({ url: uploaded('a') });
      second.flush({ url: uploaded('b') });
      await settle();

      el().querySelectorAll<HTMLButtonElement>('.remove-attachment')[0].click();
      await settle();
      postButton().click();
      await settle();

      expect(submitFn).toHaveBeenCalledExactlyOnceWith('one picture left', [uploaded('b')]);
    });

    it('cancels an upload that is still going, and lets the post go ahead without it', async () => {
      await show();
      await type('never mind the picture');
      await choose(file());
      const [request] = uploads();
      expect(postButton().disabled).toBe(true);

      el().querySelector<HTMLButtonElement>('.remove-attachment')!.click();
      await settle();

      expect(request.cancelled).toBe(true);
      expect(previews()).toHaveLength(0);
      expect(postButton().disabled).toBe(false);
    });

    it('cancels uploads still going when the box goes away', async () => {
      await show();
      await choose(file());
      const [request] = uploads();

      fixture.destroy();

      expect(request.cancelled).toBe(true);
    });
  });

  describe('limits', () => {
    it('takes four images and no more, and says so', async () => {
      await show();

      await choose(...Array.from({ length: 5 }, (_, i) => file(`p${i}.png`)));

      expect(uploads()).toHaveLength(4); // the fifth was never sent
      expect(previews()).toHaveLength(4);
      expect(error()).toBe('You can attach up to 4 images.');
      expect(attachButton().disabled).toBe(true);
    });

    it('takes another once one has been removed', async () => {
      await show();
      await choose(...Array.from({ length: 4 }, (_, i) => file(`p${i}.png`)));
      uploads().forEach((r, i) => r.flush({ url: uploaded('abcd'[i]) }));
      await settle();
      expect(attachButton().disabled).toBe(true);

      el().querySelector<HTMLButtonElement>('.remove-attachment')!.click();
      await settle();
      expect(attachButton().disabled).toBe(false);

      await choose(file('again.png'));
      expect(uploads()).toHaveLength(1);
    });

    it.each([
      ['an SVG', 'logo.svg', 'image/svg+xml'],
      ['a text file', 'notes.txt', 'text/plain'],
      ['a PDF', 'doc.pdf', 'application/pdf'],
      ['a file that names no type', 'mystery', ''],
    ])('refuses %s without asking the server', async (_label, name, type) => {
      await show();

      await choose(file(name, type));

      expect(uploads()).toHaveLength(0);
      expect(previews()).toHaveLength(0);
      expect(error()).toBe('Only PNG, JPEG, GIF and WebP images can be attached.');
    });

    it('takes an image of exactly 5 MB, and refuses one byte more', async () => {
      await show();

      await choose(file('big.png', 'image/png', 5 * 1024 * 1024 + 1));
      expect(uploads()).toHaveLength(0);
      expect(error()).toBe('Images can be at most 5 MB.');

      await choose(file('just-right.png', 'image/png', 5 * 1024 * 1024));
      expect(uploads()).toHaveLength(1);
      expect(error()).toBeUndefined();
    });

    it('uploads the good ones of a mixed batch and still mentions the bad one', async () => {
      await show();

      await choose(file('a.png'), file('notes.txt', 'text/plain'), file('b.png'));

      expect(uploads()).toHaveLength(2);
      expect(error()).toBe('Only PNG, JPEG, GIF and WebP images can be attached.');
    });

    it('forgets an old complaint when the next files are chosen', async () => {
      await show();
      await choose(file('notes.txt', 'text/plain'));
      expect(error()).toBeDefined();

      await choose(file('a.png'));

      expect(error()).toBeUndefined();
      expect(uploads()).toHaveLength(1);
    });
  });

  describe('when an upload fails', () => {
    it('drops that image and says what the server said', async () => {
      await show();
      await choose(file());

      uploads()[0].flush({ message: 'Only PNG, JPEG, GIF and WebP images can be uploaded.' }, { status: 400, statusText: 'Bad Request' });
      await settle();

      expect(previews()).toHaveLength(0);
      expect(error()).toBe('Only PNG, JPEG, GIF and WebP images can be uploaded.');
    });

    it('passes on the wait when too many uploads were made', async () => {
      await show();
      await choose(file());

      uploads()[0].flush({ message: 'Too many attempts. Please try again in a minute.' }, { status: 429, statusText: 'Too Many Requests' });
      await settle();

      expect(error()).toBe('Too many attempts. Please try again in a minute.');
    });

    it('says something general when the server gave no reason', async () => {
      await show();
      await choose(file());

      uploads()[0].error(new ProgressEvent('error'));
      await settle();

      expect(error()).toBe('Could not upload the image. Please try again.');
      expect(previews()).toHaveLength(0);
    });

    it('does not disturb the other images, and the post can go ahead without the failed one', async () => {
      await show();
      await type('one of two');
      await choose(file('a.png'), file('b.png'));
      const [first, second] = uploads();
      first.flush({ message: 'nope' }, { status: 400, statusText: 'Bad Request' });
      second.flush({ url: uploaded('b') });
      await settle();

      expect(previews()).toHaveLength(1);
      postButton().click();
      await settle();

      expect(submitFn).toHaveBeenCalledExactlyOnceWith('one of two', [uploaded('b')]);
    });
  });

  describe('while posting', () => {
    it('cannot add images', async () => {
      const answer = new Subject<Post>();
      await show(() => answer);
      await type('hello');

      postButton().click();
      await settle();

      expect(attachButton().disabled).toBe(true);
    });
  });
});
