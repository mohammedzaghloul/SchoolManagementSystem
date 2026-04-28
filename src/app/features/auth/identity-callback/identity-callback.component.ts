import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { AuthService, CentralIdentityConnectCompleteResponse } from '../../../core/services/auth.service';

@Component({
  selector: 'app-identity-callback',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './identity-callback.component.html',
  styleUrl: './identity-callback.component.css'
})
export class IdentityCallbackComponent implements OnInit {
  loading = true;
  error = '';
  result: CentralIdentityConnectCompleteResponse | null = null;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly authService: AuthService
  ) {}

  async ngOnInit(): Promise<void> {
    const query = this.route.snapshot.queryParamMap;

    try {
      this.result = await this.authService.completeCentralIdentityLink(
        query.get('code'),
        query.get('state'),
        query.get('error'));
    } catch (error: any) {
      this.error = error?.message || 'Could not complete account linking.';
    } finally {
      this.loading = false;
    }
  }
}
