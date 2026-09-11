import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { SurveySectionService } from '../../../core/services/survey-section.service';
import { SurveyMatrixSection } from '../../../core/models/review.model';

// A flat add/delete list - no inline rename, unlike RoleManagementDialogComponent's
// heavier pattern - matrix sections are just a named lookup with no nested structure to
// edit, matching the simpler ask for this dialog.
@Component({
  selector: 'app-survey-section-management-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './survey-section-management-dialog.component.html',
  styleUrl: './survey-section-management-dialog.component.scss'
})
export class SurveySectionManagementDialogComponent {
  private readonly sectionService = inject(SurveySectionService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly ref = inject(MatDialogRef<SurveySectionManagementDialogComponent>);

  readonly loading = signal(true);
  readonly sections = signal<SurveyMatrixSection[]>([]);
  readonly newSectionName = signal('');
  readonly adding = signal(false);
  readonly deletingId = signal<number | null>(null);

  // Tracked so the opener (CustomerReviewsComponent) knows whether to re-fetch its own
  // section dropdown data on close - same convention as RoleManagementDialogComponent.
  private mutated = false;

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.sectionService.getAll().subscribe({
      next: (sections) => {
        this.sections.set(sections);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.snackBar.open('Failed to load sections.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  addSection(): void {
    const name = this.newSectionName().trim();
    if (!name) {
      return;
    }

    this.adding.set(true);
    this.sectionService.create({ name }).subscribe({
      next: () => {
        this.adding.set(false);
        this.newSectionName.set('');
        this.mutated = true;
        this.load();
      },
      error: (err) => {
        this.adding.set(false);
        this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to create section.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  // No confirmation dialog here (unlike question/staff deletion elsewhere) - deleting a
  // section is already guarded server-side (blocked with a clear message if any question
  // still uses it), so there's no destructive history loss a confirm step would be
  // protecting against.
  deleteSection(section: SurveyMatrixSection): void {
    this.deletingId.set(section.id);
    this.sectionService.delete(section.id).subscribe({
      next: () => {
        this.deletingId.set(null);
        this.mutated = true;
        this.load();
      },
      error: (err) => {
        this.deletingId.set(null);
        this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to delete section.', 'Dismiss', { duration: 6000 });
      }
    });
  }

  close(): void {
    this.ref.close(this.mutated);
  }
}
