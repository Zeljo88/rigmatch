import { CommonModule } from '@angular/common';
import { Component, Inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { EditableCandidateProfile, ExperienceEntry, RoleExperienceEntry } from './cv-types';

export type CvEditDialogData = {
  title: string;
  fileUrl: string;
  profile: EditableCandidateProfile;
  standardRoles: string[];
};

@Component({
  selector: 'app-cv-edit-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule
  ],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>

    <mat-dialog-content class="dialog-content">
      <p class="file-path">File: {{ data.fileUrl }}</p>

      <div class="grid">
        <mat-form-field appearance="outline">
          <mat-label>Full Name</mat-label>
          <input matInput name="name" [(ngModel)]="workingProfile.name" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Email</mat-label>
          <input matInput name="email" [(ngModel)]="workingProfile.email" />
        </mat-form-field>
      </div>

      <div class="grid">
        <mat-form-field appearance="outline">
          <mat-label>Phone</mat-label>
          <input matInput name="phoneNumber" [(ngModel)]="workingProfile.phoneNumber" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Location</mat-label>
          <input matInput name="location" [(ngModel)]="workingProfile.location" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Experience Years</mat-label>
          <input matInput type="number" min="0" name="experienceYears" [(ngModel)]="workingProfile.experienceYears" />
        </mat-form-field>
      </div>

      <div class="grid">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Highest Education</mat-label>
          <input matInput name="highestEducation" [(ngModel)]="workingProfile.highestEducation" />
        </mat-form-field>
      </div>

      <div class="grid">
        <mat-form-field appearance="outline">
          <mat-label>Skills (comma-separated)</mat-label>
          <textarea matInput rows="2" name="skills" [(ngModel)]="skillsCsv"></textarea>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Certifications (comma-separated)</mat-label>
          <textarea matInput rows="2" name="certifications" [(ngModel)]="certificationsCsv"></textarea>
        </mat-form-field>
      </div>

      <div class="role-exp-block" *ngIf="roleExperiencePreview.length > 0">
        <h3>Role Experience Breakdown</h3>
        <div class="role-exp-list">
          <div class="role-exp-item" *ngFor="let item of roleExperiencePreview">
            <div class="role-exp-meta">
              <span class="role-exp-title">{{ item.jobTitle }}</span>
              <span class="role-exp-company">{{ getCompaniesForRole(item.jobTitle) }}</span>
            </div>
            <strong>{{ item.years | number:'1.0-1' }} years</strong>
          </div>
        </div>
      </div>

      <div class="review-summary" *ngIf="pendingRoleReviewCount > 0">
        <strong>{{ pendingRoleReviewCount }}</strong> role {{ pendingRoleReviewCount === 1 ? 'mapping needs' : 'mappings need' }} review.
        Auto-matched roles stay mapped unless you explicitly change them.
      </div>

      <div class="experience-header">
        <h3>Needs Review</h3>
        <button mat-stroked-button type="button" (click)="addExperience()">Add Experience</button>
      </div>

      <div class="empty-state" *ngIf="reviewExperiences.length === 0">
        No experience mappings currently need review.
      </div>

      <div class="experience-card" *ngFor="let exp of reviewExperiences; trackBy: trackByExperienceIndex">
        <ng-container *ngTemplateOutlet="experienceCard; context: { $implicit: exp.item, index: exp.index }"></ng-container>
      </div>

      <div class="auto-matched-section" *ngIf="autoMatchedExperiences.length > 0">
        <div class="experience-header compact">
          <h3>Auto Matched</h3>
          <button mat-button type="button" (click)="showAutoMatched = !showAutoMatched">
            {{ showAutoMatched ? 'Hide' : 'Show' }} {{ autoMatchedExperiences.length }}
          </button>
        </div>

        <div class="auto-matched-note">
          These roles were mapped above the confidence threshold and do not require action unless you want to override them.
        </div>

        <div *ngIf="showAutoMatched">
          <div class="experience-card" *ngFor="let exp of autoMatchedExperiences; trackBy: trackByExperienceIndex">
            <ng-container *ngTemplateOutlet="experienceCard; context: { $implicit: exp.item, index: exp.index }"></ng-container>
          </div>
        </div>
      </div>

      <ng-template #experienceCard let-exp let-i="index">
        <div class="experience-card-head">
          <div class="experience-title-row">
            <strong>Experience {{ i + 1 }}</strong>
            <div class="experience-badges">
              <span class="status-badge status-unmapped" *ngIf="isUnmapped(exp)">Unmapped</span>
              <span class="status-badge status-review" *ngIf="!isUnmapped(exp) && requiresReview(exp)">Needs Review</span>
              <span class="status-badge status-reviewed" *ngIf="exp.reviewedByUser">Reviewed</span>
              <span class="status-badge status-matched" *ngIf="!isUnmapped(exp) && !requiresReview(exp) && !exp.reviewedByUser">
                Auto Matched
                <span class="status-confidence" *ngIf="exp.matchConfidence !== null && exp.matchConfidence !== undefined">
                  {{ exp.matchConfidence | number:'1.2-2' }}
                </span>
              </span>
            </div>
          </div>
          <button mat-button color="warn" type="button" (click)="removeExperience(i)">Remove</button>
        </div>

        <div class="grid">
          <mat-form-field appearance="outline">
            <mat-label>Company</mat-label>
            <input matInput [name]="'company' + i" [(ngModel)]="exp.companyName" />
          </mat-form-field>

          <ng-container *ngIf="showRoleEditor(exp); else autoMappedRole">
            <mat-form-field appearance="outline">
              <mat-label>Mapped Standard Role</mat-label>
              <mat-select [name]="'standardRoleName' + i" [(ngModel)]="exp.standardRoleName">
                <mat-option [value]="''">No standard match</mat-option>
                <mat-option *ngFor="let roleOption of getRoleOptions(exp.standardRoleName)" [value]="roleOption">
                  {{ roleOption }}
                </mat-option>
              </mat-select>
              <mat-hint *ngIf="!exp.standardRoleName && exp.rawRoleTitle">No catalog match yet. Choose one if needed.</mat-hint>
            </mat-form-field>
          </ng-container>
          <ng-template #autoMappedRole>
            <mat-form-field appearance="outline">
              <mat-label>Mapped Standard Role</mat-label>
              <input matInput [value]="exp.standardRoleName" readonly />
              <mat-hint>Auto-matched above threshold.</mat-hint>
            </mat-form-field>
          </ng-template>
        </div>

        <div class="grid">
          <mat-form-field appearance="outline">
            <mat-label>Original CV Role</mat-label>
            <input matInput [name]="'rawRoleTitle' + i" [(ngModel)]="exp.rawRoleTitle" />
          </mat-form-field>

          <div class="review-actions">
            <button
              *ngIf="!showRoleEditor(exp)"
              mat-flat-button
              color="warn"
              class="change-mapping-button"
              type="button"
              (click)="enableRoleReview(i)">
              Change Mapping
            </button>

            <mat-checkbox
              *ngIf="showRoleEditor(exp)"
              [name]="'reviewedByUser' + i"
              [(ngModel)]="exp.reviewedByUser">
              Mark mapping as reviewed
            </mat-checkbox>
          </div>
        </div>

        <div class="grid">
          <mat-form-field appearance="outline">
            <mat-label>Start Date (YYYY-MM)</mat-label>
            <input matInput [name]="'startDate' + i" [(ngModel)]="exp.startDate" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>End Date (YYYY-MM / Present)</mat-label>
            <input matInput [name]="'endDate' + i" [(ngModel)]="exp.endDate" />
          </mat-form-field>
        </div>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Description</mat-label>
          <textarea matInput rows="3" [name]="'description' + i" [(ngModel)]="exp.description"></textarea>
        </mat-form-field>
      </ng-template>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="close()">Cancel</button>
      <button mat-flat-button color="primary" type="button" (click)="save()">Save</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .dialog-content {
      min-width: min(92vw, 900px);
      max-height: 72vh;
      display: grid;
      gap: 0.75rem;
      overflow: auto;
    }

    .file-path {
      margin: 0;
      color: #4b5563;
    }

    .grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 0.75rem;
    }

    .experience-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-top: 0.25rem;
    }

    .experience-header.compact {
      margin-top: 0;
    }

    .role-exp-block {
      border: 1px solid #dbe3ef;
      border-radius: 12px;
      padding: 0.75rem;
      display: grid;
      gap: 0.5rem;
      background: #f8fbff;
    }

    .role-exp-block h3 {
      margin: 0;
    }

    .role-exp-list {
      display: grid;
      gap: 0.35rem;
    }

    .role-exp-item {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 1rem;
      border-bottom: 1px solid #e2e8f0;
      padding-bottom: 0.25rem;
    }

    .role-exp-meta {
      display: flex;
      align-items: center;
      gap: 0.55rem;
      flex-wrap: wrap;
    }

    .role-exp-title {
      font-weight: 700;
      color: #1f2937;
      letter-spacing: 0.01em;
    }

    .role-exp-company {
      font-size: 0.85rem;
      color: #475569;
      border: 1px solid #cbd5e1;
      border-radius: 999px;
      padding: 0.1rem 0.5rem;
      background: #ffffff;
    }

    .role-exp-item:last-child {
      border-bottom: 0;
      padding-bottom: 0;
    }

    .experience-header h3 {
      margin: 0;
    }

    .experience-card {
      border: 1px solid #dbe3ef;
      border-radius: 12px;
      padding: 0.75rem;
      display: grid;
      gap: 0.5rem;
    }

    .auto-matched-section {
      display: grid;
      gap: 0.75rem;
      padding-top: 0.25rem;
    }

    .auto-matched-note,
    .empty-state {
      border: 1px dashed #cbd5e1;
      border-radius: 12px;
      padding: 0.75rem 0.9rem;
      color: #475569;
      background: #f8fafc;
    }

    .review-summary {
      border: 1px solid #fcd34d;
      background: #fffbeb;
      color: #92400e;
      border-radius: 12px;
      padding: 0.75rem 0.9rem;
    }

    .experience-card-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
    }

    .experience-title-row {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      flex-wrap: wrap;
    }

    .experience-badges {
      display: flex;
      align-items: center;
      gap: 0.4rem;
      flex-wrap: wrap;
    }

    .status-badge {
      border-radius: 999px;
      padding: 0.15rem 0.55rem;
      font-size: 0.76rem;
      font-weight: 700;
      letter-spacing: 0.02em;
    }

    .status-unmapped {
      background: #fff4e5;
      color: #9a3412;
      border: 1px solid #fdba74;
    }

    .status-review {
      background: #fef3c7;
      color: #92400e;
      border: 1px solid #fcd34d;
    }

    .status-reviewed {
      background: #dcfce7;
      color: #166534;
      border: 1px solid #86efac;
    }

    .status-matched {
      background: #dcfce7;
      color: #166534;
      border: 1px solid #86efac;
    }

    .status-confidence {
      margin-left: 0.45rem;
      padding-left: 0.45rem;
      border-left: 1px solid rgba(22, 101, 52, 0.25);
      font-variant-numeric: tabular-nums;
    }

    .review-actions {
      display: flex;
      align-items: center;
      min-height: 56px;
    }

    .change-mapping-button {
      box-shadow: none;
    }

    .full-width {
      width: 100%;
    }

    @media (max-width: 900px) {
      .grid {
        grid-template-columns: 1fr;
      }
    }
  `]
})
export class CvEditDialogComponent {
  workingProfile: EditableCandidateProfile;
  skillsCsv = '';
  certificationsCsv = '';
  showAutoMatched = false;
  readonly standardRoles: string[];

  constructor(
    @Inject(MAT_DIALOG_DATA) public readonly data: CvEditDialogData,
    private readonly dialogRef: MatDialogRef<CvEditDialogComponent, EditableCandidateProfile>)
  {
    this.standardRoles = [...data.standardRoles];

    this.workingProfile = {
      ...data.profile,
      jobTitles: [...data.profile.jobTitles],
      companies: [...data.profile.companies],
      skills: [...data.profile.skills],
      certifications: [...data.profile.certifications],
      experiences: data.profile.experiences.map(exp => ({ ...exp })),
      roleExperience: data.profile.roleExperience.map(item => ({ ...item }))
    };

    this.skillsCsv = this.workingProfile.skills.join(', ');
    this.certificationsCsv = this.workingProfile.certifications.join(', ');
  }

  addExperience(): void {
    this.workingProfile = {
      ...this.workingProfile,
      experiences: [...this.workingProfile.experiences, this.createEmptyExperience()]
    };
  }

  removeExperience(index: number): void {
    this.workingProfile = {
      ...this.workingProfile,
      experiences: this.workingProfile.experiences.filter((_, i) => i !== index)
    };
  }

  close(): void {
    this.dialogRef.close();
  }

  save(): void {
    const calculatedExperienceYears = this.calculateTotalExperienceYears(this.workingProfile.experiences);

    const cleanedProfile: EditableCandidateProfile = {
      ...this.workingProfile,
      experienceYears: this.workingProfile.experiences.length > 0
        ? calculatedExperienceYears
        : (Number(this.workingProfile.experienceYears) || 0),
      jobTitles: this.extractUniqueFromExperiences('role'),
      companies: this.extractUniqueFromExperiences('companyName'),
      skills: this.parseCsv(this.skillsCsv),
      certifications: this.parseCsv(this.certificationsCsv),
      experiences: this.workingProfile.experiences.map(exp => ({
        companyName: exp.companyName.trim(),
        rawRoleTitle: (exp.rawRoleTitle ?? '').trim(),
        standardRoleId: exp.standardRoleId ?? null,
        standardRoleName: (exp.standardRoleName ?? '').trim(),
        matchConfidence: exp.matchConfidence ?? null,
        needsReview: !(exp.reviewedByUser ?? false) &&
          ((exp.needsReview ?? false) || !(exp.standardRoleName ?? '').trim()),
        reviewedByUser: exp.reviewedByUser ?? false,
        role: (exp.standardRoleName ?? exp.rawRoleTitle ?? exp.role ?? '').trim(),
        startDate: exp.startDate.trim(),
        endDate: exp.endDate.trim(),
        description: exp.description.trim()
      })),
      roleExperience: this.buildRoleExperience(this.workingProfile.experiences)
    };

    this.dialogRef.close(cleanedProfile);
  }

  private parseCsv(value: string): string[] {
    const seen = new Set<string>();

    return value
      .split(',')
      .map(item => item.trim())
      .filter(item => item.length > 0)
      .filter(item =>
      {
        const key = item.toLowerCase();
        if (seen.has(key))
        {
          return false;
        }

        seen.add(key);
        return true;
      });
  }

  private createEmptyExperience(): ExperienceEntry {
    return {
      companyName: '',
      rawRoleTitle: '',
      standardRoleId: null,
      standardRoleName: '',
      matchConfidence: null,
      needsReview: true,
      reviewedByUser: false,
      role: '',
      startDate: '',
      endDate: '',
      description: ''
    };
  }

  get roleExperiencePreview(): RoleExperienceEntry[] {
    const computed = this.buildRoleExperience(this.workingProfile.experiences);
    return computed.length > 0 ? computed : this.workingProfile.roleExperience;
  }

  getCompaniesForRole(role: string): string {
    const key = role.toLowerCase().replace(/\s+/g, ' ').trim();
    const companies = this.workingProfile.experiences
      .filter(exp => (exp.standardRoleName ?? exp.rawRoleTitle ?? exp.role ?? '').toLowerCase().replace(/\s+/g, ' ').trim() === key)
      .map(exp => (exp.companyName ?? '').trim())
      .filter(name => name.length > 0)
      .filter((name, index, all) => all.findIndex(item => item.toLowerCase() === name.toLowerCase()) === index);

    return companies.length > 0 ? companies.join(', ') : 'N/A';
  }

  getRoleOptions(currentRole: string): string[] {
    return this.standardRoles;
  }

  get pendingRoleReviewCount(): number {
    return this.workingProfile.experiences.filter(exp => this.requiresReview(exp)).length;
  }

  get reviewExperiences(): { item: ExperienceEntry; index: number }[] {
    return this.workingProfile.experiences
      .map((item, index) => ({ item, index }))
      .filter(entry => this.requiresReview(entry.item));
  }

  get autoMatchedExperiences(): { item: ExperienceEntry; index: number }[] {
    return this.workingProfile.experiences
      .map((item, index) => ({ item, index }))
      .filter(entry => !this.requiresReview(entry.item));
  }

  showRoleEditor(experience: ExperienceEntry): boolean {
    return this.requiresReview(experience) || !!experience.reviewedByUser;
  }

  trackByExperienceIndex(_: number, entry: { index: number }): number {
    return entry.index;
  }

  enableRoleReview(index: number): void {
    const experiences = [...this.workingProfile.experiences];
    experiences[index] = {
      ...experiences[index],
      needsReview: true,
      reviewedByUser: false
    };

    this.workingProfile = {
      ...this.workingProfile,
      experiences
    };
  }

  isUnmapped(experience: ExperienceEntry): boolean {
    return !(experience.standardRoleName ?? '').trim();
  }

  requiresReview(experience: ExperienceEntry): boolean {
    return !experience.reviewedByUser &&
      ((experience.needsReview ?? false) || this.isUnmapped(experience));
  }

  private buildRoleExperience(experiences: ExperienceEntry[]): RoleExperienceEntry[] {
    const resolvedExperiences = this.resolveExperienceRanges(experiences);
    const buckets = new Map<string, RoleExperienceEntry>();

    for (const experience of resolvedExperiences) {
      const years = (experience.end.getTime() - experience.start.getTime()) / (1000 * 60 * 60 * 24 * 365.25);
      if (years <= 0.01) {
        continue;
      }

      const key = experience.role.toLowerCase().replace(/\s+/g, ' ').trim();
      const existing = buckets.get(key);

      if (existing) {
        existing.years += years;
      } else {
        buckets.set(key, { jobTitle: experience.role, years });
      }
    }

    return [...buckets.values()]
      .map(item => ({
        jobTitle: item.jobTitle,
        years: Math.round(item.years * 10) / 10
      }))
      .sort((a, b) => b.years - a.years || a.jobTitle.localeCompare(b.jobTitle));
  }

  private calculateTotalExperienceYears(experiences: ExperienceEntry[]): number {
    const resolvedExperiences = this.resolveExperienceRanges(experiences)
      .sort((a, b) => a.start.getTime() - b.start.getTime());

    if (resolvedExperiences.length === 0) {
      return 0;
    }

    let totalMs = 0;
    let currentStart = resolvedExperiences[0].start;
    let currentEnd = resolvedExperiences[0].end;

    for (const experience of resolvedExperiences.slice(1)) {
      if (experience.start.getTime() <= currentEnd.getTime()) {
        if (experience.end.getTime() > currentEnd.getTime()) {
          currentEnd = experience.end;
        }

        continue;
      }

      totalMs += currentEnd.getTime() - currentStart.getTime();
      currentStart = experience.start;
      currentEnd = experience.end;
    }

    totalMs += currentEnd.getTime() - currentStart.getTime();
    return Math.round((totalMs / (1000 * 60 * 60 * 24 * 365.25)));
  }

  private resolveExperienceRanges(experiences: ExperienceEntry[]): { role: string; start: Date; end: Date }[] {
    const drafts = experiences
      .map(exp => {
        const role = exp.standardRoleName?.trim() || exp.rawRoleTitle?.trim() || exp.role?.trim() || '';
        const start = this.tryParseDate(exp.startDate);
        const end = this.tryParseDate(exp.endDate);
        const isCurrent = /^(present|current|now)$/i.test((exp.endDate ?? '').trim());

        return { role, start, end, isCurrent };
      })
      .filter(item => item.role.length > 0 && !!item.start)
      .sort((a, b) => a.start!.getTime() - b.start!.getTime());

    const now = new Date();
    const resolved: { role: string; start: Date; end: Date }[] = [];

    for (let i = 0; i < drafts.length; i++) {
      const current = drafts[i];
      const start = current.start!;
      let end = current.end;

      if (!end) {
        if (current.isCurrent || i === drafts.length - 1) {
          end = now;
        } else {
          const nextStart = drafts[i + 1].start!;
          end = nextStart.getTime() > start.getTime() ? nextStart : start;
        }
      }

      if (end.getTime() < start.getTime()) {
        continue;
      }

      resolved.push({ role: current.role, start, end });
    }

    return resolved;
  }

  private extractUniqueFromExperiences(field: 'role' | 'companyName'): string[] {
    const values = this.workingProfile.experiences
      .map(exp => field === 'role'
        ? (exp.standardRoleName ?? exp.rawRoleTitle ?? exp.role ?? '').trim()
        : (exp.companyName ?? '').trim())
      .filter(value => value.length > 0);

    const uniqueValues = values
      .filter((value, index, all) => all.findIndex(item => item.toLowerCase() === value.toLowerCase()) === index);

    if (uniqueValues.length > 0) {
      return uniqueValues;
    }

    return field === 'role'
      ? [...this.workingProfile.jobTitles]
      : [...this.workingProfile.companies];
  }

  private tryParseDate(value?: string): Date | null {
    const raw = (value ?? '').trim();
    if (!raw) {
      return null;
    }

    if (/^(present|current|now)$/i.test(raw)) {
      return new Date();
    }

    if (/^\d{4}-\d{2}$/.test(raw)) {
      const parsed = new Date(`${raw}-01T00:00:00Z`);
      return Number.isNaN(parsed.getTime()) ? null : parsed;
    }

    if (/^\d{4}$/.test(raw)) {
      const parsed = new Date(`${raw}-01-01T00:00:00Z`);
      return Number.isNaN(parsed.getTime()) ? null : parsed;
    }

    const parsed = new Date(raw);
    return Number.isNaN(parsed.getTime()) ? null : parsed;
  }
}
