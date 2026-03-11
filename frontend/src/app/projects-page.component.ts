import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { ConfirmDialogComponent } from './confirm-dialog.component';
import {
  CompanyProjectCandidateMatch,
  CompanyProjectDetailResponse,
  CompanyProjectListItem,
  EditableCompanyProjectForm
} from './cv-types';

type ProjectView = 'library' | 'form' | 'detail';

@Component({
  selector: 'app-projects-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatSnackBarModule,
    MatTableModule
  ],
  templateUrl: './projects-page.component.html',
  styleUrl: './projects-page.component.scss'
})
export class ProjectsPageComponent implements OnChanges {
  private readonly http = inject(HttpClient);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  @Input({ required: true }) apiBaseUrl = '';
  @Input({ required: true }) companyId = '';
  @Input() active = false;

  @Output() requestCandidateView = new EventEmitter<string>();

  readonly displayedColumns = ['title', 'primaryRole', 'location', 'status', 'createdAtUtc', 'actions'];
  readonly projectStatuses = ['Draft', 'Active', 'Paused', 'Closed'];

  view: ProjectView = 'library';
  isLoadingProjects = false;
  isSavingProject = false;
  isBusyAction = false;

  projectList: CompanyProjectListItem[] = [];
  selectedProjectDetail: CompanyProjectDetailResponse | null = null;
  projectFormMode: 'create' | 'edit' = 'create';
  projectForm: EditableCompanyProjectForm = this.createEmptyProjectForm();
  standardRoles: string[] = [];

  projectMatchMinScore = 0;
  projectMatchesRequireAllCerts = false;
  projectMatchesLocationOnly = false;

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['active']?.currentValue) {
      return;
    }

    this.ensureStandardRolesLoaded();
    if (this.projectList.length === 0 || this.view === 'library') {
      this.loadProjects();
    }
  }

  loadProjects(): void {
    if (this.isLoadingProjects) {
      return;
    }

    this.isLoadingProjects = true;
    this.http
      .get<CompanyProjectListItem[]>(`${this.apiBaseUrl}/company/projects`, {
        headers: { 'X-Company-Id': this.companyId }
      })
      .subscribe({
        next: response => {
          this.projectList = response;
          this.isLoadingProjects = false;
        },
        error: error => {
          this.isLoadingProjects = false;
          this.showError((error?.error?.message as string) ?? 'Failed to load projects.');
        }
      });
  }

  openCreate(): void {
    this.ensureStandardRolesLoaded();
    this.projectFormMode = 'create';
    this.projectForm = this.createEmptyProjectForm();
    this.view = 'form';
  }

  editProject(item: CompanyProjectListItem): void {
    this.isBusyAction = true;
    this.ensureStandardRolesLoaded();
    this.http
      .get<CompanyProjectDetailResponse>(`${this.apiBaseUrl}/company/projects/${item.id}`, {
        headers: { 'X-Company-Id': this.companyId }
      })
      .subscribe({
        next: response => {
          this.isBusyAction = false;
          this.projectFormMode = 'edit';
          this.projectForm = this.toProjectForm(response);
          this.view = 'form';
        },
        error: error => {
          this.isBusyAction = false;
          this.showError((error?.error?.message as string) ?? 'Failed to load project for editing.');
        }
      });
  }

  saveProject(): void {
    if (!this.projectForm.title.trim() || !this.projectForm.primaryRole.trim()) {
      this.showError('Project title and primary role are required.');
      return;
    }

    this.isSavingProject = true;
    const payload = this.toProjectPayload(this.projectForm);
    const request = this.projectForm.id
      ? this.http.put<CompanyProjectDetailResponse>(`${this.apiBaseUrl}/company/projects/${this.projectForm.id}`, payload, {
          headers: { 'X-Company-Id': this.companyId }
        })
      : this.http.post<CompanyProjectDetailResponse>(`${this.apiBaseUrl}/company/projects`, payload, {
          headers: { 'X-Company-Id': this.companyId }
        });

    request.subscribe({
      next: response => {
        this.isSavingProject = false;
        this.selectedProjectDetail = response;
        this.projectForm = this.toProjectForm(response);
        this.projectFormMode = 'edit';
        this.view = 'detail';
        this.loadProjects();
        this.snackBar.open('Project saved.', 'Close', { duration: 3000 });
      },
      error: error => {
        this.isSavingProject = false;
        this.showError((error?.error?.message as string) ?? 'Failed to save project.');
      }
    });
  }

  viewProject(item: CompanyProjectListItem): void {
    this.isBusyAction = true;
    this.http
      .get<CompanyProjectDetailResponse>(`${this.apiBaseUrl}/company/projects/${item.id}`, {
        headers: { 'X-Company-Id': this.companyId }
      })
      .subscribe({
        next: response => {
          this.isBusyAction = false;
          this.selectedProjectDetail = response;
          this.projectMatchMinScore = 0;
          this.projectMatchesRequireAllCerts = false;
          this.projectMatchesLocationOnly = false;
          this.view = 'detail';
        },
        error: error => {
          this.isBusyAction = false;
          this.showError((error?.error?.message as string) ?? 'Failed to load project details.');
        }
      });
  }

  deleteProject(item: CompanyProjectListItem): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '420px',
      data: {
        title: 'Delete project',
        message: `Delete project ${item.title}? This cannot be undone.`,
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
        .delete(`${this.apiBaseUrl}/company/projects/${item.id}`, {
          headers: { 'X-Company-Id': this.companyId }
        })
        .subscribe({
          next: () => {
            this.isBusyAction = false;
            this.projectList = this.projectList.filter(project => project.id !== item.id);
            if (this.selectedProjectDetail?.id === item.id) {
              this.selectedProjectDetail = null;
              this.view = 'library';
            }
            this.snackBar.open('Project deleted.', 'Close', { duration: 3000 });
          },
          error: error => {
            this.isBusyAction = false;
            this.showError((error?.error?.message as string) ?? 'Failed to delete project.');
          }
        });
    });
  }

  openCandidate(match: CompanyProjectCandidateMatch): void {
    this.requestCandidateView.emit(match.cvId);
  }

  cancelForm(): void {
    if (this.selectedProjectDetail?.id === this.projectForm.id) {
      this.view = 'detail';
      return;
    }

    this.view = 'library';
  }

  backToLibrary(): void {
    this.view = 'library';
    this.loadProjects();
  }

  get filteredMatches(): CompanyProjectCandidateMatch[] {
    const matches = this.selectedProjectDetail?.candidateMatches ?? [];
    return matches.filter(match => {
      if (match.matchScore < this.projectMatchMinScore) {
        return false;
      }

      if (this.projectMatchesRequireAllCerts && match.missingRequiredCertifications.length > 0) {
        return false;
      }

      if (this.projectMatchesLocationOnly && !match.locationMatched) {
        return false;
      }

      return true;
    });
  }

  getRoleMatchLabel(match: CompanyProjectCandidateMatch): string {
    switch (match.roleMatchType) {
      case 'primary':
        return 'Primary role match';
      case 'secondary':
        return 'Acceptable role match';
      default:
        return 'Indirect match';
    }
  }

  private ensureStandardRolesLoaded(): void {
    if (this.standardRoles.length > 0) {
      return;
    }

    this.http
      .get<string[]>(`${this.apiBaseUrl}/company/roles/standard`, {
        headers: { 'X-Company-Id': this.companyId }
      })
      .subscribe({
        next: roles => {
          this.standardRoles = roles;
        },
        error: () => {
          this.showError('Failed to load standard roles.');
        }
      });
  }

  private createEmptyProjectForm(): EditableCompanyProjectForm {
    return {
      id: null,
      title: '',
      clientName: '',
      primaryRole: '',
      additionalRoles: [],
      requiredSkillsText: '',
      preferredSkillsText: '',
      requiredCertificationsText: '',
      preferredCertificationsText: '',
      minimumExperienceYears: null,
      location: '',
      preferredEducation: '',
      description: '',
      status: 'Draft',
      startDate: ''
    };
  }

  private toProjectForm(project: CompanyProjectDetailResponse): EditableCompanyProjectForm {
    return {
      id: project.id,
      title: project.title,
      clientName: project.clientName,
      primaryRole: project.primaryRole,
      additionalRoles: [...project.additionalRoles],
      requiredSkillsText: project.requiredSkills.join(', '),
      preferredSkillsText: project.preferredSkills.join(', '),
      requiredCertificationsText: project.requiredCertifications.join(', '),
      preferredCertificationsText: project.preferredCertifications.join(', '),
      minimumExperienceYears: project.minimumExperienceYears,
      location: project.location,
      preferredEducation: project.preferredEducation,
      description: project.description,
      status: project.status,
      startDate: project.startDateUtc ? project.startDateUtc.slice(0, 10) : ''
    };
  }

  private toProjectPayload(project: EditableCompanyProjectForm): Record<string, unknown> {
    return {
      title: project.title.trim(),
      clientName: project.clientName.trim(),
      primaryRole: project.primaryRole.trim(),
      additionalRoles: [...project.additionalRoles],
      requiredSkills: this.splitCommaList(project.requiredSkillsText),
      preferredSkills: this.splitCommaList(project.preferredSkillsText),
      requiredCertifications: this.splitCommaList(project.requiredCertificationsText),
      preferredCertifications: this.splitCommaList(project.preferredCertificationsText),
      minimumExperienceYears: project.minimumExperienceYears,
      location: project.location.trim(),
      preferredEducation: project.preferredEducation.trim(),
      description: project.description.trim(),
      status: project.status.trim(),
      startDateUtc: project.startDate ? new Date(`${project.startDate}T00:00:00Z`).toISOString() : null
    };
  }

  private splitCommaList(value: string): string[] {
    return value
      .split(',')
      .map(item => item.trim())
      .filter(item => item.length > 0)
      .filter((item, index, all) => all.findIndex(other => other.toLowerCase() === item.toLowerCase()) === index);
  }

  private showError(message: string): void {
    this.snackBar.open(message, 'Close', { duration: 4500 });
  }
}
