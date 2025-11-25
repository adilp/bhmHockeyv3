import { create } from 'zustand';
import { authService, authStorage } from '@bhmhockey/api-client';
import type { User, LoginRequest, RegisterRequest } from '@bhmhockey/shared';

interface AuthState {
  user: User | null;
  isLoading: boolean;
  isAuthenticated: boolean;

  // Actions
  login: (credentials: LoginRequest) => Promise<void>;
  register: (data: RegisterRequest) => Promise<void>;
  logout: () => Promise<void>;
  checkAuth: () => Promise<void>;
  setUser: (user: User) => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  isLoading: true,
  isAuthenticated: false,

  login: async (credentials: LoginRequest) => {
    const response = await authService.login(credentials);
    set({
      user: response.user,
      isAuthenticated: true
    });
  },

  register: async (data: RegisterRequest) => {
    const response = await authService.register(data);
    set({
      user: response.user,
      isAuthenticated: true
    });
  },

  logout: async () => {
    await authService.logout();
    set({
      user: null,
      isAuthenticated: false
    });
  },

  checkAuth: async () => {
    try {
      set({ isLoading: true });
      const token = await authStorage.getToken();

      if (!token) {
        set({ isAuthenticated: false, user: null, isLoading: false });
        return;
      }

      // Verify token is valid by fetching current user
      const user = await authService.getCurrentUser();
      set({
        user,
        isAuthenticated: true,
        isLoading: false
      });
    } catch (error) {
      // Token is invalid, clear it
      await authStorage.removeToken();
      set({
        user: null,
        isAuthenticated: false,
        isLoading: false
      });
    }
  },

  setUser: (user: User) => {
    set({ user });
  },
}));
