import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, inject } from '@angular/core';

type UploadResponse = {
  originalFileName: string;
  sizeBytes: number;
  storagePath: string;
  uploadedAtUtc: string;
};

type TextExtractionResponse = {
  originalFileName: string;
  sizeBytes: number;
  storagePath: string;
  extractedText: string;
  characterCount: number;
  extractedAtUtc: string;
};

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  private readonly http = inject(HttpClient);

  readonly apiBaseUrl = 'http://localhost:5168';
  selectedFile: File | null = null;
  isUploading = false;
  isExtracting = false;
  errorMessage = '';
  uploadResult: UploadResponse | null = null;
  extractionResult: TextExtractionResponse | null = null;

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;

    this.selectedFile = file;
    this.errorMessage = '';
    this.uploadResult = null;
    this.extractionResult = null;
  }

  uploadCv(): void {
    if (!this.selectedFile || this.isUploading) {
      return;
    }

    const formData = new FormData();
    formData.append('file', this.selectedFile);

    this.isUploading = true;
    this.errorMessage = '';
    this.uploadResult = null;

    this.http.post<UploadResponse>(`${this.apiBaseUrl}/cv/upload`, formData).subscribe({
      next: (response) => {
        this.uploadResult = response;
        this.isUploading = false;
      },
      error: (error) => {
        const message = error?.error?.message as string | undefined;
        this.errorMessage = message ?? 'Upload failed. Please try again.';
        this.isUploading = false;
      }
    });
  }

  extractTextFromCv(): void {
    if (!this.selectedFile || this.isExtracting) {
      return;
    }

    const formData = new FormData();
    formData.append('file', this.selectedFile);

    this.isExtracting = true;
    this.errorMessage = '';
    this.extractionResult = null;

    this.http.post<TextExtractionResponse>(`${this.apiBaseUrl}/cv/extract-text`, formData).subscribe({
      next: (response) => {
        this.extractionResult = response;
        this.isExtracting = false;
      },
      error: (error) => {
        const message = error?.error?.message as string | undefined;
        this.errorMessage = message ?? 'Text extraction failed. Please try again.';
        this.isExtracting = false;
      }
    });
  }
}
