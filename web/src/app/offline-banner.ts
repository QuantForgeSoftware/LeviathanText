import { Component } from '@angular/core';
import { offlineMode } from './offline-cache';

@Component({
  selector: 'app-offline-banner',
  standalone: true,
  template: `
    @if (offline()) {
      <p class="offline" role="status">
        Offline: showing cached data. Edits are kept as drafts until the server is reachable.
      </p>
    }
  `,
  styles: [`
    .offline { background: #fff4d6; border: 1px solid #e0c36a; padding: 0.4rem 0.6rem; }
  `],
})
export class OfflineBanner {
  protected readonly offline = offlineMode;
}
