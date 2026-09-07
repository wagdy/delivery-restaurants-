import { Component, ElementRef, HostListener, computed, forwardRef, inject, input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MenuItem } from '../../core/models/menu-item.model';

// A searchable autocomplete replacement for the plain <mat-select> the Create Order POS
// form's item rows used to use - a native <select>-style dropdown only lets a cashier jump
// to an option by its first letter, which doesn't scale to a menu with dozens of items.
// Implements ControlValueAccessor so it drops straight into the existing
// formControlName="menuItemId" binding with no changes needed anywhere else in the form
// (createItemPrice/reviewLines etc. still just read the plain menuItemId the control holds).
@Component({
  selector: 'app-menu-item-combobox',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  templateUrl: './menu-item-combobox.component.html',
  styleUrl: './menu-item-combobox.component.scss',
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => MenuItemComboboxComponent),
      multi: true
    }
  ]
})
export class MenuItemComboboxComponent implements ControlValueAccessor {
  private readonly elementRef = inject(ElementRef<HTMLElement>);

  readonly menuItems = input<MenuItem[]>([]);

  readonly searchTerm = signal('');
  readonly isOpen = signal(false);
  readonly disabled = signal(false);
  private readonly selectedItemId = signal<number | null>(null);

  private onChange: (value: number | null) => void = () => {};
  private onTouched: () => void = () => {};

  // Case-insensitive substring match against the full name (not just a startsWith prefix
  // check) - "burger" finds "Classic Cheeseburger" too, not just names starting with it.
  readonly filteredItems = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    const items = this.menuItems();
    if (!term) {
      return items;
    }
    return items.filter((item) => item.name.toLowerCase().includes(term));
  });

  // Closes on any click outside this component's own host element - checking
  // elementRef.nativeElement.contains(...) rather than a (blur) handler specifically
  // avoids the classic combobox bug where blur fires (and would hide the list) before a
  // click on one of its own <li> options is registered.
  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.isOpen() && !this.elementRef.nativeElement.contains(event.target as Node)) {
      this.closeAndResync();
    }
  }

  writeValue(value: number | null): void {
    this.selectedItemId.set(value);
    this.isOpen.set(false);
    this.searchTerm.set(this.labelFor(value));
  }

  registerOnChange(fn: (value: number | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }

  onFocus(): void {
    this.isOpen.set(true);
    // Clears the "Name · L.E Price" label so the cashier can start typing a fresh search
    // immediately instead of first deleting the old text - closeAndResync restores it if
    // they click away without picking a new item, so the field never looks empty while
    // the form still actually holds a valid selection underneath.
    this.searchTerm.set('');
  }

  onInput(value: string): void {
    this.searchTerm.set(value);
    this.isOpen.set(true);
  }

  // Only this method ever changes the underlying FormControl value - typing alone just
  // filters the visible list, matching a native <select>'s "nothing changes until you
  // choose an option" behavior instead of clearing a valid selection on a stray click.
  selectItem(item: MenuItem): void {
    this.selectedItemId.set(item.id);
    this.searchTerm.set(this.labelFor(item.id));
    this.isOpen.set(false);
    this.onChange(item.id);
    this.onTouched();
  }

  private closeAndResync(): void {
    this.isOpen.set(false);
    this.searchTerm.set(this.labelFor(this.selectedItemId()));
    this.onTouched();
  }

  private labelFor(menuItemId: number | null): string {
    const match = menuItemId !== null ? this.menuItems().find((m) => m.id === menuItemId) : undefined;
    return match ? `${match.name} · L.E ${match.price.toFixed(2)}` : '';
  }
}
