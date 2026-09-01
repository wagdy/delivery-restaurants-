import { Component, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';

export type MenuViewMode = 'list' | 'grid';

@Component({
  selector: 'app-menu-search-filter',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  templateUrl: './menu-search-filter.component.html',
  styleUrl: './menu-search-filter.component.scss'
})
export class MenuSearchFilterComponent {
  // Category labels only - "All" is implicit (activeCategory() === null) rather than
  // a list entry, so callers never have to special-case it in their own data.
  readonly categories = input<string[]>(['Drinks', 'Mains', 'Starters']);

  readonly searchChanged = output<string>();
  readonly categorySelected = output<string | null>();
  readonly viewChanged = output<MenuViewMode>();

  readonly searchTerm = signal('');
  readonly activeCategory = signal<string | null>(null);
  readonly activeView = signal<MenuViewMode>('grid');

  onSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.searchTerm.set(value);
    this.searchChanged.emit(value);
  }

  selectCategory(category: string | null): void {
    this.activeCategory.set(category);
    this.categorySelected.emit(category);
  }

  setView(view: MenuViewMode): void {
    if (this.activeView() === view) {
      return;
    }
    this.activeView.set(view);
    this.viewChanged.emit(view);
  }
}
