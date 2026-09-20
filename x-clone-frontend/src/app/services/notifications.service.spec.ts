import { TestBed } from '@angular/core/testing';
import { API, http, makeUser, provideAppTesting, signInAs } from '../../testing/helpers';
import { ApiService } from './api.service';
import { NOTIFICATION_POLL_MS, NotificationsService } from './notifications.service';

const UNREAD = `${API}/notifications/unread-count`;
const READ_ALL = `${API}/notifications/read-all`;

describe('NotificationsService', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.useFakeTimers();
    TestBed.configureTestingModule({ providers: provideAppTesting() });
  });

  afterEach(() => {
    delete (document as { hidden?: boolean }).hidden;
    vi.restoreAllMocks();
    vi.useRealTimers();
    localStorage.clear();
  });

  /** Signs in, creates the service and lets its effect start the polling. */
  function start(): NotificationsService {
    signInAs(makeUser());
    const service = TestBed.inject(NotificationsService);
    TestBed.tick();
    return service;
  }

  const hideTab = () => Object.defineProperty(document, 'hidden', { configurable: true, get: () => true });

  it('asks for the unread count as soon as someone is signed in', () => {
    const service = start();

    http().expectOne(UNREAD).flush({ count: 3 });

    expect(service.unreadCount()).toBe(3);
  });

  it('does nothing while nobody is signed in', () => {
    const service = TestBed.inject(NotificationsService);
    TestBed.tick();

    vi.advanceTimersByTime(NOTIFICATION_POLL_MS * 3);

    http().expectNone(UNREAD);
    expect(service.unreadCount()).toBe(0);
  });

  it('asks again every poll interval', () => {
    const service = start();
    http().expectOne(UNREAD).flush({ count: 1 });

    vi.advanceTimersByTime(NOTIFICATION_POLL_MS - 1);
    http().expectNone(UNREAD);

    vi.advanceTimersByTime(1);
    http().expectOne(UNREAD).flush({ count: 2 });
    expect(service.unreadCount()).toBe(2);

    vi.advanceTimersByTime(NOTIFICATION_POLL_MS);
    http().expectOne(UNREAD).flush({ count: 5 });
    expect(service.unreadCount()).toBe(5);
  });

  it('does not poll while the tab is in the background, and carries on when it is back', () => {
    start();
    http().expectOne(UNREAD).flush({ count: 0 });

    hideTab();
    vi.advanceTimersByTime(NOTIFICATION_POLL_MS * 2);
    http().expectNone(UNREAD);

    delete (document as { hidden?: boolean }).hidden;
    vi.advanceTimersByTime(NOTIFICATION_POLL_MS);
    http().expectOne(UNREAD).flush({ count: 0 });
  });

  it('keeps the last number when a poll fails, and tries again next time', () => {
    const service = start();
    http().expectOne(UNREAD).flush({ count: 4 });

    vi.advanceTimersByTime(NOTIFICATION_POLL_MS);
    http().expectOne(UNREAD).flush({ message: 'boom' }, { status: 500, statusText: 'Server Error' });
    expect(service.unreadCount()).toBe(4);

    vi.advanceTimersByTime(NOTIFICATION_POLL_MS);
    http().expectOne(UNREAD).flush({ count: 6 });
    expect(service.unreadCount()).toBe(6);
  });

  describe('markAllRead()', () => {
    it('clears the badge straight away and tells the server', () => {
      const service = start();
      http().expectOne(UNREAD).flush({ count: 3 });

      service.markAllRead();

      expect(service.unreadCount()).toBe(0);
      const request = http().expectOne(READ_ALL);
      expect(request.request.method).toBe('POST');
      request.flush(null, { status: 204, statusText: 'No Content' });
    });

    it('is not undone by a count request that was already on its way', () => {
      const service = start();
      const stale = http().expectOne(UNREAD); // asked before the user opened the notifications

      service.markAllRead();
      http().expectOne(READ_ALL).flush(null, { status: 204, statusText: 'No Content' });
      stale.flush({ count: 5 });

      expect(service.unreadCount()).toBe(0);
    });

    it('checks with the server again when it could not be saved', () => {
      const service = start();
      http().expectOne(UNREAD).flush({ count: 3 });

      service.markAllRead();
      http().expectOne(READ_ALL).flush({ message: 'boom' }, { status: 500, statusText: 'Server Error' });

      http().expectOne(UNREAD).flush({ count: 3 });
      expect(service.unreadCount()).toBe(3);
    });
  });

  describe('signing out', () => {
    it('stops polling and clears the badge', () => {
      const service = start();
      http().expectOne(UNREAD).flush({ count: 2 });

      TestBed.inject(ApiService).isAuthenticated.set(false);
      TestBed.tick();

      expect(service.unreadCount()).toBe(0);
      vi.advanceTimersByTime(NOTIFICATION_POLL_MS * 3);
      http().expectNone(UNREAD);
    });

    it('starts again for the next person who signs in', () => {
      start();
      http().expectOne(UNREAD).flush({ count: 2 });
      const api = TestBed.inject(ApiService);
      api.isAuthenticated.set(false);
      TestBed.tick();

      api.isAuthenticated.set(true);
      TestBed.tick();

      http().expectOne(UNREAD).flush({ count: 1 });
    });
  });

  it('stops its timer when it is destroyed', () => {
    const started = vi.spyOn(globalThis, 'setInterval');
    const cleared = vi.spyOn(globalThis, 'clearInterval');
    start();
    http().expectOne(UNREAD).flush({ count: 0 });
    const pollTimer = started.mock.results[started.mock.calls.findIndex((call) => call[1] === NOTIFICATION_POLL_MS)].value;

    TestBed.resetTestingModule();

    expect(cleared).toHaveBeenCalledWith(pollTimer);
  });
});
