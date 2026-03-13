import { environment } from '../environments/environment';

export const API_BASE_URL = environment.apiBaseUrl.replace(/\/$/, '');
export const AUTH_TOKEN_STORAGE_KEY = 'rigmatch.auth.token';
