import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, NavigationEnd } from '@angular/router';
import { Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';
import { SidebarComponent } from './shared/components/sidebar/sidebar.component';
import { HeaderComponent } from './shared/components/header/header.component';
import { BottomNavComponent } from './shared/components/bottom-nav/bottom-nav.component';
import { AuthService } from './core/services/auth.service';
import { SignalRService } from './core/services/signalr.service';

@Component({
    selector: 'app-root',
    standalone: true,
    imports: [CommonModule, RouterModule, SidebarComponent, HeaderComponent, BottomNavComponent],
    templateUrl: './app.component.html',
    styleUrls: ['./app.component.css']
})
export class AppComponent implements OnInit, OnDestroy {
    title = 'School Management System';
    isAuthenticated = false;
    private authSub?: Subscription;

    constructor(
        private authService: AuthService,
        private signalR: SignalRService,
        private router: Router
    ) { }

    ngOnInit() {
        this.authSub = this.authService.currentUser$.subscribe(user => {
            this.isAuthenticated = !!user;

            if (user) {
                this.signalR.startConnection().catch(() => { });
                return;
            }

            this.signalR.stopConnection().catch(() => { });
        });

        // Toggle admin theme globally based on route
        this.router.events.pipe(
            filter(event => event instanceof NavigationEnd)
        ).subscribe((event: any) => {
            if (event.urlAfterRedirects?.includes('/admin')) {
                document.body.classList.add('admin-theme');
            } else {
                document.body.classList.remove('admin-theme');
            }
        });
    }

    ngOnDestroy(): void {
        this.authSub?.unsubscribe();
    }
}
