import { environment } from '../../environments/environment';

/** The images this API hands out when one is uploaded: a path under /uploads with a name it made up. */
const UPLOADED_IMAGE = /^\/uploads\/[0-9a-f]{32}\.(png|jpg|gif|webp)$/;

/** What the file picker may offer, and what the API accepts. */
export const IMAGE_TYPES = ['image/png', 'image/jpeg', 'image/gif', 'image/webp'];
export const MAX_IMAGE_BYTES = 5 * 1024 * 1024;
export const MAX_IMAGES = 4;

/**
 * The address to load an image of a post from, or null when it is not one of ours. Posts store the path the API
 * answered with ("/uploads/..."), which the browser must fetch from the API's own address; anything else (an old
 * post that points at another site, or something odd) is not shown, so a post can never make a reader's browser
 * contact somebody else's server.
 */
export function mediaSrc(url: string): string | null {
  return UPLOADED_IMAGE.test(url) ? new URL(environment.apiUrl).origin + url : null;
}
