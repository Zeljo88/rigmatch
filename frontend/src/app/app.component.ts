import { CommonModule } from '@angular/common';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';

type ParsedCandidateProfile = {
  name: string;
  email: string;
  location?: string | null;
  jobTitles: string[];
  companies: string[];
  skills: string[];
  certifications: string[];
  experienceYears: number;
};

type ExperienceEntry = {
  companyName: string;
  role: string;
  startDate: string;
  endDate: string;
  description: string;
};

type EditableCandidateProfile = {
  name: string;
  email: string;
  location: string;
  jobTitles: string[];
  companies: string[];
  skills: string[];
  certifications: string[];
  experienceYears: number;
  experiences: ExperienceEntry[];
};

type CompanyCvStructuredProfile = {
  name?: string | null;
  email?: string | null;
  location?: string | null;
  jobTitles?: string[];
  companies?: string[];
  skills?: string[];
  certifications?: string[];
  experienceYears?: number | null;
  experiences?: ExperienceEntry[];
};

type CompanyCvUploadResponse = {
  id: string;
  fileUrl: string;
  parsedProfile: ParsedCandidateProfile;
  createdAtUtc: string;
};

type CompanyCvListItem = {
  id: string;
  name: string;
  latestTitle: string;
  location: string | null;
  experienceYears: number | null;
  createdAtUtc: string;
  isFinalized: boolean;
};

type CompanyCvDetailResponse = {
  id: string;
  fileUrl: string;
  structuredProfileJson: string;
  isFinalized: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  downloadUrl: string;
};

type CompanyCvDetailView = {
  id: string;
  fileUrl: string;
  isFinalized: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  downloadUrl: string;
  structuredProfile: CompanyCvStructuredProfile;
};

type Page = 'upload' | 'edit' | 'library' | 'detail';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  private readonly http = inject(HttpClient);

  readonly apiBaseUrl = 'http://localhost:5168';
  readonly companyId = 'rigmatch-demo-company';

  activePage: Page = 'upload';

  selectedFile: File | null = null;
  currentCvId: string | null = null;

  isUploading = false;
  isSaving = false;
  isLoadingLibrary = false;
  isLoadingDetail = false;

  errorMessage = '';
  successMessage = '';

  uploadedFileUrl = '';

  editableProfile: EditableCandidateProfile = this.createEmptyProfile();
  jobTitlesInput = '';
  companiesInput = '';
  newSkillInput = '';
  newCertificationInput = '';

  cvLibrary: CompanyCvListItem[] = [];

  searchQuery = '';
  minExpFilter: number | null = null;
  locationFilter = '';
  certFilter = '';

  selectedCvDetail: CompanyCvDetailView | null = null;

  setPage(page: Page): void {
    if (page === 'detail' && !this.selectedCvDetail) {
      return;
    }

    this.activePage = page;
    this.errorMessage = '';
    this.successMessage = '';

    if (page === 'library') {
      this.loadLibrary();
    }
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;

    this.selectedFile = file;
    this.currentCvId = null;
    this.uploadedFileUrl = '';
    this.errorMessage = '';
    this.successMessage = '';
    this.selectedCvDetail = null;
  }

  uploadCv(): void {
    if (!this.selectedFile || this.isUploading) {
      return;
    }

    const formData = new FormData();
    formData.append('file', this.selectedFile);

    this.isUploading = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.http
      .post<CompanyCvUploadResponse>(`${this.apiBaseUrl}/company/cv/upload`, formData, {
        headers: { 'X-Company-Id': this.companyId }
      })
      .subscribe({
        next: (response) => {
          this.currentCvId = response.id;
          this.uploadedFileUrl = response.fileUrl;
          this.hydrateEditableProfile(response.parsedProfile);
          this.successMessage = 'CV uploaded and parsed. Review and save the profile.';
          this.isUploading = false;
          this.activePage = 'edit';
        },
        error: (error) => {
          this.errorMessage = (error?.error?.message as string) ?? 'CV upload failed.';
          this.isUploading = false;
        }
      });
  }

  saveCv(): void {
    if (!this.currentCvId || this.isSaving) {
      return;
    }

    this.isSaving = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.http
      .post(`${this.apiBaseUrl}/company/cv/${this.currentCvId}/save`, { finalProfile: this.editableProfile }, {
        headers: { 'X-Company-Id': this.companyId }
      })
      .subscribe({
        next: () => {
          this.successMessage = 'CV profile saved to company library.';
          this.isSaving = false;
          this.setPage('library');
        },
        error: (error) => {
          this.errorMessage = (error?.error?.message as string) ?? 'Saving CV profile failed.';
          this.isSaving = false;
        }
      });
  }

  loadLibrary(): void {
    this.fetchLibrary();
  }

  searchLibrary(): void {
    this.fetchLibrary();
  }

  clearLibraryFilters(): void {
    this.searchQuery = '';
    this.minExpFilter = null;
    this.locationFilter = '';
    this.certFilter = '';
    this.fetchLibrary();
  }

  openCvDetail(cvId: string): void {
    if (this.isLoadingDetail) {
      return;
    }

    this.isLoadingDetail = true;
    this.errorMessage = '';

    this.http
      .get<CompanyCvDetailResponse>(`${this.apiBaseUrl}/company/cvs/${cvId}`, {
        headers: { 'X-Company-Id': this.companyId }
      })
      .subscribe({
        next: (response) => {
          this.selectedCvDetail = {
            id: response.id,
            fileUrl: response.fileUrl,
            isFinalized: response.isFinalized,
            createdAtUtc: response.createdAtUtc,
            updatedAtUtc: response.updatedAtUtc,
            downloadUrl: response.downloadUrl,
            structuredProfile: this.parseStructuredProfile(response.structuredProfileJson)
          };

          this.isLoadingDetail = false;
          this.activePage = 'detail';
        },
        error: (error) => {
          this.errorMessage = (error?.error?.message as string) ?? 'Failed to load CV details.';
          this.isLoadingDetail = false;
        }
      });
  }

  editSelectedCv(): void {
    if (!this.selectedCvDetail) {
      return;
    }

    this.currentCvId = this.selectedCvDetail.id;
    this.uploadedFileUrl = this.selectedCvDetail.fileUrl;
    this.hydrateEditableProfile(this.selectedCvDetail.structuredProfile);
    this.successMessage = 'Loaded CV record into edit form.';
    this.activePage = 'edit';
  }

  onListInputChanged(field: 'jobTitles' | 'companies', value: string): void {
    const parsedValues = value
      .split(',')
      .map(item => item.trim())
      .filter(item => item.length > 0);

    this.editableProfile = {
      ...this.editableProfile,
      [field]: parsedValues
    };
  }

  addSkill(): void {
    this.editableProfile = this.addUniqueListItem(this.editableProfile, 'skills', this.newSkillInput);
    this.newSkillInput = '';
  }

  removeSkill(index: number): void {
    this.editableProfile = {
      ...this.editableProfile,
      skills: this.editableProfile.skills.filter((_, i) => i !== index)
    };
  }

  addCertification(): void {
    this.editableProfile = this.addUniqueListItem(this.editableProfile, 'certifications', this.newCertificationInput);
    this.newCertificationInput = '';
  }

  removeCertification(index: number): void {
    this.editableProfile = {
      ...this.editableProfile,
      certifications: this.editableProfile.certifications.filter((_, i) => i !== index)
    };
  }

  addExperience(): void {
    this.editableProfile = {
      ...this.editableProfile,
      experiences: [...this.editableProfile.experiences, this.createEmptyExperience()]
    };
  }

  removeExperience(index: number): void {
    this.editableProfile = {
      ...this.editableProfile,
      experiences: this.editableProfile.experiences.filter((_, i) => i !== index)
    };
  }

  private fetchLibrary(): void {
    if (this.isLoadingLibrary) {
      return;
    }

    this.isLoadingLibrary = true;
    this.errorMessage = '';

    const hasFilters =
      this.searchQuery.trim().length > 0 ||
      this.minExpFilter !== null ||
      this.locationFilter.trim().length > 0 ||
      this.certFilter.trim().length > 0;

    const endpoint = hasFilters ? '/company/cvs/search' : '/company/cvs';
    let params = new HttpParams();

    if (hasFilters) {
      if (this.searchQuery.trim()) {
        params = params.set('q', this.searchQuery.trim());
      }

      if (this.minExpFilter !== null && this.minExpFilter >= 0) {
        params = params.set('minExp', this.minExpFilter.toString());
      }

      if (this.locationFilter.trim()) {
        params = params.set('location', this.locationFilter.trim());
      }

      if (this.certFilter.trim()) {
        params = params.set('cert', this.certFilter.trim());
      }
    }

    this.http
      .get<CompanyCvListItem[]>(`${this.apiBaseUrl}${endpoint}`, {
        headers: { 'X-Company-Id': this.companyId },
        params
      })
      .subscribe({
        next: (response) => {
          this.cvLibrary = response;
          this.isLoadingLibrary = false;
        },
        error: (error) => {
          this.errorMessage = (error?.error?.message as string) ?? 'Failed to load company CV library.';
          this.isLoadingLibrary = false;
        }
      });
  }

  private parseStructuredProfile(json: string): CompanyCvStructuredProfile {
    try {
      return JSON.parse(json) as CompanyCvStructuredProfile;
    } catch {
      return {};
    }
  }

  private hydrateEditableProfile(profile: CompanyCvStructuredProfile): void {
    this.editableProfile = {
      name: profile.name ?? '',
      email: profile.email ?? '',
      location: profile.location ?? '',
      jobTitles: [...(profile.jobTitles ?? [])],
      companies: [...(profile.companies ?? [])],
      skills: [...(profile.skills ?? [])],
      certifications: [...(profile.certifications ?? [])],
      experienceYears: profile.experienceYears ?? 0,
      experiences: [...(profile.experiences ?? [])]
    };

    this.jobTitlesInput = this.editableProfile.jobTitles.join(', ');
    this.companiesInput = this.editableProfile.companies.join(', ');
    this.newSkillInput = '';
    this.newCertificationInput = '';
  }

  private addUniqueListItem(
    profile: EditableCandidateProfile,
    field: 'skills' | 'certifications',
    value: string): EditableCandidateProfile {
    const trimmedValue = value.trim();
    if (!trimmedValue) {
      return profile;
    }

    const exists = profile[field].some(item => item.toLowerCase() === trimmedValue.toLowerCase());
    if (exists) {
      return profile;
    }

    return {
      ...profile,
      [field]: [...profile[field], trimmedValue]
    };
  }

  private createEmptyProfile(): EditableCandidateProfile {
    return {
      name: '',
      email: '',
      location: '',
      jobTitles: [],
      companies: [],
      skills: [],
      certifications: [],
      experienceYears: 0,
      experiences: []
    };
  }

  private createEmptyExperience(): ExperienceEntry {
    return {
      companyName: '',
      role: '',
      startDate: '',
      endDate: '',
      description: ''
    };
  }
}
