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
import { MatTableModule } from '@angular/material/table';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AuthService } from '../../../core/services/auth.service';
import { RoleService } from '../../../core/services/role.service';
import { Role } from '../../../core/models/role.model';
import { StaffAccount } from '../../../core/models/auth.model';
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
    MatProgressSpinnerModule,
    MatTableModule
  ],
  templateUrl: './staff-accounts.component.html',
  styleUrl: './staff-accounts.component.scss'
})
export class StaffAccountsComponent {
  // Kept identical to the [RegularExpression] patterns on CreateStaffUserRequest/
  // UpdateStaffUserRequest (backend/.../DTOs/Auth/) — client-side validation is only a
  // fast-feedback convenience, the backend re-checks the same rule regardless.
  static readonly NAME_PATTERN = /^[A-Za-z ]+$/;
  static readonly PHONE_PATTERN = /^[0-9]+$/;

  readonly captainOptionValue = CAPTAIN_OPTION_VALUE;
  readonly displayedColumns = ['fullName', 'phoneNumber', 'role', 'actions'];

  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly roleService = inject(RoleService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly roles = signal<Role[]>([]);

  readonly staffList = signal<StaffAccount[]>([]);
  readonly loadingStaff = signal(true);

  // Non-null while editing an existing account - the same form above the table doubles as
  // both the create and edit form, matching the requested "populate the existing creation
  // form" behavior rather than a separate edit dialog. null = create mode.
  readonly editingStaffId = signal<string | null>(null);

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
    this.loadStaff();
  }

  loadRoles(): void {
    this.roleService.getAll().subscribe({
      next: (roles) => this.roles.set(roles),
      error: () => this.snackBar.open('Failed to load roles.', 'Dismiss', { duration: 4000 })
    });
  }

  loadStaff(): void {
    this.loadingStaff.set(true);
    this.authService.getStaff().subscribe({
      next: (staff) => {
        this.staffList.set(staff);
        this.loadingStaff.set(false);
      },
      error: () => {
        this.loadingStaff.set(false);
        this.snackBar.open('Failed to load staff accounts.', 'Dismiss', { duration: 4000 });
      }
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

  // "Captain Order (Delivery Driver)" / the assigned Role's own name, matching the exact
  // labels the Role <mat-select> itself already uses - falls back to a generic label for
  // the rare case of an Admin whose CustomRoleId no longer resolves (a Role deleted out
  // from under them, defaulting to full access - see AuthService.ResolveAdminModuleNamesAsync).
  roleLabel(staff: StaffAccount): string {
    if (staff.role === 'CaptainOrder') {
      return 'Captain Order (Delivery Driver)';
    }
    return staff.roleName ?? 'Admin (full access)';
  }

  // Populates the form above with this staff member's data and switches it into edit
  // mode - no password field needed/shown, editing never changes it.
  editStaff(staff: StaffAccount): void {
    this.editingStaffId.set(staff.id);
    this.errorMessage.set(null);
    this.form.controls.password.clearValidators();
    this.form.controls.password.updateValueAndValidity();
    this.form.patchValue({
      fullName: staff.fullName,
      phoneNumber: staff.phoneNumber ?? '',
      staffRole: staff.role === 'CaptainOrder' ? CAPTAIN_OPTION_VALUE : (staff.roleId?.toString() ?? CAPTAIN_OPTION_VALUE)
    });
  }

  cancelEdit(): void {
    this.editingStaffId.set(null);
    this.errorMessage.set(null);
    this.form.controls.password.setValidators([Validators.required, Validators.minLength(8)]);
    this.form.reset({ staffRole: CAPTAIN_OPTION_VALUE });
  }

  // window.confirm per explicit instruction, rather than this app's usual
  // ConfirmDialogComponent (see CustomerInsightsComponent.deleteCustomer) - a plain native
  // confirm is enough friction for an action that already has a server-side safety net
  // (DeleteStaffUserAsync's own-account and recorded-transactions guards).
  deleteStaff(staff: StaffAccount): void {
    if (!window.confirm(`Delete "${staff.fullName}"? This permanently removes their account - it cannot be undone.`)) {
      return;
    }

    this.authService.deleteStaffUser(staff.id).subscribe({
      next: () => {
        this.loadStaff();
        this.snackBar.open('Staff account deleted.', 'Dismiss', { duration: 3000 });
      },
      error: (err) => {
        this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to delete staff account.', 'Dismiss', { duration: 6000 });
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
    const editingId = this.editingStaffId();

    if (editingId) {
      this.authService
        .updateStaffUser(editingId, {
          fullName: raw.fullName,
          phoneNumber: raw.phoneNumber,
          role: isCaptain ? 'CaptainOrder' : 'Admin',
          roleId: isCaptain ? null : Number(raw.staffRole)
        })
        .subscribe({
          next: (staff) => {
            this.saving.set(false);
            this.snackBar.open(`${staff.fullName} updated.`, 'Dismiss', { duration: 4000 });
            this.cancelEdit();
            this.loadStaff();
          },
          error: (err) => {
            this.saving.set(false);
            this.errorMessage.set(err.error?.errors?.[0] ?? 'Failed to update account.');
          }
        });
      return;
    }

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
          this.loadStaff();
        },
        error: (err) => {
          this.saving.set(false);
          this.errorMessage.set(err.error?.errors?.[0] ?? 'Failed to create account.');
        }
      });
  }
}
