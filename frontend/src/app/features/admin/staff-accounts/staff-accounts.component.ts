import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AuthService } from '../../../core/services/auth.service';
import { RoleService } from '../../../core/services/role.service';
import { Role } from '../../../core/models/role.model';
import { RoleManagementDialogComponent } from '../role-management-dialog/role-management-dialog.component';

// The Role <mat-select> needs one bindable value, but the domain has two orthogonal
// facts (UserRole + optional custom RoleId) - this sentinel represents "Captain Order",
// every other option value is the custom role's id as a string (see submit()/loadRoles()).
const CAPTAIN_OPTION_VALUE = 'captain';

@Component({
  selector: 'app-staff-accounts',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './staff-accounts.component.html',
  styleUrl: './staff-accounts.component.scss'
})
export class StaffAccountsComponent {
  // Kept identical to the [RegularExpression] patterns on CreateStaffUserRequest
  // (backend/.../DTOs/Auth/CreateStaffUserRequest.cs) — client-side validation is only a
  // fast-feedback convenience, the backend re-checks the same rule regardless.
  static readonly NAME_PATTERN = /^[A-Za-z ]+$/;
  static readonly PHONE_PATTERN = /^[0-9]+$/;

  readonly captainOptionValue = CAPTAIN_OPTION_VALUE;

  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly roleService = inject(RoleService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly roles = signal<Role[]>([]);

  readonly form = this.fb.nonNullable.group({
    fullName: [
      '',
      [Validators.required, Validators.maxLength(200), Validators.pattern(StaffAccountsComponent.NAME_PATTERN)]
    ],
    phoneNumber: ['', [Validators.required, Validators.pattern(StaffAccountsComponent.PHONE_PATTERN)]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    staffRole: [CAPTAIN_OPTION_VALUE, [Validators.required]]
  });

  constructor() {
    this.loadRoles();
  }

  loadRoles(): void {
    this.roleService.getAll().subscribe({
      next: (roles) => this.roles.set(roles),
      error: () => this.snackBar.open('Failed to load roles.', 'Dismiss', { duration: 4000 })
    });
  }

  openRoleManagement(): void {
    const dialogRef = this.dialog.open(RoleManagementDialogComponent, { width: '640px' });

    dialogRef.afterClosed().subscribe((mutated: boolean | undefined) => {
      if (mutated) {
        this.loadRoles();
      }
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    const raw = this.form.getRawValue();
    const isCaptain = raw.staffRole === CAPTAIN_OPTION_VALUE;

    this.authService
      .createStaffUser({
        fullName: raw.fullName,
        phoneNumber: raw.phoneNumber,
        password: raw.password,
        role: isCaptain ? 'CaptainOrder' : 'Admin',
        roleId: isCaptain ? null : Number(raw.staffRole)
      })
      .subscribe({
        next: (user) => {
          this.saving.set(false);
          this.snackBar.open(`${user.fullName} created as ${user.role}.`, 'Dismiss', { duration: 4000 });
          this.form.reset({ staffRole: CAPTAIN_OPTION_VALUE });
        },
        error: (err) => {
          this.saving.set(false);
          this.errorMessage.set(err.error?.errors?.[0] ?? 'Failed to create account.');
        }
      });
  }
}
