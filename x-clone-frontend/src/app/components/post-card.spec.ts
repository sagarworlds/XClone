import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { API, http, makePost, makeUser, provideAppTesting, signInAs } from '../../testing/helpers';
import { Post } from '../models/types';
import { PostCardComponent, postEntryKey } from './post-card';

describe('PostCardComponent', () => {
  const me = makeUser({ id: 1, username: 'me', displayName: 'Me' });
  const bob = makeUser({ id: 3, username: 'bob', displayName: 'Bob', email: '' });
  let fixture: ComponentFixture<PostCardComponent>;
  let navigate: ReturnType<typeof vi.spyOn>;
  let changes: Post[];
  let deleted: number[];

  const el = () => fixture.nativeElement as HTMLElement;
  const settle = () => fixture.whenStable();
  const button = (name: string) => el().querySelector<HTMLButtonElement>(`.${name}`)!;
  /** The number shown next to an action button (its icon is the other span). */
  const count = (name: string) => button(name).querySelector('span:not(.material-symbols-outlined)')!.textContent!.trim();

  /** Shows a post the way a page does: through the `post` input. */
  async function show(post: Post, focus = false) {
    fixture = TestBed.createComponent(PostCardComponent);
    fixture.componentRef.setInput('post', post);
    fixture.componentRef.setInput('focus', focus);
    changes = [];
    deleted = [];
    fixture.componentInstance.changed.subscribe((p) => changes.push(p));
    fixture.componentInstance.deleted.subscribe((id) => deleted.push(id));
    fixture.detectChanges();
    await settle();
  }

  beforeEach(() => {
    localStorage.clear();
    signInAs(me);
    TestBed.configureTestingModule({ imports: [PostCardComponent], providers: provideAppTesting() });
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  });

  afterEach(() => {
    http().verify();
    vi.restoreAllMocks();
    localStorage.clear();
  });

  describe('what it shows', () => {
    it('shows the author, the text and the three counters', async () => {
      await show(makePost(7, { content: 'Hello world', repliesCount: 2, retweetsCount: 3, likesCount: 4 }));

      expect(el().querySelector('.post-display-name')?.textContent).toBe('Other');
      expect(el().querySelector('.post-username')?.textContent).toBe('@other');
      expect(el().querySelector('.post-text-content')?.textContent?.trim()).toBe('Hello world');
      expect(el().querySelector('.post-time')?.textContent?.trim()).not.toBe('');
      expect([count('comment-btn'), count('retweet-btn'), count('like-btn')]).toEqual(['2', '3', '4']);
    });

    it('shows the text literally, never as HTML', async () => {
      await show(makePost(7, { content: '<img src=x onerror=alert(1)> <b>bold</b>' }));

      const text = el().querySelector('.post-text-content')!;
      expect(text.textContent).toBe('<img src=x onerror=alert(1)> <b>bold</b>');
      expect(text.querySelector('img, b')).toBeNull();
    });

    it("uses the default avatar when the author has none, and the author's own otherwise", async () => {
      await show(makePost(7));
      expect(el().querySelector('img.post-avatar')?.getAttribute('src')).toContain('default_profile_images');

      await show(makePost(8, { user: makeUser({ id: 2, username: 'other', avatarUrl: 'https://img.test/a.png' }) }));
      expect(el().querySelector('img.post-avatar')?.getAttribute('src')).toBe('https://img.test/a.png');
    });

    it('says who a reply is replying to', async () => {
      await show(makePost(7, { parentPostId: 3, replyToUsername: 'bob' }));

      expect(el().querySelector('.reply-context')?.textContent).toContain('Replying to @bob');
    });

    it('has no reply line for a top-level post', async () => {
      await show(makePost(7));

      expect(el().querySelector('.reply-context')).toBeNull();
    });

    it("names who reposted it, or says 'You' for your own repost", async () => {
      await show(makePost(7, { retweetedBy: bob }));
      expect(el().querySelector('.repost-banner')?.textContent).toContain('Bob reposted');

      await show(makePost(7, { retweetedBy: me }));
      expect(el().querySelector('.repost-banner')?.textContent).toContain('You reposted');
    });

    it('has no repost banner on an original', async () => {
      await show(makePost(7));

      expect(el().querySelector('.repost-banner')).toBeNull();
    });

    it('marks a post you liked and one you reposted', async () => {
      await show(makePost(7, { isLiked: true, isRetweeted: true }));

      expect(button('like-btn').classList).toContain('liked');
      expect(button('retweet-btn').classList).toContain('retweeted');
      expect(button('retweet-btn').title).toBe('Undo repost');
    });

    it('offers no like or repost state on a fresh post', async () => {
      await show(makePost(7));

      expect(button('like-btn').classList).not.toContain('liked');
      expect(button('retweet-btn').classList).not.toContain('retweeted');
      expect(button('retweet-btn').title).toBe('Repost');
    });

    it('resets when the page hands it another post', async () => {
      await show(makePost(7, { likesCount: 1 }));
      fixture.componentRef.setInput('post', makePost(8, { content: 'Another one', likesCount: 9 }));
      await settle();

      expect(el().querySelector('.post-text-content')?.textContent?.trim()).toBe('Another one');
      expect(count('like-btn')).toBe('9');
    });
  });

  describe('hashtags', () => {
    const hashtagLinks = () => [...el().querySelectorAll<HTMLAnchorElement>('.post-text-content a.hashtag')];

    it('are links to their page', async () => {
      await show(makePost(7, { content: 'Sunset over the #Bay, then #coffee' }));

      expect(hashtagLinks().map((a) => [a.textContent, a.getAttribute('href')])).toEqual([
        ['#Bay', '/hashtag/bay'],
        ['#coffee', '/hashtag/coffee'],
      ]);
      expect(el().querySelector('.post-text-content')?.textContent).toBe('Sunset over the #Bay, then #coffee');
    });

    it('go to their page when clicked, and do not open the thread', async () => {
      const navigateByUrl = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
      await show(makePost(7, { content: 'Sunset #Bay' }));

      hashtagLinks()[0].click();

      expect(navigateByUrl).toHaveBeenCalledOnce();
      expect(TestBed.inject(Router).serializeUrl(navigateByUrl.mock.calls[0][0] as never)).toBe('/hashtag/bay');
      expect(navigate).not.toHaveBeenCalled(); // the thread was not opened
    });

    it('are also links on the main post of a thread', async () => {
      await show(makePost(7, { content: 'Sunset #Bay' }), true);

      expect(hashtagLinks()).toHaveLength(1);
    });
  });

  describe('mentions', () => {
    const mentionLinks = () => [...el().querySelectorAll<HTMLAnchorElement>('.post-text-content a.mention')];

    it('of real accounts are links to their profiles, and unknown names stay text', async () => {
      await show(makePost(7, { content: 'Hello @Bob and @ghost', mentions: ['Bob'] }));

      expect(mentionLinks().map((a) => [a.textContent, a.getAttribute('href')])).toEqual([['@Bob', '/profile/Bob']]);
      expect(el().querySelector('.post-text-content')?.textContent).toBe('Hello @Bob and @ghost');
    });

    it('go to the profile when clicked, and do not open the thread', async () => {
      const navigateByUrl = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
      await show(makePost(7, { content: 'Hello @Bob', mentions: ['Bob'] }));

      mentionLinks()[0].click();

      expect(navigateByUrl).toHaveBeenCalledOnce();
      expect(TestBed.inject(Router).serializeUrl(navigateByUrl.mock.calls[0][0] as never)).toBe('/profile/Bob');
      expect(navigate).not.toHaveBeenCalled();
    });

    it('are not links when the post says it names nobody', async () => {
      await show(makePost(7, { content: 'Hello @Bob', mentions: [] }));

      expect(mentionLinks()).toHaveLength(0);
    });
  });

  describe('images', () => {
    const image = (letter: string, extension = 'png') => `/uploads/${letter.repeat(32)}.${extension}`;
    const grid = () => el().querySelector<HTMLElement>('.media-grid');
    const pictures = () => [...el().querySelectorAll<HTMLImageElement>('.media-grid img')];
    const sources = () => pictures().map((p) => p.getAttribute('src'));

    it('shows nothing where a post has no images', async () => {
      await show(makePost(7, { mediaUrls: [] }));

      expect(grid()).toBeNull();
    });

    it('loads an image from the API, not from the page, with a description, lazily', async () => {
      await show(makePost(7, { mediaUrls: [image('a')] }));

      expect(sources()).toEqual([`http://localhost:5168${image('a')}`]);
      expect(pictures()[0].alt).toBe('Attached image');
      expect(pictures()[0].getAttribute('loading')).toBe('lazy');
    });

    it('links each image to the full picture in a new tab that cannot reach back to the app', async () => {
      await show(makePost(7, { mediaUrls: [image('a')] }));

      const link = el().querySelector<HTMLAnchorElement>('.media-item')!;

      expect(link.getAttribute('href')).toBe(`http://localhost:5168${image('a')}`);
      expect(link.target).toBe('_blank');
      expect(link.rel).toBe('noopener noreferrer');
    });

    it.each([1, 2, 3, 4])('lays out %i image(s) in a grid of that size', async (count) => {
      const urls = Array.from({ length: count }, (_, i) => image('abcd'[i]));
      await show(makePost(7, { mediaUrls: urls }));

      expect(grid()!.classList).toContain(`count-${count}`);
      expect(pictures()).toHaveLength(count);
      expect(sources()).toEqual(urls.map((u) => `http://localhost:5168${u}`)); // in the order of the post
    });

    it('shows the text before the images', async () => {
      await show(makePost(7, { content: 'caption', mediaUrls: [image('a')] }));

      const text = el().querySelector('.post-text-content')!;
      expect(text.nextElementSibling).toBe(grid());
    });

    it('leaves out pictures that are not ours, such as old posts that point at another site', async () => {
      await show(makePost(7, { mediaUrls: ['https://tracker.example/pixel.png', image('a'), 'javascript:alert(1)', 'data:image/png;base64,AAAA'] }));

      expect(sources()).toEqual([`http://localhost:5168${image('a')}`]);
      expect(grid()!.classList).toContain('count-1');
    });

    it('shows no grid at all when none of the pictures are ours', async () => {
      await show(makePost(7, { mediaUrls: ['https://tracker.example/pixel.png'] }));

      expect(grid()).toBeNull();
      expect(el().querySelector('img[src*="tracker"]')).toBeNull();
    });

    it('copes with a post that has no list of images at all', async () => {
      await show(makePost(7, { mediaUrls: undefined as unknown as string[] }));

      expect(grid()).toBeNull();
    });

    it('does not open the thread when an image is clicked', async () => {
      await show(makePost(7, { mediaUrls: [image('a')] }));
      const link = el().querySelector<HTMLAnchorElement>('.media-item')!;
      link.addEventListener('click', (event) => event.preventDefault()); // do not really leave the test page

      link.click();

      expect(navigate).not.toHaveBeenCalled();
    });

    it('shows them on the main post of a thread too', async () => {
      await show(makePost(7, { mediaUrls: [image('a'), image('b')] }), true);

      expect(pictures()).toHaveLength(2);
    });
  });

  describe('the Edited label', () => {
    const label = () => el().querySelector<HTMLElement>('.post-edited');

    it('shows next to the time once the author has changed the text, and says when', async () => {
      await show(makePost(7, { editedAt: '2026-09-21T10:00:00Z' }));

      expect(label()?.textContent).toBe('Edited');
      expect(label()?.title).toBe(`Edited ${new Date('2026-09-21T10:00:00Z').toLocaleString()}`);
      expect(label()?.previousElementSibling?.textContent).toBe('·');
      expect(label()?.previousElementSibling?.previousElementSibling?.classList).toContain('post-time');
    });

    it('is not there on a post that was never edited', async () => {
      await show(makePost(7, { editedAt: null }));

      expect(label()).toBeNull();
    });

    it('is not there when the server sent no such field (an older answer)', async () => {
      await show(makePost(7, { editedAt: undefined as unknown as null }));

      expect(label()).toBeNull();
    });

    it('is on the main post of a thread too', async () => {
      await show(makePost(7, { editedAt: '2026-09-21T10:00:00Z' }), true);

      expect(label()).not.toBeNull();
    });
  });

  describe('edit', () => {
    const mine = (id = 7, overrides: Partial<Post> = {}) => makePost(id, { user: me, userId: me.id, content: 'first draft', ...overrides });
    const editButton = () => el().querySelector<HTMLButtonElement>('.edit-post-btn');
    const editor = () => el().querySelector('app-post-editor');
    const box = () => el().querySelector<HTMLTextAreaElement>('app-post-editor textarea')!;
    const saveButton = () => el().querySelector<HTMLButtonElement>('.editor-save')!;
    const textNow = () => el().querySelector('.post-text-content')?.textContent?.trim();

    async function typeAndSave(text: string) {
      box().value = text;
      box().dispatchEvent(new Event('input'));
      await settle();
      saveButton().click();
      await settle();
    }

    it('is offered on your own posts only, next to Delete', async () => {
      await show(mine());
      expect(editButton()).not.toBeNull();
      expect(editButton()!.title).toBe('Edit');
      expect(editButton()!.getAttribute('aria-label')).toBe('Edit post');
      expect(editButton()!.parentElement).toBe(button('delete-post-btn').parentElement);

      await show(makePost(8));
      expect(editButton()).toBeNull();
    });

    it('is not offered on somebody else\'s post that you reposted', async () => {
      await show(makePost(8, { retweetedBy: me })); // I reposted somebody else's post

      expect(editButton()).toBeNull();
    });

    it('swaps the text for a box with the text in it, and hides the pencil meanwhile', async () => {
      await show(mine());

      editButton()!.click();
      await settle();

      expect(editor()).not.toBeNull();
      expect(box().value).toBe('first draft');
      expect(textNow()).toBeUndefined(); // the plain text is not shown twice
      expect(editButton()).toBeNull();
      expect(button('delete-post-btn')).not.toBeNull();
    });

    it('does not open the thread', async () => {
      await show(mine());

      editButton()!.click();
      await settle();
      box().click();

      expect(navigate).not.toHaveBeenCalled();
    });

    it('keeps the pictures and the buttons while editing', async () => {
      await show(mine(7, { mediaUrls: [`/uploads/${'a'.repeat(32)}.png`], likesCount: 3 }));

      editButton()!.click();
      await settle();

      expect(el().querySelectorAll('.media-grid img')).toHaveLength(1);
      expect(count('like-btn')).toBe('3');
    });

    describe('saving', () => {
      it('sends the new text to the server, and shows it when the server agrees', async () => {
        await show(mine(7, { retweetsCount: 2 }));
        editButton()!.click();
        await settle();

        await typeAndSave('second draft');
        const request = http().expectOne(`${API}/posts/7`);
        expect(request.request.method).toBe('PUT');
        expect(request.request.body).toEqual({ content: 'second draft' });
        expect(editor()).not.toBeNull(); // still open while the server thinks
        expect(saveButton().textContent?.trim()).toBe('Saving...');
        expect(changes).toEqual([]);

        request.flush(mine(7, { content: 'second draft', retweetsCount: 2, editedAt: '2026-09-21T10:00:00Z' }));
        await settle();

        expect(editor()).toBeNull();
        expect(textNow()).toBe('second draft');
        expect(el().querySelector('.post-edited')).not.toBeNull();
        expect(editButton()).not.toBeNull();
        expect(changes.map((p) => [p.content, p.editedAt])).toEqual([['second draft', '2026-09-21T10:00:00Z']]);
      });

      it('links the names the server found in the new text', async () => {
        await show(mine());
        editButton()!.click();
        await settle();

        await typeAndSave('thanks @Bob and @ghost');
        http().expectOne(`${API}/posts/7`).flush(mine(7, { content: 'thanks @Bob and @ghost', mentions: ['Bob'], editedAt: '2026-09-21T10:00:00Z' }));
        await settle();

        const links = [...el().querySelectorAll<HTMLAnchorElement>('.post-text-content a.mention')];
        expect(links.map((a) => [a.textContent, a.getAttribute('href')])).toEqual([['@Bob', '/profile/Bob']]);
      });

      it('can be done again straight away, with the box working as it did the first time', async () => {
        await show(mine());
        editButton()!.click();
        await settle();
        await typeAndSave('second draft');
        http().expectOne(`${API}/posts/7`).flush(mine(7, { content: 'second draft', editedAt: '2026-09-21T10:00:00Z' }));
        await settle();

        editButton()!.click();
        await settle();
        expect(box().value).toBe('second draft');
        expect(box().disabled).toBe(false);
        expect(saveButton().textContent?.trim()).toBe('Save');

        await typeAndSave('third draft');
        const request = http().expectOne(`${API}/posts/7`);
        expect(request.request.body).toEqual({ content: 'third draft' });
        request.flush(mine(7, { content: 'third draft', editedAt: '2026-09-21T11:00:00Z' }));
        await settle();

        expect(textNow()).toBe('third draft');
      });

      it('keeps the repost banner on an entry that is a repost', async () => {
        await show(mine(7, { retweetedBy: bob }));
        editButton()!.click();
        await settle();

        await typeAndSave('second draft');
        http().expectOne(`${API}/posts/7`).flush(mine(7, { content: 'second draft', editedAt: '2026-09-21T10:00:00Z', retweetedBy: null }));
        await settle();

        expect(el().querySelector('.repost-banner')?.textContent).toContain('Bob reposted');
        expect(changes[0].retweetedBy).toEqual(bob);
      });

      it('sends one request however often Save is pressed', async () => {
        await show(mine());
        editButton()!.click();
        await settle();
        await typeAndSave('second draft');

        fixture.componentInstance.saveEdit('second draft');
        saveButton().click();

        http().expectOne(`${API}/posts/7`).flush(mine(7, { content: 'second draft' }));
      });

      it('does not lose the typing when the post is liked meanwhile', async () => {
        await show(mine());
        editButton()!.click();
        await settle();
        box().value = 'half written';
        box().dispatchEvent(new Event('input'));

        button('like-btn').click();
        http().expectOne(`${API}/likes/toggle/7`).flush({ liked: true });
        await settle();

        expect(editor()).not.toBeNull();
        expect(box().value).toBe('half written');
      });
    });

    describe('cancelling', () => {
      it('closes the box, keeps the old text and sends nothing', async () => {
        await show(mine());
        editButton()!.click();
        await settle();
        box().value = 'never mind';
        box().dispatchEvent(new Event('input'));

        el().querySelector<HTMLButtonElement>('.editor-cancel')!.click();
        await settle();

        expect(editor()).toBeNull();
        expect(textNow()).toBe('first draft');
        expect(changes).toEqual([]);
        http().expectNone(`${API}/posts/7`);
      });

      it('starts again from the post\'s own text the next time', async () => {
        await show(mine());
        editButton()!.click();
        await settle();
        box().value = 'never mind';
        box().dispatchEvent(new Event('input'));
        el().querySelector<HTMLButtonElement>('.editor-cancel')!.click();
        await settle();

        editButton()!.click();
        await settle();

        expect(box().value).toBe('first draft');
      });
    });

    describe('when the server says no', () => {
      it('keeps the box open with the typing, and says what happened', async () => {
        await show(mine());
        editButton()!.click();
        await settle();

        await typeAndSave('second draft');
        http().expectOne(`${API}/posts/7`).flush({ message: 'boom' }, { status: 500, statusText: 'Server Error' });
        await settle();

        expect(editor()).not.toBeNull();
        expect(box().value).toBe('second draft');
        expect(el().querySelector('[role="alert"]')?.textContent).toBe('boom');
        expect(saveButton().disabled).toBe(false); // it can be tried again
        expect(textNow()).toBeUndefined();
        expect(changes).toEqual([]);
      });

      it('uses its own words when the server gave none', async () => {
        await show(mine());
        editButton()!.click();
        await settle();

        await typeAndSave('second draft');
        http().expectOne(`${API}/posts/7`).error(new ProgressEvent('error'));
        await settle();

        expect(el().querySelector('[role="alert"]')?.textContent).toBe('Could not save your changes. Please try again.');
      });

      it('lets the person try again, and shows nothing of the failure afterwards', async () => {
        await show(mine());
        editButton()!.click();
        await settle();
        await typeAndSave('second draft');
        http().expectOne(`${API}/posts/7`).flush({ message: 'boom' }, { status: 500, statusText: 'Server Error' });
        await settle();

        saveButton().click();
        await settle();
        expect(el().querySelector('[role="alert"]')).toBeNull(); // the old failure is gone while trying again
        http().expectOne(`${API}/posts/7`).flush(mine(7, { content: 'second draft', editedAt: '2026-09-21T10:00:00Z' }));
        await settle();

        expect(editor()).toBeNull();
        expect(textNow()).toBe('second draft');
      });

      it('forgets the failure when the box is closed and opened again', async () => {
        await show(mine());
        editButton()!.click();
        await settle();
        await typeAndSave('second draft');
        http().expectOne(`${API}/posts/7`).flush({ message: 'boom' }, { status: 500, statusText: 'Server Error' });
        await settle();

        el().querySelector<HTMLButtonElement>('.editor-cancel')!.click();
        await settle();
        editButton()!.click();
        await settle();

        expect(el().querySelector('[role="alert"]')).toBeNull();
      });
    });

    it('closes when the page hands the card a different post', async () => {
      await show(mine(7));
      editButton()!.click();
      await settle();

      fixture.componentRef.setInput('post', mine(8, { content: 'another post of mine' }));
      await settle();

      expect(editor()).toBeNull();
      expect(textNow()).toBe('another post of mine');
    });
  });

  describe('delete', () => {
    it('is offered on your own posts only', async () => {
      await show(makePost(7, { user: me, userId: me.id }));
      expect(el().querySelector('.delete-post-btn')).not.toBeNull();

      await show(makePost(8));
      expect(el().querySelector('.delete-post-btn')).toBeNull();
    });

    it('asks first and does nothing when you say no', async () => {
      const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
      await show(makePost(7, { user: me, userId: me.id }));

      button('delete-post-btn').click();

      expect(confirm).toHaveBeenCalledOnce();
      http().expectNone(`${API}/posts/7`);
      expect(deleted).toEqual([]);
    });

    it('deletes the post and tells the page which one', async () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      await show(makePost(7, { user: me, userId: me.id }));

      button('delete-post-btn').click();
      const request = http().expectOne(`${API}/posts/7`);
      expect(request.request.method).toBe('DELETE');
      request.flush(null, { status: 204, statusText: 'No Content' });

      expect(deleted).toEqual([7]);
      expect(navigate).not.toHaveBeenCalled();
    });

    it('keeps the post and says so when the server refuses', async () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const alert = vi.spyOn(window, 'alert').mockImplementation(() => {});
      await show(makePost(7, { user: me, userId: me.id }));

      button('delete-post-btn').click();
      http().expectOne(`${API}/posts/7`).flush({ message: 'nope' }, { status: 500, statusText: 'Server Error' });

      expect(deleted).toEqual([]);
      expect(alert).toHaveBeenCalledWith('Could not delete the post. Please try again.');
    });
  });

  describe('like', () => {
    it('counts immediately, then confirms with the server', async () => {
      await show(makePost(7, { likesCount: 4 }));

      button('like-btn').click();
      await settle();
      expect(count('like-btn')).toBe('5'); // before the server has answered
      expect(button('like-btn').classList).toContain('liked');

      const request = http().expectOne(`${API}/likes/toggle/7`);
      expect(request.request.method).toBe('POST');
      request.flush({ liked: true });
      await settle();

      expect(count('like-btn')).toBe('5');
      expect(changes.map((p) => [p.isLiked, p.likesCount])).toEqual([[true, 5]]);
    });

    it('takes the like back when clicked again', async () => {
      await show(makePost(7, { likesCount: 4, isLiked: true }));

      button('like-btn').click();
      await settle();
      http().expectOne(`${API}/likes/toggle/7`).flush({ liked: false });

      expect(count('like-btn')).toBe('3');
      expect(button('like-btn').classList).not.toContain('liked');
    });

    it('puts everything back when the server fails', async () => {
      await show(makePost(7, { likesCount: 4 }));

      button('like-btn').click();
      http().expectOne(`${API}/likes/toggle/7`).flush({ message: 'x' }, { status: 500, statusText: 'Server Error' });
      await settle();

      expect(count('like-btn')).toBe('4');
      expect(button('like-btn').classList).not.toContain('liked');
      expect(changes.map((p) => p.isLiked)).toEqual([true, false]); // the page hears about both
    });

    it('does not open the thread', async () => {
      await show(makePost(7));

      button('like-btn').click();
      http().expectOne(`${API}/likes/toggle/7`).flush({ liked: true });

      expect(navigate).not.toHaveBeenCalled();
    });
  });

  describe('repost', () => {
    it('counts immediately, then confirms with the server', async () => {
      await show(makePost(7, { retweetsCount: 2 }));

      button('retweet-btn').click();
      await settle();
      expect(count('retweet-btn')).toBe('3');
      expect(button('retweet-btn').classList).toContain('retweeted');

      const request = http().expectOne(`${API}/retweets/toggle/7`);
      expect(request.request.method).toBe('POST');
      request.flush({ retweeted: true });

      expect(changes.map((p) => [p.isRetweeted, p.retweetsCount])).toEqual([[true, 3]]);
    });

    it('takes the repost back when clicked again', async () => {
      await show(makePost(7, { retweetsCount: 2, isRetweeted: true }));

      button('retweet-btn').click();
      http().expectOne(`${API}/retweets/toggle/7`).flush({ retweeted: false });
      await settle();

      expect(count('retweet-btn')).toBe('1');
      expect(button('retweet-btn').classList).not.toContain('retweeted');
    });

    it('puts everything back when the server fails', async () => {
      await show(makePost(7, { retweetsCount: 2 }));

      button('retweet-btn').click();
      http().expectOne(`${API}/retweets/toggle/7`).flush({ message: 'x' }, { status: 500, statusText: 'Server Error' });
      await settle();

      expect(count('retweet-btn')).toBe('2');
      expect(button('retweet-btn').classList).not.toContain('retweeted');
    });
  });

  describe('opening the thread', () => {
    it('opens it when the card is clicked', async () => {
      await show(makePost(7));

      el().querySelector<HTMLElement>('.post-text-content')!.click();

      expect(navigate).toHaveBeenCalledWith(['/post', 7]);
      expect(el().querySelector('.post-card')?.classList).toContain('clickable');
    });

    it('opens it from the reply button', async () => {
      await show(makePost(7));

      button('comment-btn').click();

      expect(navigate).toHaveBeenCalledOnce();
      expect(navigate).toHaveBeenCalledWith(['/post', 7]);
    });

    it('opens the original when the entry is somebody\'s repost', async () => {
      await show(makePost(7, { retweetedBy: bob }));

      el().querySelector<HTMLElement>('.post-text-content')!.click();

      expect(navigate).toHaveBeenCalledWith(['/post', 7]);
    });

    it('does nothing when it is already the main post of a thread', async () => {
      await show(makePost(7), true);

      el().querySelector<HTMLElement>('.post-text-content')!.click();
      button('comment-btn').click();

      expect(navigate).not.toHaveBeenCalled();
      expect(el().querySelector('.post-card')?.classList).not.toContain('clickable');
      expect(el().querySelector('.post-card')?.classList).toContain('focus');
    });
  });

  describe('postEntryKey', () => {
    it('tells an original from somebody\'s repost of the same post', () => {
      expect(postEntryKey(makePost(7))).toBe('7-0');
      expect(postEntryKey(makePost(7, { retweetedBy: bob }))).toBe('7-3');
      expect(postEntryKey(makePost(7))).not.toBe(postEntryKey(makePost(7, { retweetedBy: bob })));
    });
  });
});
