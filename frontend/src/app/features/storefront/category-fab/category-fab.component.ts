import { Component, HostListener, computed, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-category-fab',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  templateUrl: './category-fab.component.html',
  styleUrl: './category-fab.component.scss'
})
export class CategoryFabComponent {
  // Full ordered list of section names, and whichever one is currently scrolled into
  // view — the popup excludes it, since "jump to the section you're already in" is a
  // dead action. Both are plain data inputs so this component stays reusable/testable
  // outside the storefront (feed it a mock array and it behaves identically).
  readonly categories = input<string[]>([]);
  readonly activeCategory = input<string | null>(null);

  // How far past the top the user must scroll before the FAB fades in — tuned to clear
  // the storefront's hero + search/filter row, not just an arbitrary pixel count.
  readonly scrollThreshold = input(240);

  readonly categorySelected = output<string>();

  readonly visible = signal(false);
  readonly menuOpen = signal(false);

  readonly filteredCategories = computed(() =>
    this.categories().filter((c) => c !== this.activeCategory())
  );

  @HostListener('window:scroll')
  onWindowScroll(): void {
    const pastThreshold = window.scrollY > this.scrollThreshold();
    this.visible.set(pastThreshold);
    if (!pastThreshold) {
      this.menuOpen.set(false);
    }
  }

  toggleMenu(): void {
    this.menuOpen.update((open) => !open);
  }

  closeMenu(): void {
    this.menuOpen.set(false);
  }

  selectCategory(category: string): void {
    this.categorySelected.emit(category);
    this.closeMenu();
  }
}
