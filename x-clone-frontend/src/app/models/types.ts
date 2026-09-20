export interface User {
  id: number;
  username: string;
  email: string;
  displayName: string;
  bio: string;
  avatarUrl: string;
  createdAt: string;
  followersCount: number;
  followingCount: number;
  // The top-level posts and reposts on the profile's Posts tab (replies are not counted)
  postsCount: number;
  isFollowed: boolean;
}

export interface Post {
  id: number;
  userId: number;
  content: string;
  mediaUrls: string[];
  likesCount: number;
  retweetsCount: number;
  repliesCount: number;
  createdAt: string;
  updatedAt: string;
  user: User;
  isLiked: boolean;
  // Set when the post is a reply
  parentPostId: number | null;
  replyToUsername: string | null;
  // Retweet state: whether the current user retweeted it, and who retweeted it into this timeline entry
  isRetweeted: boolean;
  retweetedBy: User | null;
}

export interface AuthResponse {
  id: number;
  username: string;
  email: string;
  displayName: string;
  avatarUrl: string;
  token: string;
  expiresAt: string;
}

export interface AppNotification {
  id: number;
  // "reply": someone replied to your post (postId is the reply); "repost": someone reposted it (postId is your post)
  type: 'reply' | 'repost';
  actor: User;
  postId: number;
  postContent: string;
  isRead: boolean;
  createdAt: string;
}

// One page of a list that is read with cursors: pass nextCursor back to get the page after this one
export interface Page<T> {
  items: T[];
  // null when this was the last page
  nextCursor: string | null;
}
