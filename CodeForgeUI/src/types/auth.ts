import { Project } from './index';

export interface User {
    id: string;
    email: string;
    firstName?: string;
    lastName?: string;
    avatarUrl?: string;
    role: string;
    isDarkMode?: boolean;
    projects?: Project[];
    createdAt?: string;
}

export interface RegisterRequest {
    email: string;
    password: string;
    firstName?: string;
    lastName?: string;
    code: string;
}

export interface LoginRequest {
    email: string;
    password: string;
}

export interface ForgotPasswordRequest {
    email: string;
}

export interface ResetPasswordRequest {
    email: string;
    code: string;
    newPassword: string;
}

export interface AuthResponse {
    id: string;
    email: string;
    firstName?: string;
    lastName?: string;
    avatarUrl?: string;
    role: string;
}

export interface UpdateUserRequest {
    firstName?: string;
    lastName?: string;
    avatarUrl?: string;
    isDarkMode?: boolean;
}
