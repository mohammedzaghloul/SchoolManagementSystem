// core/guards/no-auth.guard.ts
import { Injectable } from '@angular/core';
import { Router, CanActivate } from '@angular/router';
import { AuthService } from '../services/auth.service';

@Injectable({
  providedIn: 'root'
})
export class NoAuthGuard implements CanActivate {
  constructor(
    private auth: AuthService,
    private router: Router
  ) {}

  canActivate(): boolean {
    if (!this.auth.isAuthenticated()) {
      return true;
    }

    const user = this.auth.getCurrentUser();
    
    if (user) {
      switch (user.role) {
        case 'Admin':
          this.router.navigate(['/admin/dashboard']);
          break;
        case 'Teacher':
          this.router.navigate(['/teacher/dashboard']);
          break;
        case 'Student':
          this.router.navigate(['/student/dashboard']);
          break;
        case 'Parent':
          this.router.navigate(['/parent/dashboard']);
          break;
      }
    } else {
      this.router.navigate(['/']);
    }
    
    return false;
  }
}