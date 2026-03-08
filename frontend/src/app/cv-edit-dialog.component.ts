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

      <div class="experience-header">
        <h3>Experience Entries</h3>
        <button mat-stroked-button type="button" (click)="addExperience()">Add Experience</button>
      </div>

      <div class="experience-card" *ngFor="let exp of workingProfile.experiences; index as i">
        <div class="experience-card-head">
          <strong>Experience {{ i + 1 }}</strong>
          <button mat-button color="warn" type="button" (click)="removeExperience(i)">Remove</button>
        </div>

        <div class="grid">
          <mat-form-field appearance="outline">
            <mat-label>Company</mat-label>
            <input matInput [name]="'company' + i" [(ngModel)]="exp.companyName" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Standard Role</mat-label>
            <mat-select [name]="'standardRoleName' + i" [(ngModel)]="exp.standardRoleName">
              <mat-option *ngFor="let roleOption of getRoleOptions(exp.standardRoleName)" [value]="roleOption">
                {{ roleOption }}
              </mat-option>
            </mat-select>
          </mat-form-field>
        </div>

        <div class="grid">
          <mat-form-field appearance="outline">
            <mat-label>Original CV Role</mat-label>
            <input matInput [name]="'rawRoleTitle' + i" [(ngModel)]="exp.rawRoleTitle" />
          </mat-form-field>

          <mat-checkbox [name]="'reviewedByUser' + i" [(ngModel)]="exp.reviewedByUser">
            Mark mapping as reviewed
          </mat-checkbox>
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
      </div>
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

    .experience-card-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
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
    const cleanedProfile: EditableCandidateProfile = {
      ...this.workingProfile,
      experienceYears: Number(this.workingProfile.experienceYears) || 0,
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
        needsReview: exp.needsReview ?? false,
        reviewedByUser: exp.reviewedByUser ?? false,
        role: (exp.standardRoleName ?? exp.role ?? '').trim(),
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
      .filter(exp => (exp.standardRoleName ?? exp.role ?? '').toLowerCase().replace(/\s+/g, ' ').trim() === key)
      .map(exp => (exp.companyName ?? '').trim())
      .filter(name => name.length > 0)
      .filter((name, index, all) => all.findIndex(item => item.toLowerCase() === name.toLowerCase()) === index);

    return companies.length > 0 ? companies.join(', ') : 'N/A';
  }

  getRoleOptions(currentRole: string): string[] {
    const normalizedCurrentRole = currentRole.trim();
    if (!normalizedCurrentRole) {
      return this.standardRoles;
    }

    const exists = this.standardRoles.some(role => role.toLowerCase() === normalizedCurrentRole.toLowerCase());
    if (exists) {
      return this.standardRoles;
    }

    return [normalizedCurrentRole, ...this.standardRoles];
  }

  private buildRoleExperience(experiences: ExperienceEntry[]): RoleExperienceEntry[] {
    const buckets = new Map<string, RoleExperienceEntry>();

    for (const experience of experiences) {
      const role = experience.role?.trim();
      const standardRole = experience.standardRoleName?.trim() || role;
      if (!standardRole) {
        continue;
      }

      const start = this.tryParseDate(experience.startDate);
      if (!start) {
        continue;
      }

      const end = this.tryParseDate(experience.endDate) ?? new Date();
      if (end.getTime() < start.getTime()) {
        continue;
      }

      const years = (end.getTime() - start.getTime()) / (1000 * 60 * 60 * 24 * 365.25);
      if (years <= 0.01) {
        continue;
      }

      const key = standardRole.toLowerCase().replace(/\s+/g, ' ').trim();
      const existing = buckets.get(key);

      if (existing) {
        existing.years += years;
      } else {
        buckets.set(key, { jobTitle: standardRole, years });
      }
    }

    return [...buckets.values()]
      .map(item => ({
        jobTitle: item.jobTitle,
        years: Math.round(item.years * 10) / 10
      }))
      .sort((a, b) => b.years - a.years || a.jobTitle.localeCompare(b.jobTitle));
  }

  private extractUniqueFromExperiences(field: 'role' | 'companyName'): string[] {
    const values = this.workingProfile.experiences
      .map(exp => field === 'role'
        ? (exp.standardRoleName ?? exp.role ?? '').trim()
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
