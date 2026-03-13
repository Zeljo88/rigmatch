import { TestBed } from '@angular/core/testing';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { AppComponent } from './app.component';

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent, NoopAnimationsModule],
      providers: [provideHttpClientTesting()]
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should keep company upload button disabled when no file is selected', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    app.authResolved = true;
    app.authSession = {
      token: 'token',
      expiresAtUtc: '',
      userId: 'user-id',
      fullName: 'Demo User',
      email: 'demo@example.com',
      companyId: 'company-id',
      companyName: 'Demo Company'
    };
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const uploadButton = compiled.querySelector('[data-testid="company-upload-button"]');

    expect(uploadButton?.hasAttribute('disabled')).toBeTrue();
  });

  it('should switch to settings page', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    app.authResolved = true;
    app.authSession = {
      token: 'token',
      expiresAtUtc: '',
      userId: 'user-id',
      fullName: 'Demo User',
      email: 'demo@example.com',
      companyId: 'company-id',
      companyName: 'Demo Company'
    };

    app.setPage('settings');

    expect(app.activePage).toBe('settings');
  });

  it('should clear filters', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    spyOn(app, 'loadLibrary').and.stub();

    app.searchQuery = 'Operator';
    app.minExpFilter = 5;
    app.educationFilter = 'Bachelor';
    app.certFilter = 'IWCF';

    app.clearFilters();

    expect(app.searchQuery).toBe('');
    expect(app.minExpFilter).toBeNull();
    expect(app.educationFilter).toBe('');
    expect(app.certFilter).toBe('');
  });
});
