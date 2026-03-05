import { TestBed } from '@angular/core/testing';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { AppComponent } from './app.component';

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
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
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const uploadButton = compiled.querySelector('[data-testid="company-upload-button"]');

    expect(uploadButton?.hasAttribute('disabled')).toBeTrue();
  });

  it('should add a skill manually', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;

    app.newSkillInput = 'Go';
    app.addSkill();

    expect(app.editableProfile.skills).toContain('Go');
  });

  it('should add and remove manual experience entries', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;

    app.addExperience();
    expect(app.editableProfile.experiences.length).toBe(1);

    app.removeExperience(0);
    expect(app.editableProfile.experiences.length).toBe(0);
  });
});
