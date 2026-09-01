import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FormsModule } from '@angular/forms';
import { OrderService } from '../../../core/services/order.service';
import { AuthService } from '../../../core/services/auth.service';
import { ORDER_STATUSES, Order, OrderStatus } from '../../../core/models/order.model';
import { ConfirmDialogComponent } from '../../../shared/confirm-dialog/confirm-dialog.component';
import { OrderFormDialogComponent } from '../order-form-dialog/order-form-dialog.component';
import { AddOnNamesPipe } from '../../../shared/pipes/add-on-names.pipe';

@Component({
  selector: 'app-order-details-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatFormFieldModule,
    MatSelectModule,
    MatProgressSpinnerModule,
    AddOnNamesPipe
  ],
  templateUrl: './order-details-dialog.component.html',
  styleUrl: './order-details-dialog.component.scss'
})
export class OrderDetailsDialogComponent {
  private readonly orderService = inject(OrderService);
  private readonly authService = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly ref = inject(MatDialogRef<OrderDetailsDialogComponent>);

  readonly order = signal<Order>(inject(MAT_DIALOG_DATA));
  readonly statuses = ORDER_STATUSES;
  readonly updatingStatus = signal(false);
  readonly deleting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  private didMutate = false;

  readonly isAdmin = this.authService.isAdmin;
  readonly isCaptain = this.authService.isCaptain;

  get isEditable(): boolean {
    const status = this.order().status;
    return status !== 'Delivered' && status !== 'Cancelled';
  }

  // A captain accepts a ready order (kitchen has it in Preparing) to start delivering it,
  // then marks it Delivered on completion — a purpose-built pair of actions rather than
  // exposing the full admin status dropdown (which includes Cancel / revert-to-Pending).
  get canAccept(): boolean {
    const status = this.order().status;
    return status === 'Pending' || status === 'Preparing';
  }

  get canMarkDelivered(): boolean {
    return this.order().status === 'OutForDelivery';
  }

  onStatusChange(status: OrderStatus): void {
    if (status === this.order().status) {
      return;
    }
    this.updateStatus(status);
  }

  acceptOrder(): void {
    this.updateStatus('OutForDelivery');
  }

  markDelivered(): void {
    this.updateStatus('Delivered');
  }

  private updateStatus(status: OrderStatus): void {
    this.updatingStatus.set(true);
    this.errorMessage.set(null);

    this.orderService.updateStatus(this.order().id, status).subscribe({
      next: (updated) => {
        this.updatingStatus.set(false);
        this.order.set(updated);
        this.didMutate = true;
      },
      error: (err) => {
        this.updatingStatus.set(false);
        this.errorMessage.set(err.error?.errors?.[0] ?? 'Failed to update status.');
      }
    });
  }

  edit(): void {
    const editRef = this.dialog.open(OrderFormDialogComponent, {
      width: '560px',
      data: { mode: 'edit', order: this.order() }
    });

    editRef.afterClosed().subscribe((updated: Order | undefined) => {
      if (updated) {
        this.order.set(updated);
        this.didMutate = true;
      }
    });
  }

  delete(): void {
    const confirmRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Delete order',
        message: `Permanently delete order #${this.order().id}? This cannot be undone.`,
        confirmLabel: 'Delete',
        danger: true
      }
    });

    confirmRef.afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) {
        return;
      }

      this.deleting.set(true);
      this.orderService.delete(this.order().id).subscribe({
        next: () => {
          this.deleting.set(false);
          this.ref.close(true);
        },
        error: (err) => {
          this.deleting.set(false);
          this.errorMessage.set(err.error?.errors?.[0] ?? 'Failed to delete order.');
        }
      });
    });
  }

  close(): void {
    this.ref.close(this.didMutate);
  }
}
