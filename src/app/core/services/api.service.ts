// core/services/api.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, throwError, firstValueFrom } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private apiUrl = environment.apiUrl;
  private defaultTimeout = 30000; // 30 seconds

  constructor(private http: HttpClient) { }

  getBaseUrl(): string {
    return this.apiUrl;
  }

  private createHeaders(): HttpHeaders {
    const token = localStorage.getItem('auth_token');
    return new HttpHeaders({
      'Content-Type': 'application/json',
      ...(token ? { 'Authorization': `Bearer ${token}` } : {})
    });
  }

  private handleError(error: any): Observable<any> {
    console.error('API Error:', error);

    let errorMessage = 'حدث خطأ في الاتصال بالخادم';

    if (error.error instanceof ErrorEvent) {
      // Client-side error
      errorMessage = error.error.message;
    } else {
      // Server-side error
      switch (error.status) {
        case 400:
          errorMessage = error.error?.message || 'طلب غير صحيح';
          break;
        case 401:
          errorMessage = error.error?.message || 'غير مصرح به. الرجاء تسجيل الدخول مرة أخرى';
          break;
        case 403:
          errorMessage = error.error?.message || 'ليس لديك صلاحية للوصول إلى هذا المورد';
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

    return throwError(() => ({
      status: error.status,
      message: errorMessage,
      error: error.error
    }));
  }

  get<T>(url: string, params?: any, options: any = {}): Promise<T> {
    const httpOptions = {
      headers: this.createHeaders(),
      params: new HttpParams({ fromObject: params || {} })
    };

    return firstValueFrom(this.http.get<T>(`${this.apiUrl}${url}`, httpOptions)
      .pipe(
        catchError(err => this.handleError(err))
      )) as Promise<T>;
  }

  post<T>(url: string, body: any, options: any = {}): Promise<T> {
    const httpOptions = {
      headers: this.createHeaders()
    };

    return firstValueFrom(this.http.post<T>(`${this.apiUrl}${url}`, body, httpOptions)
      .pipe(
        catchError(err => this.handleError(err))
      )) as Promise<T>;
  }

  put<T>(url: string, body: any, options: any = {}): Promise<T> {
    const httpOptions = {
      headers: this.createHeaders()
    };

    return firstValueFrom(this.http.put<T>(`${this.apiUrl}${url}`, body, httpOptions)
      .pipe(
        catchError(err => this.handleError(err))
      )) as Promise<T>;
  }

  patch<T>(url: string, body: any, options: any = {}): Promise<T> {
    const httpOptions = {
      headers: this.createHeaders()
    };

    return firstValueFrom(this.http.patch<T>(`${this.apiUrl}${url}`, body, httpOptions)
      .pipe(
        catchError(err => this.handleError(err))
      )) as Promise<T>;
  }

  delete<T>(url: string, options: any = {}): Promise<T> {
    const httpOptions = {
      headers: this.createHeaders()
    };

    return firstValueFrom(this.http.delete<T>(`${this.apiUrl}${url}`, httpOptions)
      .pipe(
        catchError(err => this.handleError(err))
      )) as Promise<T>;
  }

  upload<T>(url: string, fileOrFormData: File | FormData, additionalData?: any): Promise<T> {
    let formData: FormData;

    if (fileOrFormData instanceof FormData) {
      formData = fileOrFormData;
      if (additionalData) {
        Object.keys(additionalData).forEach(key => {
          if (!formData.has(key)) {
            formData.append(key, additionalData[key]);
          }
        });
      }
    } else {
      formData = new FormData();
      formData.append('file', fileOrFormData);
      if (additionalData) {
        Object.keys(additionalData).forEach(key => {
          formData.append(key, additionalData[key]);
        });
      }
    }

    // When sending FormData, the browser automatically sets the correct Content-Type with boundary
    const token = localStorage.getItem('auth_token');
    const headers = new HttpHeaders({
      ...(token ? { 'Authorization': `Bearer ${token}` } : {})
    });

    return firstValueFrom(this.http.post<T>(`${this.apiUrl}${url}`, formData, { headers })
      .pipe(
        catchError(err => this.handleError(err))
      )) as Promise<T>;
  }

  postMultipart<T>(url: string, formData: FormData): Promise<T> {
    return this.upload<T>(url, formData);
  }

  download(url: string, params?: any): Promise<Blob> {
    const httpOptions = {
      headers: this.createHeaders(),
      params: new HttpParams({ fromObject: params || {} }),
      responseType: 'blob' as 'json'
    };

    return firstValueFrom(this.http.get<Blob>(`${this.apiUrl}${url}`, httpOptions)
      .pipe(
        catchError(err => this.handleError(err))
      )) as Promise<Blob>;
  }
}
