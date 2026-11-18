export type Difficulty = 'Easy' | 'Medium' | 'Hard';
export type RecipeSort = 'Newest' | 'TopRated' | 'MostLiked' | 'Trending';

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface Lookup {
  id: number;
  name: string;
}

export interface RecipeSummary {
  id: string;
  title: string;
  description: string;
  cuisine: string;
  category: string;
  totalMinutes: number;
  difficulty: Difficulty;
  coverImageUrl: string | null;
  authorName: string;
  likeCount: number;
  averageRating: number;
  ratingCount: number;
  isFeatured: boolean;
  createdAt: string;
}

export interface Ingredient {
  name: string;
  quantity: string;
}

export interface RecipeImage {
  url: string;
  altText: string;
}

export interface RecipeDetail {
  id: string;
  title: string;
  description: string;
  cuisineId: number;
  cuisine: string;
  categoryId: number;
  category: string;
  prepMinutes: number;
  cookMinutes: number;
  servings: number;
  difficulty: Difficulty;
  authorId: string;
  authorName: string;
  isFeatured: boolean;
  createdAt: string;
  updatedAt: string;
  ingredients: Ingredient[];
  steps: string[];
  images: RecipeImage[];
  tags: string[];
  likeCount: number;
  averageRating: number;
  ratingCount: number;
  commentCount: number;
  likedByMe: boolean;
  myRating: number | null;
  canEdit: boolean;
}

export interface SaveRecipeRequest {
  title: string;
  description: string;
  cuisineId: number;
  categoryId: number;
  prepMinutes: number;
  cookMinutes: number;
  servings: number;
  difficulty: Difficulty;
  ingredients: Ingredient[];
  steps: string[];
  images: RecipeImage[];
  tags: string[];
}

export interface RecipeQuery {
  q?: string;
  cuisineId?: number;
  categoryId?: number;
  maxCookMinutes?: number;
  minRating?: number;
  tag?: string;
  sort?: RecipeSort;
  page?: number;
  pageSize?: number;
}

export interface User {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  user: User;
}

export interface LikeState {
  liked: boolean;
  likeCount: number;
}

export interface RatingState {
  myRating: number;
  averageRating: number;
  ratingCount: number;
}

export interface Comment {
  id: string;
  userId: string;
  userName: string;
  text: string;
  photoUrl: string | null;
  createdAt: string;
  isHidden: boolean;
  canDelete: boolean;
}

export interface Activity {
  type: 'recipe' | 'comment' | 'user';
  description: string;
  actor: string;
  at: string;
}

export interface Dashboard {
  users: number;
  recipes: number;
  comments: number;
  likes: number;
  hiddenComments: number;
  latestActivity: Activity[];
}

export interface AdminUser {
  id: string;
  email: string;
  displayName: string;
  isActive: boolean;
  roles: string[];
  createdAt: string;
  recipeCount: number;
}

export interface AdminComment {
  id: string;
  recipeId: string;
  recipeTitle: string;
  userName: string;
  text: string;
  photoUrl: string | null;
  isHidden: boolean;
  createdAt: string;
}

export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}
