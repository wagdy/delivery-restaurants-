import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AddOnService } from '../../../core/services/add-on.service';
import { AddOn } from '../../../core/models/add-on.model';
import { ConfirmDialogComponent } from '../../../shared/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-add-on-management-dialog',
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
  templateUrl: './add-on-management-dialog.component.html',
  styleUrl: './add-on-management-dialog.component.scss'
})
export class AddOnManagementDialogComponent {
  private readonly addOnService = inject(AddOnService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly ref = inject(MatDialogRef<AddOnManagementDialogComponent>);

  readonly loading = signal(true);
  readonly addOns = signal<AddOn[]>([]);

  readonly newName = signal('');
  readonly newPrice = signal(0);
  readonly adding = signal(false);

  readonly editingId = signal<number | null>(null);
  readonly editingName = signal('');
  readonly editingPrice = signal(0);
  readonly savingEdit = signal(false);

  private mutated = false;

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.addOnService.getAll().subscribe({
      next: (addOns) => {
        this.addOns.set(addOns);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.snackBar.open('Failed to load add-ons.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  addAddOn(): void {
    const name = this.newName().trim();
    if (!name) {
      return;
    }

    this.adding.set(true);
    this.addOnService.create({ name, price: this.newPrice() }).subscribe({
      next: () => {
        this.adding.set(false);
        this.newName.set('');
        this.newPrice.set(0);
        this.mutated = true;
        this.load();
      },
      error: (err) => {
        this.adding.set(false);
        this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to create add-on.', 'Dismiss', {
          duration: 4000
        });
      }
    });
  }

  startEdit(addOn: AddOn): void {
    this.editingId.set(addOn.id);
    this.editingName.set(addOn.name);
    this.editingPrice.set(addOn.price);
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.editingName.set('');
    this.editingPrice.set(0);
  }

  saveEdit(addOn: AddOn): void {
    const name = this.editingName().trim();
    const price = this.editingPrice();
    if (!name) {
      this.cancelEdit();
      return;
    }

    this.savingEdit.set(true);
    this.addOnService.update(addOn.id, { name, price }).subscribe({
      next: () => {
        this.savingEdit.set(false);
        this.mutated = true;
        this.cancelEdit();
        this.load();
      },
      error: (err) => {
        this.savingEdit.set(false);
        this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to update add-on.', 'Dismiss', {
          duration: 4000
        });
      }
    });
  }

  delete(addOn: AddOn): void {
    const confirmRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Delete add-on',
        message: `Delete "${addOn.name}"? This only works if it hasn't been used in any past order.`,
        confirmLabel: 'Delete',
        danger: true
      }
    });

    confirmRef.afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) {
        return;
      }

      this.addOnService.delete(addOn.id).subscribe({
        next: () => {
          this.mutated = true;
          this.load();
        },
        error: (err) => {
          this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to delete add-on.', 'Dismiss', {
            duration: 6000
          });
        }
      });
    });
  }

  close(): void {
    this.ref.close(this.mutated);
  }
}
