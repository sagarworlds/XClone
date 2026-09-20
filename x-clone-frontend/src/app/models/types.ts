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
