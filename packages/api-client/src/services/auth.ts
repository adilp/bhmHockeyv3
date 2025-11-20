import type { LoginRequest, RegisterRequest, AuthResponse, User } from '@bhmhockey/shared';
import { apiClient } from '../client';
import { authStorage } from '../storage/auth';

/**
 * Authentication service
 */
export const authService = {
  /**
   * Register a new user
   */
  async register(data: RegisterRequest): Promise<AuthResponse> {
    const response = await apiClient.instance.post<AuthResponse>('/auth/register', data);
    await authStorage.setToken(response.data.token);
    await authStorage.setRefreshToken(response.data.refreshToken);
    return response.data;
  },

  /**
   * Login user
   */
  async login(data: LoginRequest): Promise<AuthResponse> {
    const response = await apiClient.instance.post<AuthResponse>('/auth/login', data);
    await authStorage.setToken(response.data.token);
    await authStorage.setRefreshToken(response.data.refreshToken);
    return response.data;
  },

  /**
   * Logout user
   */
  async logout(): Promise<void> {
    try {
      await apiClient.instance.post('/auth/logout');
    } catch (error) {
      // Ignore errors on logout
      console.error('Logout error:', error);
    } finally {
      await authStorage.removeToken();
    }
  },

  /**
   * Get current user profile
   */
  async getCurrentUser(): Promise<User> {
    const response = await apiClient.instance.get<User>('/users/me');
    return response.data;
  },

  /**
   * Update push notification token
   */
  async updatePushToken(token: string): Promise<void> {
    await apiClient.instance.put('/users/me/push-token', { pushToken: token });
  },

  /**
   * Check if user is authenticated
   */
  async isAuthenticated(): Promise<boolean> {
    const token = await authStorage.getToken();
    return !!token;
  },
};
