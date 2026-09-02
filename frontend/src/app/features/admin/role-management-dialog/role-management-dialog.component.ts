import { Component, WritableSignal, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RoleService } from '../../../core/services/role.service';
import { AdminModuleName, Role } from '../../../core/models/role.model';
import { ConfirmDialogComponent } from '../../../shared/confirm-dialog/confirm-dialog.component';

const MODULE_OPTIONS: { value: AdminModuleName; label: string }[] = [
  { value: 'Orders', label: 'Orders' },
  { value: 'MenuItems', label: 'Menu Items' },
  { value: 'Settings', label: 'Settings' },
  { value: 'Staff', label: 'Staff' },
  { value: 'Customers', label: 'Customers' }
];

@Component({
  selector: 'app-role-management-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatCheckboxModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './role-management-dialog.component.html',
  styleUrl: './role-management-dialog.component.scss'
})
export class RoleManagementDialogComponent {
  private readonly roleService = inject(RoleService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly ref = inject(MatDialogRef<RoleManagementDialogComponent>);

  readonly moduleOptions = MODULE_OPTIONS;

  readonly loading = signal(true);
  readonly roles = signal<Role[]>([]);
  readonly newRoleName = signal('');
  readonly newRoleModules = signal<AdminModuleName[]>([]);
  readonly adding = signal(false);
  readonly editingId = signal<number | null>(null);
  readonly editingName = signal('');
  readonly editingModules = signal<AdminModuleName[]>([]);
  readonly savingEdit = signal(false);
  private mutated = false;

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.roleService.getAll().subscribe({
      next: (roles) => {
        this.roles.set(roles);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.snackBar.open('Failed to load roles.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  isModuleChecked(modules: WritableSignal<AdminModuleName[]>, module: AdminModuleName): boolean {
    return modules().includes(module);
  }

  toggleModule(modules: WritableSignal<AdminModuleName[]>, module: AdminModuleName): void {
    const current = modules();
    modules.set(
      current.includes(module) ? current.filter((m) => m !== module) : [...current, module]
    );
  }

  addRole(): void {
    const name = this.newRoleName().trim();
    if (!name) {
      return;
    }

    this.adding.set(true);
    this.roleService.create({ name, modules: this.newRoleModules() }).subscribe({
      next: () => {
        this.adding.set(false);
        this.newRoleName.set('');
        this.newRoleModules.set([]);
        this.mutated = true;
        this.load();
      },
      error: (err) => {
        this.adding.set(false);
        this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to create role.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  startEdit(role: Role): void {
    this.editingId.set(role.id);
    this.editingName.set(role.name);
    this.editingModules.set([...role.modules]);
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.editingName.set('');
    this.editingModules.set([]);
  }

  saveEdit(role: Role): void {
    const name = this.editingName().trim();
    if (!name) {
      this.cancelEdit();
      return;
    }

    this.savingEdit.set(true);
    this.roleService.update(role.id, { name, modules: this.editingModules() }).subscribe({
      next: () => {
        this.savingEdit.set(false);
        this.mutated = true;
        this.cancelEdit();
        this.load();
      },
      error: (err) => {
        this.savingEdit.set(false);
        this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to update role.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  delete(role: Role): void {
    const confirmRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Delete role',
        message: `Delete "${role.name}"? This only works if no staff accounts are assigned to it.`,
        confirmLabel: 'Delete',
        danger: true
      }
    });

    confirmRef.afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) {
        return;
      }

      this.roleService.delete(role.id).subscribe({
        next: () => {
          this.mutated = true;
          this.load();
        },
        error: (err) => {
          this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to delete role.', 'Dismiss', { duration: 6000 });
        }
      });
    });
  }

  close(): void {
    this.ref.close(this.mutated);
  }
}
