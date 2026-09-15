import type {
  LoginRequest,
  RegisterRequest,
  AuthResponse,
  User,
  ChangePasswordRequest,
  ForgotPasswordRequest,
  ForgotPasswordResponse,
  PasswordResetRequest,
  ConfirmPasswordResetRequest,
  PasswordResetMessageResponse,
} from '@bhmhockey/shared';
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

  /**
   * Change password for the current user
   */
  async changePassword(data: ChangePasswordRequest): Promise<void> {
    await apiClient.instance.post('/auth/change-password', data);
  },

  /**
   * Request password reset (notifies admin)
   */
  async forgotPassword(data: ForgotPasswordRequest): Promise<ForgotPasswordResponse> {
    const response = await apiClient.instance.post<ForgotPasswordResponse>(
      '/auth/forgot-password',
      data
    );
    return response.data;
  },

  /**
   * Email a reset link and 6-digit code. Resolves with the same message whether
   * or not the address has an account.
   */
  async requestPasswordReset(data: PasswordResetRequest): Promise<PasswordResetMessageResponse> {
    const response = await apiClient.instance.post<PasswordResetMessageResponse>(
      '/auth/password-reset/request',
      data
    );
    return response.data;
  },

  /**
   * Set a new password with the link token, or the email plus code.
   * Rejects with the server's message when the token or code is invalid or expired.
   */
  async confirmPasswordReset(data: ConfirmPasswordResetRequest): Promise<PasswordResetMessageResponse> {
    const response = await apiClient.instance.post<PasswordResetMessageResponse>(
      '/auth/password-reset/confirm',
      data
    );
    return response.data;
  },
};
