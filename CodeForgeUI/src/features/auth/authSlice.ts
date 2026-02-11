import { createSlice, createAsyncThunk, PayloadAction } from '@reduxjs/toolkit';
import api from '../../utils/api';
import { LoginRequest, RegisterRequest, ResetPasswordRequest, User } from '../../types/auth';

interface AuthState {
    isAuthenticated: boolean;
    user: User | null;
    token: string | null; // Added token to state interface as it was missing but used in Profile
    loading: boolean;
    error: string | null;
}

const initialState: AuthState = {
    isAuthenticated: false,
    user: null,
    token: null,
    loading: false,
    error: null,
};

export const sendVerificationCode = createAsyncThunk<void, string, { rejectValue: string }>(
    'auth/sendCode',
    async (email, { rejectWithValue }) => {
        try {
            await api.post('/auth/send-code', { email });
        } catch (error: any) {
            return rejectWithValue(error.response?.data?.message || 'Failed to send verification code');
        }
    }
);

export const forgotPassword = createAsyncThunk<void, string, { rejectValue: string }>(
    'auth/forgotPassword',
    async (email, { rejectWithValue }) => {
        try {
            await api.post('/auth/forgot-password', { email });
        } catch (error: any) {
            return rejectWithValue(error.response?.data?.message || 'Failed to send password reset code');
        }
    }
);

export const resetPassword = createAsyncThunk<void, ResetPasswordRequest, { rejectValue: string }>(
    'auth/resetPassword',
    async (data, { rejectWithValue }) => {
        try {
            await api.post('/auth/reset-password', data);
        } catch (error: any) {
            return rejectWithValue(error.response?.data?.message || 'Failed to reset password');
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
            return rejectWithValue(error.response?.data?.message || 'Registration failed');
        }
    }
);

export const login = createAsyncThunk<User, LoginRequest, { rejectValue: string }>(
    'auth/login',
    async (data: LoginRequest, { rejectWithValue }) => {
        try {
            const response = await api.post<User>('/auth/login', data);
            return response.data;
        } catch (error: any) {
            return rejectWithValue(error.response?.data?.message || 'Login failed');
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
            return rejectWithValue('Not authenticated');
        }
    });

export const verifyEmail = createAsyncThunk<void, { email: string; code: string }, { rejectValue: string }>(
    'auth/verify',
    async (data, { rejectWithValue }) => {
        try {
            await api.post('/auth/verify', data);
        } catch (error: any) {
            return rejectWithValue(error.response?.data?.message || 'Verification failed');
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
            return rejectWithValue(error.response?.data?.message || 'Update failed');
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
        }
    },
    extraReducers: (builder) => {
        builder
            // Register
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
            // Login
            .addCase(login.pending, (state) => {
                state.loading = true;
                state.error = null;
            })
            .addCase(login.fulfilled, (state, action: PayloadAction<User>) => {
                state.loading = false;
                state.isAuthenticated = true;
                state.user = action.payload;
            })
            .addCase(login.rejected, (state, action) => {
                state.loading = false;
                state.error = action.payload as string;
            })
            // Logout
            .addCase(logout.fulfilled, (state) => {
                state.isAuthenticated = false;
                state.user = null;
                state.token = null;
            })
            // Check auth
            .addCase(checkAuth.fulfilled, (state, action: PayloadAction<User>) => {
                state.isAuthenticated = true;
                state.user = action.payload;
            })
            .addCase(checkAuth.rejected, (state) => {
                state.isAuthenticated = false;
                state.user = null;
            })
            // Update User
            .addCase(updateUser.fulfilled, (state, action: PayloadAction<User>) => {
                state.user = action.payload;
            });
    },
});

export const { clearError, setUser } = authSlice.actions;
export default authSlice.reducer;
