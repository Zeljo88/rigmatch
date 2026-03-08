import { CommonModule } from '@angular/common';
import { HttpClient, HttpParams } from '@angular/common/http';
import { AfterViewInit, Component, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginator, MatPaginatorModule } from '@angular/material/paginator';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { ConfirmDialogComponent } from './confirm-dialog.component';
import { CvEditDialogComponent } from './cv-edit-dialog.component';
import {
  CompanyCvDetailResponse,
  CompanyCvDetailView,
  CompanyCvListItem,
  CompanyCvStructuredProfile,
  CompanyCvUploadResponse,
  EditableCandidateProfile,
  ExperienceEntry,
  RoleExperienceEntry
} from './cv-types';

type Page = 'upload' | 'library' | 'settings' | 'candidateDetail';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatToolbarModule,
    MatSidenavModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatDialogModule,
    MatSnackBarModule,
    MatProgressSpinnerModule,
    MatCheckboxModule
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements AfterViewInit {
  private readonly http = inject(HttpClient);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly apiBaseUrl = 'http://localhost:5168';
  readonly companyId = 'rigmatch-demo-company';
  readonly displayedColumns = ['name', 'latestTitle', 'highestEducation', 'experienceYears', 'createdAtUtc', 'actions'];

  @ViewChild(MatPaginator) paginator?: MatPaginator;
  @ViewChild(MatSort) sort?: MatSort;

  activePage: Page = 'upload';

  selectedFile: File | null = null;
  isUploading = false;
  isLoadingLibrary = false;
  isBusyAction = false;

  searchQuery = '';
  minExpFilter: number | null = null;
  educationFilter = '';
  certFilter = '';
  needsReviewOnlyFilter = false;

  selectedCvDetail: CompanyCvDetailView | null = null;
  dataSource = new MatTableDataSource<CompanyCvListItem>([]);
  private standardRolesCache: string[] = [];

  ngAfterViewInit(): void {
    this.dataSource.paginator = this.paginator ?? null;
    this.dataSource.sort = this.sort ?? null;
    this.dataSource.sortingDataAccessor = (item, property) => {
      if (property === 'createdAtUtc') {
        return new Date(item.createdAtUtc).getTime();
      }

      if (property === 'experienceYears') {
        return item.experienceYears ?? -1;
      }

      return (item as Record<string, unknown>)[property] as string | number;
    };
  }

  setPage(page: Page): void {
    if (page === 'candidateDetail' && !this.selectedCvDetail) {
      return;
    }

    this.activePage = page;

    if (page === 'library') {
      this.loadLibrary();
    }
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.[0] ?? null;
  }

  uploadCv(): void {
    if (!this.selectedFile || this.isUploading) {
      return;
    }

    const formData = new FormData();
    formData.append('file', this.selectedFile);

    this.isUploading = true;

    this.http
      .post<CompanyCvUploadResponse>(`${this.apiBaseUrl}/company/cv/upload`, formData, {
        headers: { 'X-Company-Id': this.companyId }
      })
      .subscribe({
        next: (response) => {
          this.isUploading = false;
          this.selectedFile = null;
          this.loadStandardRoles(standardRoles => {
            this.openEditDialog(
              response.id,
              response.fileUrl,
              this.toEditableProfile(response.parsedProfile),
              'Review Parsed CV',
              standardRoles);
          });
        },
        error: (error) => {
          this.isUploading = false;
          this.showError((error?.error?.message as string) ?? 'CV upload failed.');
        }
      });
  }

  loadLibrary(): void {
    if (this.isLoadingLibrary) {
      return;
    }

    this.isLoadingLibrary = true;
    const hasFilters =
      this.searchQuery.trim().length > 0 ||
      this.minExpFilter !== null ||
      this.educationFilter.trim().length > 0 ||
      this.certFilter.trim().length > 0 ||
      this.needsReviewOnlyFilter;

    const endpoint = hasFilters ? '/company/cvs/search' : '/company/cvs';
    let params = new HttpParams();

    if (hasFilters) {
      if (this.searchQuery.trim()) {
        params = params.set('q', this.searchQuery.trim());
      }

      if (this.minExpFilter !== null && this.minExpFilter >= 0) {
        params = params.set('minExp', this.minExpFilter.toString());
      }

      if (this.educationFilter.trim()) {
        params = params.set('education', this.educationFilter.trim());
      }

      if (this.certFilter.trim()) {
        params = params.set('cert', this.certFilter.trim());
      }

      if (this.needsReviewOnlyFilter) {
        params = params.set('needsReview', 'true');
      }
    }

    this.http
      .get<CompanyCvListItem[]>(`${this.apiBaseUrl}${endpoint}`, {
        headers: { 'X-Company-Id': this.companyId },
        params
      })
      .subscribe({
        next: (response) => {
          this.dataSource.data = response;
          this.isLoadingLibrary = false;

          if (this.paginator) {
            this.paginator.firstPage();
          }
        },
        error: (error) => {
          this.isLoadingLibrary = false;
          this.showError((error?.error?.message as string) ?? 'Failed to load CV library.');
        }
      });
  }

  clearFilters(): void {
    this.searchQuery = '';
    this.minExpFilter = null;
    this.educationFilter = '';
    this.certFilter = '';
    this.needsReviewOnlyFilter = false;
    this.loadLibrary();
  }

  viewCv(item: CompanyCvListItem): void {
    this.isBusyAction = true;
    this.http
      .get<CompanyCvDetailResponse>(`${this.apiBaseUrl}/company/cvs/${item.id}`, {
        headers: { 'X-Company-Id': this.companyId }
      })
      .subscribe({
        next: (response) => {
          this.isBusyAction = false;
          this.selectedCvDetail = {
            id: response.id,
            fileUrl: response.fileUrl,
            isFinalized: response.isFinalized,
            createdAtUtc: response.createdAtUtc,
            updatedAtUtc: response.updatedAtUtc,
            downloadUrl: response.downloadUrl,
            structuredProfile: this.parseStructuredProfile(response.structuredProfileJson)
          };
          this.activePage = 'candidateDetail';
        },
        error: (error) => {
          this.isBusyAction = false;
          this.showError((error?.error?.message as string) ?? 'Failed to load CV details.');
        }
      });
  }

  editCv(item: CompanyCvListItem): void {
    this.isBusyAction = true;
    this.http
      .get<CompanyCvDetailResponse>(`${this.apiBaseUrl}/company/cvs/${item.id}`, {
        headers: { 'X-Company-Id': this.companyId }
      })
      .subscribe({
        next: (response) => {
          this.isBusyAction = false;
          const profile = this.toEditableProfile(this.parseStructuredProfile(response.structuredProfileJson));
          this.loadStandardRoles(standardRoles => {
            this.openEditDialog(response.id, response.fileUrl, profile, 'Edit CV Record', standardRoles);
          });
        },
        error: (error) => {
          this.isBusyAction = false;
          this.showError((error?.error?.message as string) ?? 'Failed to load CV for editing.');
        }
      });
  }

  deleteCv(item: CompanyCvListItem): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '420px',
      data: {
        title: 'Delete CV record',
        message: `Delete ${item.name} from your CV Library? This cannot be undone.`,
        confirmText: 'Delete',
        cancelText: 'Cancel'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.isBusyAction = true;
      this.http
        .delete(`${this.apiBaseUrl}/company/cv/${item.id}`, {
          headers: { 'X-Company-Id': this.companyId }
        })
        .subscribe({
          next: () => {
            this.isBusyAction = false;
            this.dataSource.data = this.dataSource.data.filter(row => row.id !== item.id);

            if (this.selectedCvDetail?.id === item.id) {
              this.selectedCvDetail = null;
              if (this.activePage === 'candidateDetail') {
                this.activePage = 'library';
              }
            }

            this.snackBar.open('CV deleted.', 'Close', { duration: 3000 });
          },
          error: (error) => {
            this.isBusyAction = false;
            this.showError((error?.error?.message as string) ?? 'Failed to delete CV record.');
          }
        });
    });
  }

  closeDetail(): void {
    this.selectedCvDetail = null;
    this.setPage('library');
  }

  backToLibrary(): void {
    this.setPage('library');
  }

  private openEditDialog(
    cvId: string,
    fileUrl: string,
    profile: EditableCandidateProfile,
    title: string,
    standardRoles: string[]): void
  {
    const dialogRef = this.dialog.open(CvEditDialogComponent, {
      width: '980px',
      maxWidth: '96vw',
      data: {
        title,
        fileUrl,
        profile,
        standardRoles
      }
    });

    dialogRef.afterClosed().subscribe(updatedProfile => {
      if (!updatedProfile) {
        return;
      }

      this.saveCvProfile(cvId, updatedProfile);
    });
  }

  private saveCvProfile(cvId: string, profile: EditableCandidateProfile): void {
    this.isBusyAction = true;

    this.http
      .post(`${this.apiBaseUrl}/company/cv/${cvId}/save`, { finalProfile: profile }, {
        headers: { 'X-Company-Id': this.companyId }
      })
      .subscribe({
        next: () => {
          this.isBusyAction = false;
          this.snackBar.open('CV changes saved.', 'Close', { duration: 3000 });
          this.setPage('library');
          this.loadLibrary();

          if (this.selectedCvDetail?.id === cvId) {
            this.selectedCvDetail = {
              ...this.selectedCvDetail,
              structuredProfile: {
                ...profile,
                roleExperience: this.buildRoleExperience(profile.experiences)
              },
              isFinalized: true,
              updatedAtUtc: new Date().toISOString()
            };
          }
        },
        error: (error) => {
          this.isBusyAction = false;
          this.showError((error?.error?.message as string) ?? 'Failed to save CV changes.');
        }
      });
  }

  private showError(message: string): void {
    this.snackBar.open(message, 'Close', { duration: 4500 });
  }

  private loadStandardRoles(onSuccess: (roles: string[]) => void): void {
    if (this.standardRolesCache.length > 0) {
      onSuccess(this.standardRolesCache);
      return;
    }

    this.http
      .get<string[]>(`${this.apiBaseUrl}/company/roles/standard`, {
        headers: { 'X-Company-Id': this.companyId }
      })
      .subscribe({
        next: roles => {
          this.standardRolesCache = roles;
          onSuccess(roles);
        },
        error: () => {
          this.showError('Failed to load standard roles.');
          onSuccess([]);
        }
      });
  }

  private parseStructuredProfile(json: string): CompanyCvStructuredProfile {
    try {
      const raw = JSON.parse(json) as Record<string, unknown>;
      const pick = <T>(...keys: string[]): T | undefined => {
        for (const key of keys) {
          const value = raw[key];
          if (value !== undefined && value !== null) {
            return value as T;
          }
        }

        return undefined;
      };

      const mapExperiences = (source: unknown): ExperienceEntry[] => {
        if (!Array.isArray(source)) {
          return [];
        }

        return source.map(item => {
          const value = item as Record<string, unknown>;
          return {
            companyName: String(value['companyName'] ?? value['CompanyName'] ?? ''),
            rawRoleTitle: String(value['rawRoleTitle'] ?? value['RawRoleTitle'] ?? ''),
            standardRoleId: Number(value['standardRoleId'] ?? value['StandardRoleId'] ?? 0) || null,
            standardRoleName: String(value['standardRoleName'] ?? value['StandardRoleName'] ?? value['role'] ?? value['Role'] ?? ''),
            matchConfidence: Number(value['matchConfidence'] ?? value['MatchConfidence'] ?? 0) || null,
            needsReview: Boolean(value['needsReview'] ?? value['NeedsReview'] ?? false),
            reviewedByUser: Boolean(value['reviewedByUser'] ?? value['ReviewedByUser'] ?? false),
            role: String(value['role'] ?? value['Role'] ?? ''),
            startDate: String(value['startDate'] ?? value['StartDate'] ?? ''),
            endDate: String(value['endDate'] ?? value['EndDate'] ?? ''),
            description: String(value['description'] ?? value['Description'] ?? '')
          };
        });
      };

      const mapRoleExperience = (source: unknown): RoleExperienceEntry[] => {
        if (!Array.isArray(source)) {
          return [];
        }

        return source
          .map(item => {
            const value = item as Record<string, unknown>;
            const yearsRaw = value['years'] ?? value['Years'] ?? 0;
            const years = typeof yearsRaw === 'number' ? yearsRaw : Number(yearsRaw);

            return {
              jobTitle: String(value['jobTitle'] ?? value['JobTitle'] ?? ''),
              years: Number.isFinite(years) ? years : 0
            };
          })
          .filter(item => item.jobTitle.trim().length > 0);
      };

      const jobTitles = pick<unknown[]>('jobTitles', 'JobTitles');
      const companies = pick<unknown[]>('companies', 'Companies');
      const skills = pick<unknown[]>('skills', 'Skills');
      const certifications = pick<unknown[]>('certifications', 'Certifications');
      const expYears = pick<number | string>('experienceYears', 'ExperienceYears');

      return {
        name: pick<string>('name', 'Name') ?? null,
        email: pick<string>('email', 'Email') ?? null,
        phoneNumber: pick<string>('phoneNumber', 'PhoneNumber') ?? null,
        highestEducation: pick<string>('highestEducation', 'HighestEducation') ?? null,
        location: pick<string>('location', 'Location') ?? null,
        jobTitles: Array.isArray(jobTitles) ? jobTitles.map(value => String(value)) : [],
        companies: Array.isArray(companies) ? companies.map(value => String(value)) : [],
        skills: Array.isArray(skills) ? skills.map(value => String(value)) : [],
        certifications: Array.isArray(certifications) ? certifications.map(value => String(value)) : [],
        experienceYears: expYears === undefined ? null : Number(expYears),
        experiences: mapExperiences(pick('experiences', 'Experiences')),
        roleExperience: mapRoleExperience(pick('roleExperience', 'RoleExperience'))
      };
    } catch {
      return {};
    }
  }

  private toEditableProfile(profile: CompanyCvStructuredProfile): EditableCandidateProfile {
    const experiences = (profile.experiences ?? []).map(exp => ({ ...exp }));
    const roleExperience = (profile.roleExperience && profile.roleExperience.length > 0)
      ? [...profile.roleExperience]
      : this.buildRoleExperience(experiences);

    return {
      name: (profile.name ?? '').trim(),
      email: (profile.email ?? '').trim(),
      phoneNumber: (profile.phoneNumber ?? '').trim(),
      highestEducation: (profile.highestEducation ?? '').trim(),
      location: (profile.location ?? '').trim(),
      jobTitles: [...(profile.jobTitles ?? [])],
      companies: [...(profile.companies ?? [])],
      skills: [...(profile.skills ?? [])],
      certifications: [...(profile.certifications ?? [])],
      experienceYears: profile.experienceYears ?? 0,
      experiences,
      roleExperience
    };
  }

  private buildRoleExperience(experiences: ExperienceEntry[]): RoleExperienceEntry[] {
    const buckets = new Map<string, RoleExperienceEntry>();

    for (const experience of experiences) {
      const role = experience.standardRoleName?.trim() || experience.role?.trim() || experience.rawRoleTitle?.trim();
      if (!role) {
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

      const key = role.toLowerCase().replace(/\s+/g, ' ').trim();
      const existing = buckets.get(key);

      if (existing) {
        existing.years += years;
      } else {
        buckets.set(key, { jobTitle: role, years });
      }
    }

    return [...buckets.values()]
      .map(item => ({
        jobTitle: item.jobTitle,
        years: Math.round(item.years * 10) / 10
      }))
      .sort((a, b) => b.years - a.years || a.jobTitle.localeCompare(b.jobTitle));
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

  getCompaniesForRole(role: string, experiences?: ExperienceEntry[]): string {
    if (!experiences || experiences.length === 0) {
      return 'N/A';
    }

    const target = role.toLowerCase().replace(/\s+/g, ' ').trim();
    const companies = experiences
      .filter(exp => {
        const currentRole = (exp.standardRoleName ?? exp.role ?? exp.rawRoleTitle ?? '')
          .toLowerCase()
          .replace(/\s+/g, ' ')
          .trim();
        return currentRole === target;
      })
      .map(exp => (exp.companyName ?? '').trim())
      .filter(company => company.length > 0)
      .filter((company, index, all) => all.findIndex(item => item.toLowerCase() === company.toLowerCase()) === index);

    return companies.length > 0 ? companies.join(', ') : 'N/A';
  }
}
