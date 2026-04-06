// core/guards/role.guard.ts
import { Injectable } from '@angular/core';
import { Router, CanActivate, ActivatedRouteSnapshot } from '@angular/router';
import { AuthService } from '../services/auth.service';

@Injectable({
  providedIn: 'root'
})
export class RoleGuard implements CanActivate {
  constructor(
    private auth: AuthService,
    private router: Router
  ) {}

  canActivate(route: ActivatedRouteSnapshot): boolean {
    const requiredRoles = route.data['roles'] as string | string[];
    
    if (!requiredRoles) {
      return true;
    }

    if (this.auth.hasRole(requiredRoles)) {
      return true;
    }

    this.router.navigate(['/unauthorized']);
    return false;
  }
}