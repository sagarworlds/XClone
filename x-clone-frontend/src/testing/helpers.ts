import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Routes } from '@angular/router';
import { environment } from '../environments/environment';
import { AppNotification, Page, Post, TrendingHashtag, User } from '../app/models/types';

export const API = environment.apiUrl;

export function makeUser(overrides: Partial<User> = {}): User {
  return {
    id: 1,
    username: 'me',
    email: 'me@example.test',
    displayName: 'Me',
    bio: '',
    avatarUrl: '',
    createdAt: '2026-01-01T00:00:00Z',
    followersCount: 0,
    followingCount: 0,
    postsCount: 0,
    isFollowed: false,
    ...overrides,
  };
}

/** A post by someone else unless told otherwise. */
export function makePost(id: number, overrides: Partial<Post> = {}): Post {
  const author = overrides.user ?? makeUser({ id: 2, username: 'other', displayName: 'Other', email: '' });
  return {
    id,
    userId: author.id,
    content: `Post #${id}`,
    mediaUrls: [],
    likesCount: 0,
    retweetsCount: 0,
    repliesCount: 0,
    createdAt: '2026-09-20T10:00:00Z',
    updatedAt: '2026-09-20T10:00:00Z',
    user: author,
    isLiked: false,
    parentPostId: null,
    replyToUsername: null,
    mentions: [],
    isRetweeted: false,
    retweetedBy: null,
    ...overrides,
  };
}

export function makeNotification(id: number, overrides: Partial<AppNotification> = {}): AppNotification {
  return {
    id,
    type: 'reply',
    actor: makeUser({ id: 2, username: 'other', displayName: 'Other', email: '' }),
    postId: 100 + id,
    postContent: `Notification text #${id}`,
    isRead: false,
    createdAt: '2026-09-20T10:00:00Z',
    ...overrides,
  };
}

/** Puts a session where the app looks for it. Call before anything injects ApiService. */
export function signInAs(user: User): void {
  localStorage.setItem('token', 'test-token');
  localStorage.setItem('user', JSON.stringify(user));
}

/** Router and HttpClient (with its requests captured), the way the app configures them. */
export function provideAppTesting(routes: Routes = []) {
  return [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()];
}

export function http(): HttpTestingController {
  return TestBed.inject(HttpTestingController);
}

/** One page of a cursor-paged list, the way the API answers. */
export function pageOf<T>(items: T[], nextCursor: string | null = null): Page<T> {
  return { items, nextCursor };
}

/**
 * The single pending GET for a paged endpoint: the first page when `cursor` is null (no cursor is sent),
 * otherwise the page after that cursor. E.g. expectPage('/posts/feed'), expectPage('/posts/feed', 'c1').
 */
export function expectPage(path: string, cursor: string | null = null, take = 20): TestRequest {
  return http().expectOne(
    (r) =>
      r.method === 'GET' &&
      r.url === `${API}${path}` &&
      r.params.get('cursor') === cursor &&
      r.params.get('take') === String(take),
    `GET ${path}?take=${take}${cursor === null ? '' : `&cursor=${cursor}`}`,
  );
}

/** Answers what the shared sidebar and widgets ask for on their own, so a test can look at the page's requests. */
export function answerBackgroundRequests(unread = 0, trends: TrendingHashtag[] = []): void {
  for (const request of http().match((r) => r.url === `${API}/notifications/unread-count`)) {
    request.flush({ count: unread });
  }
  for (const request of http().match((r) => r.url === `${API}/users/suggestions`)) {
    request.flush([]);
  }
  for (const request of http().match((r) => r.url === `${API}/hashtags/trending`)) {
    request.flush(trends);
  }
}

export const range = (from: number, to: number) => Array.from({ length: to - from + 1 }, (_, i) => from + i);
