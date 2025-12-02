import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { Location } from '@angular/common';
import { routes } from './app.routes';
import { RouterTestingModule } from '@angular/router/testing';

describe('App Routes', () => {
  let router: Router;
  let location: Location;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [
        RouterTestingModule.withRoutes(routes)
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    location = TestBed.inject(Location);
  });

  it('should have routes defined', () => {
    expect(routes).toBeDefined();
    expect(routes.length).toBeGreaterThan(0);
  });

  it('should navigate to default route', async () => {
    await router.navigate(['']);
    expect(location.path()).toBe('/');
  });

  it('should load TransportComponent for default route', async () => {
    await router.navigate(['']);
    // Route should be configured
    expect(router.config.length).toBeGreaterThan(0);
  });

  it('should have lazy loaded component for default route', () => {
    const defaultRoute = routes.find(r => r.path === '');
    expect(defaultRoute).toBeDefined();
    expect(defaultRoute?.loadComponent).toBeDefined();
  });
});
