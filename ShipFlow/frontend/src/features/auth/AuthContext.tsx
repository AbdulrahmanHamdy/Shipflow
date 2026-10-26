import React, { createContext, useContext, useEffect, useState, useCallback } from 'react';
import { authApi, setAccessToken, getAccessToken, setUnauthorizedListener } from '@/api';
import { CurrentUserResponse, LoginCommand, LoginResult, RegisterCommand, UserDto } from '@/api/types';
import { queryClient } from '@/lib/queryClient';

interface AuthContextType {
  user: CurrentUserResponse | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  roles: string[];
  hasRole: (role: string) => boolean;
  hasAnyRole: (roles: string[]) => boolean;
  login: (command: LoginCommand) => Promise<LoginResult>;
  register: (command: RegisterCommand) => Promise<UserDto>;
  logout: () => void;
  refreshUser: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<CurrentUserResponse | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);

  const fetchCurrentUser = useCallback(async (): Promise<CurrentUserResponse | null> => {
    const token = getAccessToken();
    if (!token) {
      setUser(null);
      setIsLoading(false);
      return null;
    }

    try {
      const me = await authApi.getMe();
      setUser(me);
      return me;
    } catch {
      // Token invalid or expired
      setAccessToken(null);
      setUser(null);
      return null;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const logout = useCallback(() => {
    setAccessToken(null);
    setUser(null);
    queryClient.clear();
  }, []);

  useEffect(() => {
    // Register 401 callback
    setUnauthorizedListener(() => {
      logout();
    });

    // Initial restore session
    fetchCurrentUser();
  }, [fetchCurrentUser, logout]);

  const login = async (command: LoginCommand): Promise<LoginResult> => {
    const result = await authApi.login(command);
    setAccessToken(result.accessToken);
    // After login, set user from LoginResult user / me
    setUser({
      id: result.user.id,
      email: result.user.email,
      roles: result.user.roles,
    });
    return result;
  };

  const register = async (command: RegisterCommand): Promise<UserDto> => {
    const result = await authApi.register(command);
    return result;
  };

  const hasRole = useCallback(
    (role: string): boolean => {
      return user?.roles?.includes(role) ?? false;
    },
    [user]
  );

  const hasAnyRole = useCallback(
    (rolesToCheck: string[]): boolean => {
      if (!user?.roles) return false;
      return rolesToCheck.some((r) => user.roles.includes(r));
    },
    [user]
  );

  const refreshUser = async () => {
    await fetchCurrentUser();
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        isAuthenticated: !!user && !!getAccessToken(),
        isLoading,
        roles: user?.roles ?? [],
        hasRole,
        hasAnyRole,
        login,
        register,
        logout,
        refreshUser,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export function useAuth(): AuthContextType {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
