// core/interceptors/error.interceptor.ts
import { Injectable } from '@angular/core';
import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { ToastrService } from 'ngx-toastr';

@Injectable()
export class ErrorInterceptor implements HttpInterceptor {
  constructor(
    private auth: AuthService,
    private router: Router,
    private toastr: ToastrService
  ) { }

  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    return next.handle(req).pipe(
      catchError((error: HttpErrorResponse) => {
        let errorMessage = 'حدث خطأ غير متوقع';

        if (error.error instanceof ErrorEvent) {
          // Client-side error
          errorMessage = error.error.message;
        } else {
          // Server-side error
          switch (error.status) {
            case 401:
              errorMessage = `انتهت الجلسة. 401 في مسار: ${req.url}`;
              this.auth.logout();
              this.router.navigate(['/auth/login']);
              break;
            case 403:
              errorMessage = 'ليس لديك صلاحية للوصول إلى هذا المورد';
              break;
            case 404:
              errorMessage = 'المورد المطلوب غير موجود';
              break;
            case 409:
              errorMessage = error.error?.message || 'لا يمكن تنفيذ العملية بسبب وجود بيانات مرتبطة';
              break;
            case 500:
              errorMessage = 'خطأ داخلي في الخادم';
              break;
            default:
              errorMessage = error.error?.message || errorMessage;
          }
        }

        if (!this.shouldSuppressToast(req)) {
          this.toastr.error(errorMessage, 'خطأ');
        }
        return throwError(() => error);
      })
    );
  }

  private shouldSuppressToast(req: HttpRequest<any>): boolean {
    return req.url.includes('/api/auth/login') ||
      req.url.includes('/api/Account/login') ||
      req.url.includes('/api/auth/verify-login-otp') ||
      req.url.includes('/api/auth/request-login-otp');
  }
}
