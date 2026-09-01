import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { CartService } from '../../../core/services/cart.service';
import { AddOnNamesPipe } from '../../../shared/pipes/add-on-names.pipe';

@Component({
  selector: 'app-cart-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule, MatIconModule, AddOnNamesPipe],
  templateUrl: './cart-dialog.component.html',
  styleUrl: './cart-dialog.component.scss'
})
export class CartDialogComponent {
  protected readonly cart = inject(CartService);
  private readonly router = inject(Router);
  private readonly ref = inject(MatDialogRef<CartDialogComponent>);

  checkout(): void {
    this.ref.close();
    this.router.navigateByUrl('/checkout');
  }

  close(): void {
    this.ref.close();
  }
}
