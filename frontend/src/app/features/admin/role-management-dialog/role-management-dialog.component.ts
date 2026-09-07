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
import { AdminModuleName, MODULE_SUB_PERMISSIONS, Role, SubPermissionOption } from '../../../core/models/role.model';
import { ConfirmDialogComponent } from '../../../shared/confirm-dialog/confirm-dialog.component';

const MODULE_OPTIONS: { value: AdminModuleName; label: string }[] = [
  { value: 'Orders', label: 'Orders' },
  { value: 'MenuItems', label: 'Menu Items' },
  { value: 'Settings', label: 'Settings' },
  { value: 'Staff', label: 'Staff' },
  { value: 'Customers', label: 'Customers' },
  { value: 'Crm', label: 'CRM' },
  { value: 'Campaigns', label: 'Campaigns' },
  { value: 'Scanner', label: 'Scanner' },
  { value: 'PromoCodes', label: 'Promo Codes' }
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
  readonly newRolePermissions = signal<string[]>([]);
  readonly adding = signal(false);
  readonly editingId = signal<number | null>(null);
  readonly editingName = signal('');
  readonly editingModules = signal<AdminModuleName[]>([]);
  readonly editingPermissions = signal<string[]>([]);
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

  // Unchecking a module also clears (and thereby hides) all of its nested sub-permissions
  // - a role can't be left holding "Orders.Create" without the "Orders" module itself.
  toggleModule(
    modules: WritableSignal<AdminModuleName[]>,
    permissions: WritableSignal<string[]>,
    module: AdminModuleName
  ): void {
    const current = modules();
    const isChecked = current.includes(module);

    if (isChecked) {
      modules.set(current.filter((m) => m !== module));
      const subPermissions = this.subPermissionsFor(module).map((p) => p.value);
      permissions.set(permissions().filter((p) => !subPermissions.includes(p)));
    } else {
      modules.set([...current, module]);
    }
  }

  subPermissionsFor(module: AdminModuleName): SubPermissionOption[] {
    return MODULE_SUB_PERMISSIONS[module] ?? [];
  }

  isPermissionChecked(permissions: WritableSignal<string[]>, permission: string): boolean {
    return permissions().includes(permission);
  }

  togglePermission(permissions: WritableSignal<string[]>, permission: string): void {
    const current = permissions();
    permissions.set(
      current.includes(permission) ? current.filter((p) => p !== permission) : [...current, permission]
    );
  }

  addRole(): void {
    const name = this.newRoleName().trim();
    if (!name) {
      return;
    }

    this.adding.set(true);
    const request = { name, modules: this.newRoleModules(), granularPermissions: this.newRolePermissions() };
    this.roleService.create(request).subscribe({
      next: () => {
        this.adding.set(false);
        this.newRoleName.set('');
        this.newRoleModules.set([]);
        this.newRolePermissions.set([]);
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
    this.editingPermissions.set([...role.granularPermissions]);
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.editingName.set('');
    this.editingModules.set([]);
    this.editingPermissions.set([]);
  }

  saveEdit(role: Role): void {
    const name = this.editingName().trim();
    if (!name) {
      this.cancelEdit();
      return;
    }

    this.savingEdit.set(true);
    const request = { name, modules: this.editingModules(), granularPermissions: this.editingPermissions() };
    this.roleService.update(role.id, request).subscribe({
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
