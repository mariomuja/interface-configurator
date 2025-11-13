import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./components/transport/transport.component').then(m => m.TransportComponent)
  }
];



