import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { AuthResponse } from '../models/types';
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

    const pagedCalls: [string, () => Observable<unknown>, string][] = [
      ['getFeed', () => api.getFeed(40, 20), '/posts/feed'],
      ['getUserPosts', () => api.getUserPosts(9, 40, 20), '/posts/user/9'],
      ['getReplies', () => api.getReplies(9, 40, 20), '/posts/9/replies'],
      ['getUserReplies', () => api.getUserReplies(9, 40, 20), '/posts/user/9/replies'],
      ['getNotifications', () => api.getNotifications(40, 20), '/notifications'],
    ];

    it.each(pagedCalls)('%s pages with skip and take', (_name, call, path) => {
      call().subscribe();

      const request = http().expectOne((r) => r.url === `${API}${path}`);
      expect(request.request.method).toBe('GET');
      expect(request.request.params.get('skip')).toBe('40');
      expect(request.request.params.get('take')).toBe('20');
      request.flush([]);
    });

    it('asks for the first 20 notifications by default', () => {
      api.getNotifications().subscribe();

      const request = http().expectOne((r) => r.url === `${API}/notifications`);
      expect(request.request.params.get('skip')).toBe('0');
      expect(request.request.params.get('take')).toBe('20');
      request.flush([makeNotification(1)]);
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

    it('posts and replies with just the content', () => {
      api.createPost('hello').subscribe();
      const post = http().expectOne(`${API}/posts`);
      expect(post.request.body).toMatchObject({ content: 'hello' });
      post.flush(makePost(1));

      api.createReply(1, 'a reply').subscribe();
      const reply = http().expectOne(`${API}/posts/1/replies`);
      expect(reply.request.method).toBe('POST');
      expect(reply.request.body).toEqual({ content: 'a reply' });
      reply.flush(makePost(2));
    });

    it('keeps the stored user in step when the profile is updated', () => {
      api.updateProfile({ displayName: 'Renamed', bio: '', avatarUrl: '' }).subscribe();
      http().expectOne(`${API}/users/profile`).flush(makeUser({ displayName: 'Renamed' }));

      expect(api.currentUser()?.displayName).toBe('Renamed');
      expect(JSON.parse(localStorage.getItem('user')!).displayName).toBe('Renamed');
    });
  });
});
