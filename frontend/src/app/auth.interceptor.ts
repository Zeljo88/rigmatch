import { HttpInterceptorFn } from '@angular/common/http';
import { API_BASE_URL, AUTH_TOKEN_STORAGE_KEY } from './app.constants';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const token = localStorage.getItem(AUTH_TOKEN_STORAGE_KEY);
  if (!token || !request.url.startsWith(API_BASE_URL)) {
    return next(request);
  }

  return next(request.clone({
    setHeaders: {
      Authorization: `Bearer ${token}`
    }
  }));
};
