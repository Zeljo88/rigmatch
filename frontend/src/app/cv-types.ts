export type ParsedCandidateProfile = {
  name: string;
  email: string;
  phoneNumber?: string | null;
  highestEducation?: string | null;
  location?: string | null;
  jobTitles: string[];
  companies: string[];
  skills: string[];
  certifications: string[];
  experienceYears: number;
  experiences: ExperienceEntry[];
  roleExperience: RoleExperienceEntry[];
};

export type ExperienceEntry = {
  companyName: string;
  rawRoleTitle: string;
  standardRoleId?: number | null;
  standardRoleName: string;
  matchConfidence?: number | null;
  needsReview?: boolean;
  reviewedByUser?: boolean;
  role?: string;
  startDate: string;
  endDate: string;
  description: string;
};

export type EditableCandidateProfile = {
  name: string;
  email: string;
  phoneNumber: string;
  highestEducation: string;
  location: string;
  jobTitles: string[];
  companies: string[];
  skills: string[];
  certifications: string[];
  experienceYears: number;
  experiences: ExperienceEntry[];
  roleExperience: RoleExperienceEntry[];
};

export type RoleExperienceEntry = {
  jobTitle: string;
  years: number;
};

export type CompanyCvStructuredProfile = {
  name?: string | null;
  email?: string | null;
  phoneNumber?: string | null;
  highestEducation?: string | null;
  location?: string | null;
  jobTitles?: string[];
  companies?: string[];
  skills?: string[];
  certifications?: string[];
  experienceYears?: number | null;
  experiences?: ExperienceEntry[];
  roleExperience?: RoleExperienceEntry[];
};

export type CompanyCvUploadResponse = {
  id: string;
  fileUrl: string;
  parsedProfile: ParsedCandidateProfile;
  createdAtUtc: string;
};

export type CompanyCvListItem = {
  id: string;
  name: string;
  latestTitle: string;
  highestEducation: string | null;
  experienceYears: number | null;
  createdAtUtc: string;
  isFinalized: boolean;
};

export type CompanyCvDetailResponse = {
  id: string;
  fileUrl: string;
  structuredProfileJson: string;
  isFinalized: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  downloadUrl: string;
};

export type CompanyCvDetailView = {
  id: string;
  fileUrl: string;
  isFinalized: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  downloadUrl: string;
  structuredProfile: CompanyCvStructuredProfile;
};
