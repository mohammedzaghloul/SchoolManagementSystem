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
            case 500:
              errorMessage = 'خطأ داخلي في الخادم';
              break;
            default:
              errorMessage = error.error?.message || errorMessage;
          }
        }

        this.toastr.error(errorMessage, 'خطأ');
        return throwError(() => error);
      })
    );
  }
}