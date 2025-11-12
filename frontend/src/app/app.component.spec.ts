import { TestBed } from '@angular/core/testing';
import { AppComponent } from './app.component';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideAnimations()
      ]
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should have title', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app.title).toBe('infrastructure-as-code');
  });

  it('should display app title in toolbar', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const titleElement = fixture.nativeElement.querySelector('.app-title');
    expect(titleElement).toBeTruthy();
    expect(titleElement.textContent).toContain('Infrastructure as Code');
  });

  it('should display profile card in toolbar', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const profileCard = fixture.nativeElement.querySelector('.profile-card');
    expect(profileCard).toBeTruthy();
  });

  it('should have contact information in profile card', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const profileText = fixture.nativeElement.querySelector('.profile-text');
    expect(profileText).toBeTruthy();
    expect(profileText.textContent).toContain('Mario Muja');
    expect(profileText.textContent).toContain('Hamburg');
  });

  it('should have phone and email links', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const phoneLink = fixture.nativeElement.querySelector('a[href^="tel:"]');
    const emailLink = fixture.nativeElement.querySelector('a[href^="mailto:"]');
    expect(phoneLink).toBeTruthy();
    expect(emailLink).toBeTruthy();
  });

  it('should have GitHub link', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const githubLink = fixture.nativeElement.querySelector('a[href*="github.com"]');
    expect(githubLink).toBeTruthy();
    expect(githubLink.getAttribute('target')).toBe('_blank');
  });
});


