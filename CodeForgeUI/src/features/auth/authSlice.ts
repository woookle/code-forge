import { createSlice, createAsyncThunk, PayloadAction } from '@reduxjs/toolkit';
import api from '../../utils/api';
import { LoginRequest, RegisterRequest, ResetPasswordRequest, User, Enable2FAResponse, LoginWith2FARequest } from '../../types/auth';

interface AuthState {
    isAuthenticated: boolean;
    user: User | null;
    token: string | null;
    loading: boolean;
    error: string | null;
    requiresTwoFactor: boolean;
    pendingTwoFactorEmail: string | null;
    pendingTwoFactorPassword: string | null;
}

const initialState: AuthState = {
    isAuthenticated: false,
    user: null,
    token: null,
    loading: false,
    error: null,
    requiresTwoFactor: false,
    pendingTwoFactorEmail: null,
    pendingTwoFactorPassword: null,
};

export const sendVerificationCode = createAsyncThunk<void, string, { rejectValue: string }>(
    'auth/sendCode',
    async (email, { rejectWithValue }) => {
        try {
            await api.post('/auth/send-code', { email });
        } catch (error: any) {
            return rejectWithValue(error.response?.data?.message || 'Не удалось отправить код подтверждения');
        }
    }
);

export const forgotPassword = createAsyncThunk<void, string, { rejectValue: string }>(
    'auth/forgotPassword',
    async (email, { rejectWithValue }) => {
        try {
            await api.post('/auth/forgot-password', { email });
        } catch (error: any) {
            return rejectWithValue(error.response?.data?.message || 'Не удалось отправить код сброса пароля');
        }
    }
);

export const resetPassword = createAsyncThunk<void, ResetPasswordRequest, { rejectValue: string }>(
    'auth/resetPassword',
    async (data, { rejectWithValue }) => {
        try {
            await api.post('/auth/reset-password', data);
        } catch (error: any) {
            return rejectWithValue(error.response?.data?.message || 'Не удалось сбросить пароль');
        }
    }
);

export const register = createAsyncThunk<User, RegisterRequest, { rejectValue: string }>(
    'auth/register',
    async (data: RegisterRequest, { rejectWithValue }) => {
        try {
            const response = await api.post<User>('/auth/register', data);
            return response.data;
        } catch (error: any) {
            return rejectWithValue(error.response?.data?.message || 'Ошибка регистрации');
        }
    }
);

export const login = createAsyncThunk<
    { user?: User; requiresTwoFactor?: boolean; email?: string; password?: string },
    LoginRequest,
    { rejectValue: string }
>(
    'auth/login',
    async (data: LoginRequest, { rejectWithValue }) => {
        try {
            const response = await api.post('/auth/login', data);
            if (response.data.requiresTwoFactor) {
                // Пароль передаётся в Redux чтобы TotpModal мог его использовать на шаге 2FA
                return { requiresTwoFactor: true, email: response.data.email, password: data.password };
            }
            return { user: response.data as User };
        } catch (error: any) {
            return rejectWithValue(error.response?.data?.message || 'Ошибка входа');
        }
    }
);

export const loginWith2FA = createAsyncThunk<User, LoginWith2FARequest, { rejectValue: string }>(
    'auth/loginWith2FA',
    async (data, { rejectWithValue }) => {
        try {
            const response = await api.post<User>('/auth/login-2fa', data);
            return response.data;
        } catch (error: any) {
            return rejectWithValue(error.response?.data?.message || 'Неверный TOTP-код');
        }
    }
);

export const logout = createAsyncThunk<void, void, { rejectValue: string }>(
    'auth/logout',
    async () => {
        await api.post('/auth/logout');
    }
);

export const checkAuth = createAsyncThunk<User, void, { rejectValue: string }>(
    'auth/check',
    async (_, { rejectWithValue }) => {
        try {
            const response = await api.get<User>('/auth/me');
            return response.data;
        } catch (error: any) {
            return rejectWithValue('Не аутентифицирован');
        }
    });

export const verifyEmail = createAsyncThunk<void, { email: string; code: string }, { rejectValue: string }>(
    'auth/verify',
    async (data, { rejectWithValue }) => {
        try {
            await api.post('/auth/verify', data);
        } catch (error: any) {
            return rejectWithValue(error.response?.data?.message || 'Ошибка подтверждения');
        }
    }
);

export const updateUser = createAsyncThunk<User, { id: string; data: Partial<User> }, { rejectValue: string }>(
    'auth/update',
    async ({ id, data }, { rejectWithValue }) => {
        try {
            await api.put(`/users/${id}`, data);
            const response = await api.get<User>('/auth/me');
            return response.data;
        } catch (error: any) {
            return rejectWithValue(error.response?.data?.message || 'Ошибка обновления');
        }
    }
);

export const setup2FA = createAsyncThunk<Enable2FAResponse, void, { rejectValue: string }>(
    'auth/setup2FA',
    async (_, { rejectWithValue }) => {
        try {
            const response = await api.post<Enable2FAResponse>('/auth/2fa/setup');
            return response.data;
        } catch (error: any) {
            return rejectWithValue(error.response?.data?.message || 'Не удалось настроить 2FA');
        }
    }
);

export const enable2FA = createAsyncThunk<void, string, { rejectValue: string }>(
    'auth/enable2FA',
    async (code, { rejectWithValue }) => {
        try {
            await api.post('/auth/2fa/enable', { code });
        } catch (error: any) {
            return rejectWithValue(error.response?.data?.message || 'Неверный TOTP-код');
        }
    }
);

export const disable2FA = createAsyncThunk<void, string, { rejectValue: string }>(
    'auth/disable2FA',
    async (code, { rejectWithValue }) => {
        try {
            await api.post('/auth/2fa/disable', { code });
        } catch (error: any) {
            return rejectWithValue(error.response?.data?.message || 'Неверный код 2FA');
        }
    }
);

const authSlice = createSlice({
    name: 'auth',
    initialState,
    reducers: {
        clearError: (state) => {
            state.error = null;
        },
        setUser: (state, action: PayloadAction<User>) => {
            state.user = action.payload;
        },
        clearTwoFactorState: (state) => {
            state.requiresTwoFactor = false;
            state.pendingTwoFactorEmail = null;
            state.pendingTwoFactorPassword = null;
        }
    },
    extraReducers: (builder) => {
        builder
            // Регистрация
            .addCase(register.pending, (state) => {
                state.loading = true;
                state.error = null;
            })
            .addCase(register.fulfilled, (state, action: PayloadAction<User>) => {
                state.loading = false;
                state.isAuthenticated = true;
                state.user = action.payload;
            })
            .addCase(register.rejected, (state, action) => {
                state.loading = false;
                state.error = action.payload as string;
            })
            // Вход
            .addCase(login.pending, (state) => {
                state.loading = true;
                state.error = null;
            })
            .addCase(login.fulfilled, (state, action) => {
                state.loading = false;
                if (action.payload.requiresTwoFactor) {
                    state.requiresTwoFactor = true;
                    state.pendingTwoFactorEmail = action.payload.email || null;
                    // Сохраняем пароль в Redux чтобы TotpModal мог его использовать
                    state.pendingTwoFactorPassword = action.payload.password || null;
                } else if (action.payload.user) {
                    state.isAuthenticated = true;
                    state.user = action.payload.user;
                }
            })
            .addCase(login.rejected, (state, action) => {
                state.loading = false;
                state.error = action.payload as string;
            })
            // Вход с 2FA
            .addCase(loginWith2FA.pending, (state) => {
                state.loading = true;
                state.error = null;
            })
            .addCase(loginWith2FA.fulfilled, (state, action: PayloadAction<User>) => {
                state.loading = false;
                state.isAuthenticated = true;
                state.user = action.payload;
                state.requiresTwoFactor = false;
                state.pendingTwoFactorEmail = null;
                state.pendingTwoFactorPassword = null;
            })
            .addCase(loginWith2FA.rejected, (state, action) => {
                state.loading = false;
                state.error = action.payload as string;
            })
            // Выход
            .addCase(logout.fulfilled, (state) => {
                state.isAuthenticated = false;
                state.user = null;
                state.token = null;
                state.requiresTwoFactor = false;
                state.pendingTwoFactorEmail = null;
                state.pendingTwoFactorPassword = null;
            })
            // Проверка аутентификации
            .addCase(checkAuth.fulfilled, (state, action: PayloadAction<User>) => {
                state.isAuthenticated = true;
                state.user = action.payload;
            })
            .addCase(checkAuth.rejected, (state) => {
                state.isAuthenticated = false;
                state.user = null;
            })
            // Обновление пользователя
            .addCase(updateUser.fulfilled, (state, action: PayloadAction<User>) => {
                state.user = action.payload;
            })
            // Включение 2FA
            .addCase(enable2FA.fulfilled, (state) => {
                if (state.user) {
                    state.user = { ...state.user, twoFactorEnabled: true };
                }
            })
            // Отключение 2FA
            .addCase(disable2FA.fulfilled, (state) => {
                if (state.user) {
                    state.user = { ...state.user, twoFactorEnabled: false };
                }
            });
    },
});

export const { clearError, setUser, clearTwoFactorState } = authSlice.actions;
export default authSlice.reducer;
