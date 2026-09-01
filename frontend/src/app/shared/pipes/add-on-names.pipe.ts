import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'addOnNames', standalone: true })
export class AddOnNamesPipe implements PipeTransform {
  transform(addOns: { name: string }[]): string {
    return addOns.map((a) => a.name).join(', ');
  }
}
