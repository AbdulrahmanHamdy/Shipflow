export interface UserDto {
  id: string;
  email: string;
  fullName: string;
  roles: string[];
}

export interface LoginResult {
  accessToken: string;
  tokenType: string;
  expiresAtUtc: string;
  user: UserDto;
}

export interface CurrentUserResponse {
  id: string | null;
  email: string | null;
  roles: string[];
}

export interface RegisterCommand {
  email: string;
  password: string;
  fullName: string;
}

export interface LoginCommand {
  email: string;
  password: string;
}

export type PlatformRole = 'Customer' | 'CompanyAdmin' | 'Dispatcher' | 'Driver' | 'PlatformAdmin';
