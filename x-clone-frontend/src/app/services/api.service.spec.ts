import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { AuthResponse, Post } from '../models/types';
import { API, http, makeNotification, makePost, makeUser, provideAppTesting, signInAs } from '../../testing/helpers';
import { ApiService } from './api.service';

describe('ApiService', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: provideAppTesting() });
  });

  afterEach(() => {
    http().verify();
    localStorage.clear();
  });

  describe('session', () => {
    it('starts signed out when nothing is stored', () => {
      const api = TestBed.inject(ApiService);

      expect(api.isAuthenticated()).toBe(false);
      expect(api.currentUser()).toBeNull();
    });

    it('restores a stored session', () => {
      signInAs(makeUser({ username: 'stored' }));

      const api = TestBed.inject(ApiService);

      expect(api.isAuthenticated()).toBe(true);
      expect(api.currentUser()?.username).toBe('stored');
    });

    it('drops a session whose stored user is corrupted', () => {
      localStorage.setItem('token', 'test-token');
      localStorage.setItem('user', '{not json');
      vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

      const api = TestBed.inject(ApiService);

      expect(api.isAuthenticated()).toBe(false);
      expect(localStorage.getItem('token')).toBeNull();
    });

    it('logout() clears the session and goes to the login page', () => {
      signInAs(makeUser());
      const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
      const api = TestBed.inject(ApiService);

      api.logout();

      expect(api.isAuthenticated()).toBe(false);
      expect(api.currentUser()).toBeNull();
      expect(localStorage.getItem('token')).toBeNull();
      expect(localStorage.getItem('user')).toBeNull();
      expect(navigate).toHaveBeenCalledWith(['/login']);
    });
  });

  describe('signing in', () => {
    const authResponse: AuthResponse = {
      id: 7,
      username: 'newbie',
      email: 'newbie@example.test',
      displayName: 'New Bie',
      avatarUrl: '',
      token: 'jwt-from-server',
      expiresAt: '2026-09-21T00:00:00Z',
    };

    it('login() sends the credentials and turns the flat response into a session', () => {
      const api = TestBed.inject(ApiService);

      api.login({ usernameOrEmail: 'newbie', password: 'secret' }).subscribe();
      const request = http().expectOne(`${API}/auth/login`);
      expect(request.request.method).toBe('POST');
      expect(request.request.body).toEqual({ usernameOrEmail: 'newbie', password: 'secret' });
      request.flush(authResponse);

      expect(localStorage.getItem('token')).toBe('jwt-from-server');
      expect(api.isAuthenticated()).toBe(true);
      expect(api.currentUser()).toMatchObject({ id: 7, username: 'newbie', displayName: 'New Bie', followersCount: 0 });
      expect(JSON.parse(localStorage.getItem('user')!)).toMatchObject({ id: 7, username: 'newbie' });
    });

    it('register() signs the new user in as well', () => {
      const api = TestBed.inject(ApiService);

      api.register({ username: 'newbie', email: 'newbie@example.test', password: 'secret' }).subscribe();
      http().expectOne(`${API}/auth/register`).flush(authResponse);

      expect(api.isAuthenticated()).toBe(true);
      expect(localStorage.getItem('token')).toBe('jwt-from-server');
    });

    it('a rejected login leaves the user signed out', () => {
      const api = TestBed.inject(ApiService);
      const failed = vi.fn();

      api.login({ usernameOrEmail: 'x', password: 'y' }).subscribe({ error: failed });
      http().expectOne(`${API}/auth/login`).flush({ message: 'Invalid credentials' }, { status: 401, statusText: 'Unauthorized' });

      expect(failed).toHaveBeenCalled();
      expect(api.isAuthenticated()).toBe(false);
      expect(localStorage.getItem('token')).toBeNull();
    });
  });

  describe('requests', () => {
    let api: ApiService;
    beforeEach(() => {
      signInAs(makeUser());
      api = TestBed.inject(ApiService);
    });

    const pagedCalls: [string, (cursor: string | null) => Observable<unknown>, string][] = [
      ['getFeed', (cursor) => api.getFeed(cursor, 30), '/posts/feed'],
      ['getUserPosts', (cursor) => api.getUserPosts(9, cursor, 30), '/posts/user/9'],
      ['getReplies', (cursor) => api.getReplies(9, cursor, 30), '/posts/9/replies'],
      ['getUserReplies', (cursor) => api.getUserReplies(9, cursor, 30), '/posts/user/9/replies'],
      ['getNotifications', (cursor) => api.getNotifications(cursor, 30), '/notifications'],
    ];

    it.each(pagedCalls)('%s sends the cursor and the page size', (_name, call, path) => {
      call('abc-123_x').subscribe();

      const request = http().expectOne((r) => r.url === `${API}${path}`);
      expect(request.request.method).toBe('GET');
      expect(request.request.params.get('cursor')).toBe('abc-123_x');
      expect(request.request.params.get('take')).toBe('30');
      expect(request.request.params.has('skip')).toBe(false);
      request.flush({ items: [], nextCursor: null });
    });

    it.each(pagedCalls)('%s sends no cursor at all for the first page', (_name, call, path) => {
      call(null).subscribe();

      const request = http().expectOne((r) => r.url === `${API}${path}`);
      expect(request.request.params.has('cursor')).toBe(false);
      expect(request.request.params.get('take')).toBe('30');
      request.flush({ items: [], nextCursor: null });
    });

    it('asks for the first 20 of a list by default, and hands the answer back as a page', () => {
      const received = vi.fn();
      api.getNotifications().subscribe(received);

      const request = http().expectOne((r) => r.url === `${API}/notifications`);
      expect(request.request.params.has('cursor')).toBe(false);
      expect(request.request.params.get('take')).toBe('20');
      request.flush({ items: [makeNotification(1)], nextCursor: 'next' });
      expect(received).toHaveBeenCalledWith({ items: [makeNotification(1)], nextCursor: 'next' });
    });

    it('reads the unread count and marks everything read', () => {
      const counted = vi.fn();
      api.getUnreadNotificationCount().subscribe(counted);
      http().expectOne(`${API}/notifications/unread-count`).flush({ count: 4 });
      expect(counted).toHaveBeenCalledWith({ count: 4 });

      api.markNotificationsRead().subscribe();
      const request = http().expectOne(`${API}/notifications/read-all`);
      expect(request.request.method).toBe('POST');
      request.flush(null, { status: 204, statusText: 'No Content' });
    });

    it('toggles likes, reposts and follows with POSTs to the toggle endpoints', () => {
      api.likePost(5).subscribe();
      api.toggleRetweet(5).subscribe();
      api.followUser(9).subscribe();

      for (const path of ['/likes/toggle/5', '/retweets/toggle/5', '/follows/toggle/9']) {
        const request = http().expectOne(`${API}${path}`);
        expect(request.request.method).toBe('POST');
        request.flush({});
      }
    });

    it('posts and replies with the text, and no images unless given', () => {
      api.createPost('hello').subscribe();
      const post = http().expectOne(`${API}/posts`);
      expect(post.request.body).toEqual({ content: 'hello', mediaUrls: [] });
      post.flush(makePost(1));

      api.createReply(1, 'a reply').subscribe();
      const reply = http().expectOne(`${API}/posts/1/replies`);
      expect(reply.request.method).toBe('POST');
      expect(reply.request.body).toEqual({ content: 'a reply', mediaUrls: [] });
      reply.flush(makePost(2));
    });

    it('posts and replies with the images that were uploaded', () => {
      const images = ['/uploads/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png', '/uploads/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.jpg'];

      api.createPost('with pictures', images).subscribe();
      const post = http().expectOne(`${API}/posts`);
      expect(post.request.body).toEqual({ content: 'with pictures', mediaUrls: images });
      post.flush(makePost(1));

      api.createReply(1, 'reply with pictures', images).subscribe();
      const reply = http().expectOne(`${API}/posts/1/replies`);
      expect(reply.request.body).toEqual({ content: 'reply with pictures', mediaUrls: images });
      reply.flush(makePost(2));
    });

    it('changes the text of a post with a PUT that carries only the text, and gives back the post', () => {
      let answer: Post | undefined;

      api.updatePost(7, 'better words').subscribe((post) => (answer = post));
      const request = http().expectOne(`${API}/posts/7`);

      expect(request.request.method).toBe('PUT');
      expect(request.request.body).toEqual({ content: 'better words' });
      request.flush(makePost(7, { content: 'better words', editedAt: '2026-09-21T10:00:00Z' }));
      expect(answer?.editedAt).toBe('2026-09-21T10:00:00Z');
    });

    it('uploads an image as a multipart form with the file in the "file" field, and gives back its address', () => {
      const file = new File([new Uint8Array([1, 2, 3])], 'holiday.png', { type: 'image/png' });
      let answer: { url: string } | undefined;

      api.uploadMedia(file).subscribe((result) => (answer = result));
      const upload = http().expectOne(`${API}/media`);

      expect(upload.request.method).toBe('POST');
      expect(upload.request.body).toBeInstanceOf(FormData);
      const sent = (upload.request.body as FormData).get('file') as File;
      expect(sent.name).toBe('holiday.png');
      expect(sent.size).toBe(3);
      expect((upload.request.body as FormData).getAll('file')).toHaveLength(1);
      upload.flush({ url: '/uploads/cccccccccccccccccccccccccccccccc.png' });
      expect(answer).toEqual({ url: '/uploads/cccccccccccccccccccccccccccccccc.png' });
    });

    it('reads the posts of a hashtag a page at a time, with the tag made safe for the address', () => {
      api.getHashtagPosts('sunset').subscribe();
      const first = http().expectOne((r) => r.url === `${API}/posts/hashtag/sunset`);
      expect(first.request.method).toBe('GET');
      expect(first.request.params.get('take')).toBe('20');
      expect(first.request.params.has('cursor')).toBe(false);
      first.flush({ items: [], nextCursor: null });

      api.getHashtagPosts('日本語 と/#?', 'c1', 5).subscribe();
      const second = http().expectOne((r) => r.url.startsWith(`${API}/posts/hashtag/`) && r.params.get('cursor') === 'c1');
      expect(second.request.url).toBe(`${API}/posts/hashtag/${encodeURIComponent('日本語 と/#?')}`);
      expect(second.request.url).not.toMatch(/[ #?](?!$)/);
      expect(second.request.params.get('take')).toBe('5');
      second.flush({ items: [], nextCursor: null });
    });

    it('asks for the trending hashtags, five by default', () => {
      api.getTrendingHashtags().subscribe();
      const request = http().expectOne((r) => r.url === `${API}/hashtags/trending`);
      expect(request.request.method).toBe('GET');
      expect(request.request.params.get('take')).toBe('5');
      request.flush([]);

      api.getTrendingHashtags(2).subscribe();
      const other = http().expectOne((r) => r.url === `${API}/hashtags/trending`);
      expect(other.request.params.get('take')).toBe('2');
      other.flush([]);
    });

    it('keeps the stored user in step when the profile is updated', () => {
      api.updateProfile({ displayName: 'Renamed', bio: '', avatarUrl: '' }).subscribe();
      http().expectOne(`${API}/users/profile`).flush(makeUser({ displayName: 'Renamed' }));

      expect(api.currentUser()?.displayName).toBe('Renamed');
      expect(JSON.parse(localStorage.getItem('user')!).displayName).toBe('Renamed');
    });
  });
});
