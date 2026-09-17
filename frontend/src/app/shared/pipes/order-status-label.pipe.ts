import { Pipe, PipeTransform } from '@angular/core';
import { ORDER_STATUS_LABELS, OrderStatus } from '../../core/models/order.model';

// Turns a stored status into something a person should read: "OutForDelivery" was
// rendering verbatim in the customer's own order list, and "ReadyForCollection" would
// have been worse. Pure, since the mapping is constant.
@Pipe({ name: 'orderStatusLabel', standalone: true })
export class OrderStatusLabelPipe implements PipeTransform {
  transform(status: OrderStatus | null | undefined): string {
    if (!status) {
      return '';
    }

    // Falls back to the raw value rather than an empty cell if the backend ever sends a
    // status this build has not heard of - a new deploy reaching an old cached bundle is
    // exactly the case (see the service worker note in AppComponent).
    return ORDER_STATUS_LABELS[status] ?? status;
  }
}
