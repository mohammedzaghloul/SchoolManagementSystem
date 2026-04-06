import { User } from './user.model';

export interface LoginDto {
    email: string;
    password?: string;
}

export interface RegisterDto {
    fullName: string;
    email: string;
    password?: string;
    role: string;
}

export interface AuthResponse {
    token: string;
    refreshToken: string;
    user: User;
}

export { User };
