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
  duplicateWarnings: CompanyCvDuplicateWarning[];
};

export type CompanyCvDuplicateWarning = {
  type: 'exact' | 'probable' | 'possible' | string;
  message: string;
  existingCvId: string;
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

export type CompanyProjectListItem = {
  id: string;
  title: string;
  primaryRole: string;
  location: string;
  status: string;
  createdAtUtc: string;
  updatedAtUtc: string | null;
};

export type CompanyProjectCandidateMatch = {
  cvId: string;
  candidateName: string;
  currentRole: string;
  experienceYears: number;
  matchScore: number;
  roleMatchType: string;
  matchedRequiredCertifications: string[];
  missingRequiredCertifications: string[];
  matchedPreferredCertifications: string[];
  matchedRequiredSkills: string[];
  missingRequiredSkills: string[];
  matchedPreferredSkills: string[];
  meetsMinimumExperience: boolean;
  locationMatched: boolean;
  educationMatched: boolean;
  summaryPoints: string[];
};

export type CompanyProjectDetailResponse = {
  id: string;
  title: string;
  clientName: string;
  primaryRole: string;
  additionalRoles: string[];
  requiredSkills: string[];
  preferredSkills: string[];
  requiredCertifications: string[];
  preferredCertifications: string[];
  minimumExperienceYears: number | null;
  location: string;
  preferredEducation: string;
  description: string;
  status: string;
  startDateUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  candidateMatches: CompanyProjectCandidateMatch[];
};

export type EditableCompanyProjectForm = {
  id: string | null;
  title: string;
  clientName: string;
  primaryRole: string;
  additionalRoles: string[];
  requiredSkillsText: string;
  preferredSkillsText: string;
  requiredCertificationsText: string;
  preferredCertificationsText: string;
  minimumExperienceYears: number | null;
  location: string;
  preferredEducation: string;
  description: string;
  status: string;
  startDate: string;
};
