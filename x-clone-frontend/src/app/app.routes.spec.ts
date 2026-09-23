import { Route } from '@angular/router';
import { FeedComponent } from './components/feed';
import { FollowListComponent } from './components/follow-list';
import { HashtagComponent } from './components/hashtag';
import { LoginComponent } from './components/login';
import { NotificationsComponent } from './components/notifications';
import { PostDetailComponent } from './components/post-detail';
import { ProfileComponent } from './components/profile';
import { RegisterComponent } from './components/register';
import { SearchComponent } from './components/search';
import { followListMatcher, routes } from './app.routes';

/** The route table as the app really has it: which address opens which page, and who may open it. */
describe('routes', () => {
  const routeFor = (path: string): Route => routes.find((r) => r.path === path)!;

  /** What the route would load, and how many checks stand in front of it. */
  async function describeRoute(route: Route) {
    const component = await (route.loadComponent as () => Promise<unknown>)();
    return { component, guards: route.canActivate?.length ?? 0 };
  }

  it.each([
    ['home', FeedComponent],
    ['post/:id', PostDetailComponent],
    ['notifications', NotificationsComponent],
    ['hashtag/:tag', HashtagComponent],
    ['search', SearchComponent],
    ['profile/:username', ProfileComponent],
  ])('%s opens its page, for signed-in users only', async (path, component) => {
    expect(routeFor(path), `no route for ${path}`).toBeDefined();

    expect(await describeRoute(routeFor(path))).toEqual({ component, guards: 1 });
  });

  it('opens the followers and following lists through the matcher, for signed-in users only', async () => {
    const route = routes.find((r) => r.matcher === followListMatcher)!;

    expect(await describeRoute(route)).toEqual({ component: FollowListComponent, guards: 1 });
  });

  it.each([
    ['login', LoginComponent],
    ['register', RegisterComponent],
  ])('%s is for visitors who are not signed in', async (path, component) => {
    expect(await describeRoute(routeFor(path))).toEqual({ component, guards: 1 });
  });

  it('sends the empty address and unknown ones home', () => {
    expect(routeFor('')).toMatchObject({ redirectTo: 'home', pathMatch: 'full' });
    expect(routeFor('**')).toMatchObject({ redirectTo: 'home' });
  });

  it('has no other pages than the ones above', () => {
    const paths = routes.map((r) => r.path ?? '(matcher)').sort();

    expect(paths).toEqual(
      ['', '(matcher)', '**', 'hashtag/:tag', 'home', 'login', 'notifications', 'post/:id', 'profile/:username', 'register', 'search'].sort(),
    );
  });
});
