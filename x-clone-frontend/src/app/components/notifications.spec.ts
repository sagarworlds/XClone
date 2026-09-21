import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NotificationsService } from '../services/notifications.service';
import { API, answerBackgroundRequests, expectPage, http, makeNotification, makeUser, pageOf, provideAppTesting, range, signInAs } from '../../testing/helpers';
import { AppNotification } from '../models/types';
import { NotificationsComponent } from './notifications';

const READ_ALL = `${API}/notifications/read-all`;

describe('NotificationsComponent', () => {
  let fixture: ComponentFixture<NotificationsComponent>;
  const el = () => fixture.nativeElement as HTMLElement;
  const items = () => [...el().querySelectorAll<HTMLAnchorElement>('a.notification')];
  const settle = () => fixture.whenStable();

  /** Opens the page and answers its first request with `firstPage`. A full page of 20 is assumed to have more after it, as the API would say; anything shorter is everything. */
  async function open(
    firstPage: AppNotification[],
    unreadBadge = firstPage.filter((n) => !n.isRead).length,
    nextCursor: string | null = firstPage.length >= 20 ? `after-${firstPage[firstPage.length - 1].id}` : null,
  ) {
    fixture = TestBed.createComponent(NotificationsComponent);
    fixture.detectChanges();
    await settle();
    answerBackgroundRequests(unreadBadge);
    expectPage('/notifications').flush(pageOf(firstPage, nextCursor));
    await settle();
  }

  const answerReadAll = () => http().expectOne(READ_ALL).flush(null, { status: 204, statusText: 'No Content' });

  beforeEach(() => {
    localStorage.clear();
    signInAs(makeUser());
    TestBed.configureTestingModule({ imports: [NotificationsComponent], providers: provideAppTesting() });
  });

  afterEach(() => localStorage.clear());

  it('says what happened, who did it, and shows the text of the post', async () => {
    const reply = makeNotification(1, {
      type: 'reply',
      postId: 501,
      postContent: 'Great point!',
      actor: makeUser({ id: 2, username: 'alice', displayName: 'Alice' }),
    });
    const repost = makeNotification(2, {
      type: 'repost',
      postId: 502,
      postContent: 'My hot take',
      isRead: true,
      actor: makeUser({ id: 3, username: 'bob', displayName: 'Bob' }),
    });

    await open([reply, repost]);
    answerReadAll();

    expect(items()).toHaveLength(2);
    expect(items()[0].textContent).toContain('Alice');
    expect(items()[0].textContent).toContain('replied to your post');
    expect(items()[0].textContent).toContain('Great point!');
    expect(items()[1].textContent).toContain('Bob');
    expect(items()[1].textContent).toContain('reposted your post');
    expect(items()[1].textContent).toContain('My hot take');
  });

  it('opens the post when a notification is clicked', async () => {
    await open([makeNotification(1, { postId: 501 }), makeNotification(2, { postId: 502, isRead: true })]);
    answerReadAll();

    expect(items().map((a) => a.getAttribute('href'))).toEqual(['/post/501', '/post/502']);
  });

  it('highlights the notifications that were unread when the page opened', async () => {
    await open([makeNotification(1), makeNotification(2, { isRead: true })]);
    answerReadAll();

    expect(items()[0].classList).toContain('unread');
    expect(items()[1].classList).not.toContain('unread');
  });

  it('falls back to the username when there is no display name', async () => {
    await open([makeNotification(1, { actor: makeUser({ id: 2, username: 'nameless', displayName: '' }) })]);
    answerReadAll();

    expect(items()[0].textContent).toContain('nameless');
  });

  it('shows the type as an icon: a speech bubble for replies, arrows for reposts, an @ for mentions', async () => {
    await open([makeNotification(1, { type: 'reply' }), makeNotification(2, { type: 'repost' }), makeNotification(3, { type: 'mention' })]);
    answerReadAll();

    const icons = items().map((a) => a.querySelector('.type-icon')?.textContent?.trim());
    expect(icons).toEqual(['chat_bubble', 'repeat', 'alternate_email']);
    expect(items()[1].querySelector('.type-icon')?.classList).toContain('repost');
    expect(items()[2].querySelector('.type-icon')?.classList).toContain('mention');
    expect(items()[0].querySelector('.type-icon')?.classList).not.toContain('mention');
  });

  it('says what each kind of notification is about', async () => {
    const actor = makeUser({ id: 2, username: 'other', displayName: 'Other', email: '' });
    await open([
      makeNotification(1, { type: 'reply', actor }),
      makeNotification(2, { type: 'repost', actor }),
      makeNotification(3, { type: 'mention', actor }),
    ]);
    answerReadAll();

    expect(items().map((a) => a.querySelector('.summary')?.textContent?.replace(/\s+/g, ' ').trim().replace(/ · .*$/, ''))).toEqual([
      'Other replied to your post',
      'Other reposted your post',
      'Other mentioned you in a post',
    ]);
  });

  it('opens the post that mentions you', async () => {
    await open([makeNotification(3, { type: 'mention', postId: 321, postContent: 'hello @me' })]);
    answerReadAll();

    expect(items()[0].getAttribute('href')).toBe('/post/321');
    expect(items()[0].querySelector('.excerpt')?.textContent).toBe('hello @me');
  });

  describe('marking as read', () => {
    it('marks everything read on the server and clears the badge when there was something new', async () => {
      await open([makeNotification(1), makeNotification(2)], 2);

      const request = http().expectOne(READ_ALL);
      expect(request.request.method).toBe('POST');
      request.flush(null, { status: 204, statusText: 'No Content' });
      expect(TestBed.inject(NotificationsService).unreadCount()).toBe(0);
    });

    it('does not bother the server when everything was read already', async () => {
      await open([makeNotification(1, { isRead: true })]);

      http().expectNone(READ_ALL);
    });
  });

  describe('paging', () => {
    const page = (from: number, to: number) => range(from, to).map((id) => makeNotification(id, { isRead: true }));

    it('offers Load more after a full page and appends the next page', async () => {
      await open(page(1, 20));
      const button = () => el().querySelector<HTMLButtonElement>('.load-more-btn');
      expect(items()).toHaveLength(20);
      expect(button()?.textContent?.trim()).toBe('Load more');

      button()!.click();
      expectPage('/notifications', 'after-20').flush(pageOf(page(21, 23)));
      await settle();

      expect(items()).toHaveLength(23);
      expect(button()).toBeNull();
    });

    it('does not mark anything as read again for later pages', async () => {
      await open([...range(1, 20).map((id) => makeNotification(id, { isRead: true }))]);
      el().querySelector<HTMLButtonElement>('.load-more-btn')!.click();

      expectPage('/notifications', 'after-20').flush(pageOf([makeNotification(21, { isRead: false })]));
      await settle();

      http().expectNone(READ_ALL);
    });

    it('has no Load more when the first page is short', async () => {
      await open(page(1, 3));

      expect(el().querySelector('.load-more-btn')).toBeNull();
    });
  });

  describe('when there is nothing to show', () => {
    it('explains what will appear here', async () => {
      await open([]);

      expect(items()).toHaveLength(0);
      expect(el().textContent).toContain('Nothing to see yet');
      expect(el().textContent).toContain('replies to or reposts your posts, or mentions you');
    });

    it('says so when the notifications could not be loaded', async () => {
      fixture = TestBed.createComponent(NotificationsComponent);
      fixture.detectChanges();
      await settle();
      answerBackgroundRequests();

      expectPage('/notifications').flush({ message: 'boom' }, { status: 500, statusText: 'Server Error' });
      await settle();

      expect(el().textContent).toContain("Couldn't load notifications");
      expect(el().textContent).not.toContain('Nothing to see yet');
    });
  });
});
