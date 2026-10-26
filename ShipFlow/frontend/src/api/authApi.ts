import { apiClient } from './client';
import { CurrentUserResponse, LoginCommand, LoginResult, RegisterCommand, UserDto } from './types';

export const authApi = {
  register: async (command: RegisterCommand, signal?: AbortSignal): Promise<UserDto> => {
    const res = await apiClient.post<UserDto>('/api/auth/register', command, { signal });
    return res.data;
  },

  login: async (command: LoginCommand, signal?: AbortSignal): Promise<LoginResult> => {
    const res = await apiClient.post<LoginResult>('/api/auth/login', command, { signal });
    return res.data;
  },

  getMe: async (signal?: AbortSignal): Promise<CurrentUserResponse> => {
    const res = await apiClient.get<CurrentUserResponse>('/api/auth/me', { signal });
    return res.data;
  },
};
