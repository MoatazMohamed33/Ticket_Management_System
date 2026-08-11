export type UserRole = 'Admin' | 'SupportAgent' | 'Customer';

export interface UserDto {
  id: string;
  email: string;
  displayName: string;
  role: UserRole;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
  user: UserDto;
}
